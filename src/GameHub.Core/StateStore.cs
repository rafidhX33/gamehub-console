using System.Text.Json;

namespace GameHub.Core;

public sealed class StateStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string? LastLoadWarning { get; private set; }

    public LibraryState Load()
    {
        LastLoadWarning = null;
        if (!File.Exists(path)) return new();
        try
        {
            var state = JsonSerializer.Deserialize<LibraryState>(File.ReadAllText(path), Options);
            if (state is null || state.Version != 1 || state.Games is null || state.Games.Any(g => g is null || string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Name) || string.IsNullOrWhiteSpace(g.Target)))
                throw new JsonException("Invalid or unsupported library file.");
            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LastLoadWarning = "Library could not be read; original file retained: " + ex.Message;
            return new();
        }
    }

    public void Save(LibraryState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options));
        File.Move(temporary, path, true);
    }
}
