using System.Text.Json;

namespace GameHub.Core;

public sealed record ScanResult(List<GameEntry> Games, List<string> Warnings);

public static class Discovery
{
    public static Task<ScanResult> ScanAsync(IEnumerable<string> steamRoots, string? epicManifests, CancellationToken token = default)
        => Task.Run(() => Scan(steamRoots, epicManifests, token), token);

    public static ScanResult Scan(IEnumerable<string> steamRoots, string? epicManifests, CancellationToken token = default)
    {
        var games = new List<GameEntry>();
        var warnings = new List<string>();
        var installations = steamRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string steam in installations)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
            Try(() => {
                string file = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(file))
                    foreach (var node in Vdf.Parse(ReadSmall(file)).Descendants().Where(n => n.Name == "path" && n.Value is not null))
                        libraries.Add(node.Value!);
            }, "Steam libraries");
            foreach (string root in libraries)
            {
                token.ThrowIfCancellationRequested();
                Try(() => {
                    string apps = Path.Combine(root, "steamapps");
                    if (!Directory.Exists(apps)) return;
                    foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                    {
                        token.ThrowIfCancellationRequested();
                        Try(() => {
                            var app = Vdf.Parse(ReadSmall(manifest)).Children.FirstOrDefault();
                            string? id = app?.Get("appid"), name = app?.Get("name"), install = app?.Get("installdir");
                            if (id is null || !id.All(char.IsAsciiDigit) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(install)) return;
                            if (name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase) || name.Contains("Steamworks", StringComparison.OrdinalIgnoreCase)) return;
                            string directory = Path.Combine(apps, "common", install);
                            if (!Directory.Exists(directory)) return;
                            string? cover = Artwork(steam, id, "library_600x900");
                            string? hero = Artwork(steam, id, "library_hero") ?? Artwork(steam, id, "header") ?? cover;
                            games.Add(new() { Id = "steam:" + id, Name = name, Target = "steam://rungameid/" + id, Source = "Steam", WorkingDirectory = directory, CoverPath = cover, HeroPath = hero });
                        }, Path.GetFileName(manifest));
                    }
                }, "Steam manifest folder");
            }
        }
        if (!string.IsNullOrWhiteSpace(epicManifests) && Directory.Exists(epicManifests))
            Try(() => {
                foreach (string file in Directory.EnumerateFiles(epicManifests, "*.item"))
                {
                    token.ThrowIfCancellationRequested();
                    Try(() => {
                        using var json = JsonDocument.Parse(ReadSmall(file));
                        var item = json.RootElement;
                        string Get(string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
                        string app = Get("AppName"), name = Get("DisplayName"), install = Get("InstallLocation"), exe = Get("LaunchExecutable");
                        if (string.IsNullOrWhiteSpace(app) || string.IsNullOrWhiteSpace(name) || !Directory.Exists(install) || string.IsNullOrWhiteSpace(exe)) return;
                        if (!File.Exists(Path.Combine(install, exe))) return;
                        if (item.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return;
                        games.Add(new() { Id = "epic:" + app, Name = name, Source = "Epic", Target = "com.epicgames.launcher://apps/" + Uri.EscapeDataString(app) + "?action=launch&silent=true", WorkingDirectory = install });
                    }, Path.GetFileName(file));
                }
            }, "Epic manifest folder");
        return new(games.DistinctBy(g => g.Id).ToList(), warnings);

        void Try(Action action, string area)
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException or ArgumentException)
            { warnings.Add(area + ": " + ex.Message); }
        }
    }

    private static string ReadSmall(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException("Manifest exceeds 4 MB.");
        return File.ReadAllText(path);
    }

    private static string? Artwork(string steam, string id, string kind)
    {
        string cache = Path.Combine(steam, "appcache", "librarycache");
        foreach (string extension in new[] { ".jpg", ".png", ".jpeg" })
        {
            string path = Path.Combine(cache, id + "_" + kind + extension);
            if (File.Exists(path)) return path;
        }
        string folder = Path.Combine(cache, id);
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder).FirstOrDefault(path =>
            Path.GetFileName(path).Contains(kind, StringComparison.OrdinalIgnoreCase) &&
            new[] { ".jpg", ".png", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()));
    }
}
