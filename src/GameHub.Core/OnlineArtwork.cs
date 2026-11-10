using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameHub.Core;

public sealed record ArtworkMatch(int AppId, string Name);
public sealed record ArtworkFiles(string? Cover, string? Hero);

public sealed class OnlineArtwork(HttpClient client, string cacheDirectory)
{
    public static string Normalize(string title) => string.Concat(title.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static ArtworkMatch? ExactMatch(string title, IEnumerable<ArtworkMatch> matches)
    {
        var exact = matches.Where(m => Normalize(m.Name) == Normalize(title)).Take(2).ToArray();
        return exact.Length == 1 ? exact[0] : null;
    }
    public async Task<List<ArtworkMatch>> SearchAsync(string title, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return [];
        Directory.CreateDirectory(cacheDirectory);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(title.Trim().ToUpperInvariant())));
        string file = Path.Combine(cacheDirectory, "search-" + key + ".json");
        if (File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromDays(7))
            try { return JsonSerializer.Deserialize<List<ArtworkMatch>>(await File.ReadAllTextAsync(file, token)) ?? []; }
            catch (JsonException) { }
        string url = "https://store.steampowered.com/api/storesearch/?l=english&cc=us&term=" + Uri.EscapeDataString(title.Trim());
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        byte[] bytes = await ReadBounded(response, 1024 * 1024, token);
        using var document = JsonDocument.Parse(bytes);
        var result = new List<ArtworkMatch>();
        if (document.RootElement.TryGetProperty("items", out var items))
            foreach (var item in items.EnumerateArray().Take(20))
                if (item.TryGetProperty("id", out var id) && id.TryGetInt32(out int number) && number > 0 && item.TryGetProperty("name", out var name) && name.GetString() is string text)
                    result.Add(new(number, text));
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(result), token);
        return result;
    }
    public async Task<ArtworkFiles> DownloadAsync(int appId, CancellationToken token = default)
    {
        if (appId <= 0) throw new ArgumentOutOfRangeException(nameof(appId));
        string folder = Path.Combine(cacheDirectory, appId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        string? cover = await Fetch("library_600x900", folder, appId, token);
        string? hero = await Fetch("library_hero", folder, appId, token);
        if (cover is null || hero is null)
        {
            string? header = await Fetch("header", folder, appId, token);
            cover ??= header; hero ??= header ?? cover;
        }
        return new(cover, hero);
    }
    private async Task<string?> Fetch(string kind, string folder, int appId, CancellationToken token)
    {
        string path = Path.Combine(folder, kind + ".jpg");
        if (File.Exists(path) && new FileInfo(path).Length > 3) return path;
        string url = $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/{kind}.jpg";
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        byte[] bytes = await ReadBounded(response, 12 * 1024 * 1024, token);
        bool jpeg = bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        bool png = bytes.Length > 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (!jpeg && !png) return null;
        string temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, bytes, token);
        File.Move(temporary, path, true);
        return path;
    }
    private static async Task<byte[]> ReadBounded(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new IOException("Artwork response too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        byte[] block = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(block, token)) > 0)
        {
            if (buffer.Length + read > limit) throw new IOException("Artwork response too large.");
            buffer.Write(block, 0, read);
        }
        return buffer.ToArray();
    }
}
