namespace GameHub.Core;

public sealed record GameEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Target { get; init; }
    public string Source { get; init; } = "Manual";
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? CoverPath { get; init; }
    public string? HeroPath { get; init; }
    public bool IsFavorite { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }
    public string SourceLabel => Source.ToUpperInvariant();
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "G" : Name[..1].ToUpperInvariant();
    public string FavoriteLabel => IsFavorite ? "♥ Favorited" : "♡ Favorite";
}

public sealed record LibraryState
{
    public int Version { get; init; } = 1;
    public List<GameEntry> Games { get; init; } = [];
    public string Mode { get; init; } = "Desktop";
    public bool Topmost { get; init; }
}

public static class LibraryMerge
{
    public static List<GameEntry> Merge(IEnumerable<GameEntry> previous, IEnumerable<GameEntry> discovered, bool keepMissing = false)
    {
        var old = previous.DistinctBy(g => g.Id).ToDictionary(g => g.Id);
        var found = discovered.Concat(old.Values.Where(g => keepMissing || g.Source == "Manual"))
            .DistinctBy(g => g.Id);
        return found.Select(g => old.TryGetValue(g.Id, out var saved)
            ? g with { IsFavorite = saved.IsFavorite, LastPlayed = saved.LastPlayed, CoverPath = File.Exists(saved.CoverPath) ? saved.CoverPath : g.CoverPath, HeroPath = File.Exists(saved.HeroPath) ? saved.HeroPath : g.HeroPath }
            : g).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
