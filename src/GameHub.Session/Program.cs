using System.Diagnostics;
using Microsoft.Win32;

namespace GameHub.Session;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "GameHub.exe");
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using var process = Process.Start(new ProcessStartInfo { FileName = executable, Arguments = "--shell", WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false });
                if (process is null) break;
                process.WaitForExit();
                if (process.ExitCode == 0) { StartDesktop(); return 0; }
            }
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { try { File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameHub", "session-error.txt"), ex.Message); } catch (IOException) { } }
        // Three failed starts: restore this application's own policy, then Explorer.
        try
        {
            using var marker = Registry.CurrentUser.OpenSubKey(@"Software\GameHubConsole");
            using var policy = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true);
            string? ours = marker?.GetValue("ShellCommand") as string;
            if (ours is not null && Equals(policy?.GetValue("Shell"), ours)) policy!.DeleteValue("Shell", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        StartDesktop(); return 1;
    }
    private static void StartDesktop()
    {
        int session = Process.GetCurrentProcess().SessionId;
        if (Process.GetProcessesByName("explorer").Any(p => p.SessionId == session)) return;
        try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
