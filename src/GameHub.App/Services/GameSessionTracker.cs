using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GameHub.Core;
using Microsoft.Win32;

namespace GameHub.App.Services;

// Protocol launchers often return no process handle. Track the game's actual
// foreground process inside its install directory, never the Steam/Epic client.
public sealed class GameSessionTracker
{
    private string? directory;
    private int? processId;
    private long armedAt;
    private string? steamId;
    private bool steamStarted;
    public void Arm(GameEntry game)
    {
        directory = Directory.Exists(game.WorkingDirectory) ? Path.GetFullPath(game.WorkingDirectory!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar : null;
        processId = null; armedAt = Environment.TickCount64;
        steamId = game.Id.StartsWith("steam:") && game.Id[6..].All(char.IsAsciiDigit) ? game.Id[6..] : null;
        steamStarted = false;
    }
    public bool GameExited()
    {
        if (steamId is not null)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps\" + steamId);
                if (key?.GetValue("Running") is int running)
                {
                    if (running != 0) { steamStarted = true; return false; }
                    if (steamStarted) { steamId = null; directory = null; processId = null; return true; }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        if (directory is null) return false;
        if (processId is int id)
        {
            try { using var process = Process.GetProcessById(id); if (!process.HasExited) return false; }
            catch (ArgumentException) { }
            directory = null; processId = null; return true;
        }
        if (Environment.TickCount64 - armedAt > 180000) { directory = null; return false; }
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
        if (foreground == 0 || foreground == Environment.ProcessId) return false;
        try
        {
            using var process = Process.GetProcessById((int)foreground);
            string? executable = process.MainModule?.FileName;
            if (executable?.StartsWith(directory, StringComparison.OrdinalIgnoreCase) == true) processId = process.Id;
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        return false;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
}
