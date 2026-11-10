using System.Text.Json;
using System.Net;
using System.Net.Http;
using GameHub.Core;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var doc = Vdf.Parse("// comment\n\"libraryfolders\" { \"0\" { \"path\" \"D:\\\\SteamLibrary\" } \"1\" { \"path\" \"E:\\\\Games\" } }");
Check(doc.Descendants().Where(n => n.Name == "path").Select(n => n.Value).SequenceEqual(new[] { @"D:\SteamLibrary", @"E:\Games" }), "VDF nested libraries and escaped paths");
try { Vdf.Parse("\"broken\" {"); throw new Exception("Invalid VDF accepted"); } catch (FormatException) { passed++; Console.WriteLine("PASS: reject truncated VDF"); }
string root = Path.Combine(Path.GetTempPath(), "gamehub-check-" + Guid.NewGuid().ToString("N"));
try
{
    string steam = Path.Combine(root, "steam"), extra = Path.Combine(root, "extra"), epic = Path.Combine(root, "epic");
    Directory.CreateDirectory(Path.Combine(steam, "steamapps", "common", "Test Game"));
    Directory.CreateDirectory(Path.Combine(extra, "steamapps", "common", "Other Game"));
    Directory.CreateDirectory(epic);
    File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + extra.Replace("\\", "\\\\") + "\" } }");
    File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_42.acf"), "\"AppState\" { \"appid\" \"42\" \"name\" \"Test Game\" \"installdir\" \"Test Game\" }");
    File.WriteAllText(Path.Combine(extra, "steamapps", "appmanifest_43.acf"), "\"AppState\" { \"appid\" \"43\" \"name\" \"Other Game\" \"installdir\" \"Other Game\" }");
    File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_99.acf"), "\"AppState\" { \"appid\" \"99\" \"name\" \"Uninstalled\" \"installdir\" \"Missing\" }");
    string install = Path.Combine(root, "EpicGame"); Directory.CreateDirectory(install); File.WriteAllText(Path.Combine(install, "game.exe"), "fixture");
    File.WriteAllText(Path.Combine(epic, "test.item"), JsonSerializer.Serialize(new { AppName = "space app", DisplayName = "Epic Test", InstallLocation = install, LaunchExecutable = "game.exe" }));
    File.WriteAllText(Path.Combine(epic, "broken.item"), "{broken");
    var scan = Discovery.Scan([steam, steam], epic);
    Check(scan.Games.Count == 3 && scan.Games.Select(g => g.Id).Distinct().Count() == 3, "Steam multi-library, Epic detection and deduplication");
    Check(scan.Games.All(g => g.Id != "steam:99"), "skip uninstalled Steam game");
    Check(scan.Games.Single(g => g.Source == "Epic").Target.Contains("space%20app"), "escape Epic launch URI");
    Check(scan.Warnings.Count == 1, "malformed manifest does not hide valid games");
    var favorite = scan.Games[0] with { IsFavorite = true, LastPlayed = DateTimeOffset.UtcNow };
    var manual = new GameEntry { Id = "manual:one", Name = "Custom", Target = "custom.exe" };
    var merged = LibraryMerge.Merge([favorite, manual], scan.Games);
    Check(merged.Single(g => g.Id == favorite.Id).IsFavorite && merged.Single(g => g.Id == favorite.Id).LastPlayed is not null && merged.Any(g => g.Id == manual.Id), "rescan retains favorites, recent and manual entries");
    Check(LibraryMerge.Merge([favorite], [], true).Count == 1, "partial scan retains previous metadata");
    var store = new StateStore(Path.Combine(root, "state", "library.json"));
    store.Save(new() { Games = merged, Mode = "Dock", Topmost = true });
    Check(store.Load() is { Mode: "Dock", Topmost: true } loaded && loaded.Games.Count == 4, "state round-trip");
    string badFile = Path.Combine(root, "bad.json"); File.WriteAllText(badFile, "{broken");
    var badStore = new StateStore(badFile);
    Check(badStore.Load().Games.Count == 0 && badStore.LastLoadWarning is not null && File.ReadAllText(badFile) == "{broken", "corrupt state retained and warning returned");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { Discovery.Scan([steam], epic, cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { passed++; Console.WriteLine("PASS: scan cancellation"); }
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
var plan = SessionPackage.Plan(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "session-target"), SessionPackage.Required);
Check(plan.Count == SessionPackage.Required.Length && plan.All(p => Path.IsPathFullyQualified(p.Destination)), "complete session package has bounded copy plan");
foreach (string invalid in new[] { "../escape.exe", @"..\escape.exe", "C:/escape.exe", "/escape.exe", "data:stream" })
{
    try { SessionPackage.Plan(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "session-target"), SessionPackage.Required.Append(invalid)); throw new Exception("Unsafe path accepted"); }
    catch (IOException) { }
}
Check(true, "session package rejects traversal, absolute paths and alternate data streams");
try { SessionPackage.Plan(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "session-target"), ["GameHub.exe"]); throw new Exception("Incomplete package accepted"); }
catch (IOException) { passed++; Console.WriteLine("PASS: incomplete session package cannot activate shell"); }
// Network behavior is tested with deterministic HTTP fixtures; no API key required.
Check(OnlineArtwork.ExactMatch("Hades", [new(1, "Hades II"), new(2, "Hades")])?.AppId == 2, "artwork exact title beats similar sequel");
Check(OnlineArtwork.ExactMatch("Hades", [new(1, "Hades"), new(2, "HADES")]) is null, "ambiguous artwork needs user choice");
string artRoot = Path.Combine(Path.GetTempPath(), "gamehub-art-" + Guid.NewGuid().ToString("N"));
try
{
    int calls = 0;
    using var http = new HttpClient(new FakeHandler(request => {
        calls++;
        if (request.RequestUri!.Host == "store.steampowered.com")
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"items\":[{\"id\":42,\"name\":\"Test Game\"}]}") };
        if (request.RequestUri.AbsolutePath.Contains("library_")) return new(HttpStatusCode.NotFound);
        return new(HttpStatusCode.OK) { Content = new ByteArrayContent([255, 216, 255, 224, 0, 0]) };
    }));
    var provider = new OnlineArtwork(http, artRoot);
    var matches = await provider.SearchAsync("Test Game");
    await provider.SearchAsync("Test Game");
    Check(matches.Single().AppId == 42 && calls == 1, "online title search and local search cache");
    var files = await provider.DownloadAsync(42);
    Check(files.Cover is not null && files.Cover == files.Hero && File.Exists(files.Cover), "missing poster/hero falls back to official header");
    int before = calls; await provider.DownloadAsync(42);
    Check(calls - before == 2, "cached header is reused; unavailable poster and hero retried");
    using var invalid = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("<html>error</html>") }));
    var invalidProvider = new OnlineArtwork(invalid, Path.Combine(artRoot, "invalid"));
    var invalidFiles = await invalidProvider.DownloadAsync(99);
    Check(invalidFiles.Cover is null && invalidFiles.Hero is null, "HTML response is never cached as artwork");
    using var huge = new HttpClient(new FakeHandler(_ => {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("x") };
        response.Content.Headers.ContentLength = 50 * 1024 * 1024;
        return response;
    }));
    try { await new OnlineArtwork(huge, Path.Combine(artRoot,"huge")).DownloadAsync(44); throw new Exception("Oversized image accepted"); }
    catch (IOException) { passed++; Console.WriteLine("PASS: bounded artwork download"); }
    if (args.Contains("--online-smoke"))
    {
        using var real = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var online = new OnlineArtwork(real, Path.Combine(artRoot, "real"));
        var results = await online.SearchAsync("Hades");
        Check(OnlineArtwork.ExactMatch("Hades", results)?.AppId == 1145360, "live Steam title lookup");
        var images = await online.DownloadAsync(1145360);
        Check(File.Exists(images.Cover) && File.Exists(images.Hero), "live official Steam artwork download");
    }
}
finally { if (Directory.Exists(artRoot)) Directory.Delete(artRoot, true); }
Console.WriteLine($"All {passed} checks passed.");

sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}
