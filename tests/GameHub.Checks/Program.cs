using System.Text.Json;
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
Console.WriteLine($"All {passed} checks passed.");
