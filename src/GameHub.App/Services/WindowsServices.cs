using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GameHub.Core;
using Microsoft.Win32;

namespace GameHub.App.Services;

public static class WindowsServices
{
    public static IEnumerable<string> SteamRoots()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is string path) yield return path;
        foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            if (!string.IsNullOrEmpty(folder)) yield return Path.Combine(folder, "Steam");
    }
    public static string EpicManifests => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
    public static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameHub", "library.json");
    public static GameEntry ManualGame(string path)
    {
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
        return new() { Id = "manual:" + id, Name = Path.GetFileNameWithoutExtension(path), Target = path, WorkingDirectory = Path.GetDirectoryName(path), Source = "Manual" };
    }
    public static void Launch(GameEntry game)
    {
        bool isProtocol = Uri.TryCreate(game.Target, UriKind.Absolute, out var uri) && uri.Scheme is "steam" or "com.epicgames.launcher";
        if (!isProtocol && !File.Exists(game.Target)) throw new FileNotFoundException("File game dipindahkan atau tidak ditemukan.", game.Target);
        Process.Start(new ProcessStartInfo { FileName = game.Target, Arguments = game.Arguments ?? "", WorkingDirectory = game.WorkingDirectory is string dir && Directory.Exists(dir) ? dir : "", UseShellExecute = true });
    }
    public static bool StartupEnabled => StartupValue() is not null;
    private static object? StartupValue() { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("GameHubConsole"); }
    public static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enable)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Application path unavailable.");
            if (!Path.GetFileName(executable).Equals("GameHub.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Aktifkan startup dari versi portable GameHub.exe.");
            key.SetValue("GameHubConsole", "\"" + executable + "\" --console");
        }
        else key.DeleteValue("GameHubConsole", false);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Memory { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
    [StructLayout(LayoutKind.Sequential)] private struct Power { public byte AC, BatteryFlag, Percent, SystemStatus; public uint Lifetime, FullLifetime; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref Memory status);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out Power status);
    public static (string Memory, string Power, string Storage) ReadSystem()
    {
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        string ram = GlobalMemoryStatusEx(ref memory) ? $"{(memory.TotalPhysical - memory.AvailablePhysical) / 1073741824d:F1} / {memory.TotalPhysical / 1073741824d:F0} GB RAM" : "RAM unavailable";
        string power = GetSystemPowerStatus(out var p) ? p.AC == 1 ? "Connected to power" : p.Percent <= 100 ? $"Battery {p.Percent}%" : "Battery unavailable" : "Power unavailable";
        string storage = "Storage unavailable";
        try { var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); storage = $"System drive · {drive.AvailableFreeSpace / 1073741824d:F0} GB free"; } catch (IOException) { }
        return (ram, power, storage);
    }
}
