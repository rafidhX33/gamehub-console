namespace GameHub.Core;

public sealed record PackageCopy(string Source, string Destination);

public static class SessionPackage
{
    public static readonly string[] Required = ["GameHub.exe", "GameHub.dll", "GameHub.Core.dll", "GameHub.runtimeconfig.json", "GameHub.deps.json", "GameHub.Session.exe", "GameHub.Session.dll", "GameHub.Session.runtimeconfig.json", "GameHub.Session.deps.json", "scripts/Restore-WindowsDesktop.ps1"];

    // Pure validation is shared by Windows setup and cross-platform regression checks.
    public static IReadOnlyList<PackageCopy> Plan(string source, string destination, IEnumerable<string> manifest)
    {
        var files = manifest.Select(f => f?.Replace('\\', '/') ?? "").ToArray();
        var included = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Required.Any(f => !included.Contains(f))) throw new IOException("Incomplete portable package; shell was not changed.");
        string sourceRoot = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string destinationRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = new List<PackageCopy>();
        foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(file) || file.StartsWith('/') || file.Contains(':') || file.Split('/').Any(p => p is "." or ".." or ""))
                throw new IOException("Invalid portable package path.");
            string relative = file.Replace('/', Path.DirectorySeparatorChar);
            string from = Path.GetFullPath(Path.Combine(sourceRoot, relative));
            string to = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!from.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase) || !to.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Package path escapes destination.");
            result.Add(new(from, to));
        }
        return result;
    }
}
