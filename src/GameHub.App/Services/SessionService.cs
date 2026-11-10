using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using GameHub.Core;

namespace GameHub.App.Services;

public static class SessionService
{
    public static bool IsShellSession => Environment.GetCommandLineArgs().Contains("--shell");
    private const string Policy = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    public static void StartDesktop()
    {
        // Explicit desktop escape; leave policy unchanged until uninstall is chosen.
        int session = Process.GetCurrentProcess().SessionId;
        if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true });
    }
    public static void DisableShell()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Policy, true);
        string? value = key?.GetValue("Shell") as string;
        using var marker = Registry.CurrentUser.OpenSubKey(@"Software\GameHubConsole");
        string? ours = marker?.GetValue("ShellCommand") as string;
        if (ours is not null && value == ours) key!.DeleteValue("Shell", false);
        StartDesktop();
    }
    public static async Task EnableShellAsync(CancellationToken token = default)
    {
        using var editionKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        string edition = editionKey?.GetValue("EditionID") as string ?? "";
        if (!new[] { "Professional", "Enterprise", "Education", "IoTEnterprise" }.Any(p => edition.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Custom User Interface tidak didokumentasikan untuk edisi Windows ini. Gunakan startup Console biasa pada Windows Home.");
        string source = AppContext.BaseDirectory;
        string destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameHub", "Session");
        string manifest = Path.Combine(source, "package.manifest.json");
        if (!File.Exists(manifest) || !File.Exists(Path.Combine(source, "GameHub.Session.exe")))
            throw new InvalidOperationException("Gunakan paket portable lengkap dari GitHub Actions, bukan dotnet run atau executable yang dipindah sendiri.");
        using var existingPolicy = Registry.CurrentUser.OpenSubKey(Policy);
        using var existingMarker = Registry.CurrentUser.OpenSubKey(@"Software\GameHubConsole");
        object? current = existingPolicy?.GetValue("Shell");
        string? owned = existingMarker?.GetValue("ShellCommand") as string;
        if (current is not null && (owned is null || !Equals(current, owned)))
            throw new InvalidOperationException("Sudah ada custom shell lain / policy administrator. GameHub tidak akan menimpanya.");
        string[] files = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(manifest, token)) ?? throw new IOException("Invalid package manifest.");
        var plan = SessionPackage.Plan(source, destination, files);
        await Task.Run(() =>
        {
            Directory.CreateDirectory(destination);
            foreach (var copy in plan)
            {
                token.ThrowIfCancellationRequested();
                if (copy.Source.Equals(copy.Destination, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(copy.Destination)!);
                File.Copy(copy.Source, copy.Destination, true);
            }
            string targetManifest = Path.Combine(destination, "package.manifest.json");
            if (!Path.GetFullPath(manifest).Equals(Path.GetFullPath(targetManifest), StringComparison.OrdinalIgnoreCase)) File.Copy(manifest, targetManifest, true);
        });
        token.ThrowIfCancellationRequested();
        string command = "\"" + Path.Combine(destination, "GameHub.Session.exe") + "\"";
        using var marker = Registry.CurrentUser.CreateSubKey(@"Software\GameHubConsole");
        marker.SetValue("ShellCommand", command);
        using var policy = Registry.CurrentUser.CreateSubKey(Policy);
        WindowsServices.SetStartup(false);
        token.ThrowIfCancellationRequested();
        policy.SetValue("Shell", command, RegistryValueKind.String);
    }
    public static void Fullscreen(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        var monitor = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor)) return;
        MatrixScale(window, monitor.Monitor);
    }
    private static void MatrixScale(Window window, NativeRect rect)
    {
        var source = PresentationSource.FromVisual(window);
        var matrix = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        Point a = matrix.Transform(new Point(rect.Left, rect.Top));
        Point b = matrix.Transform(new Point(rect.Right, rect.Bottom));
        window.Left = a.X; window.Top = a.Y;
        window.Width = b.X - a.X; window.Height = b.Y - a.Y;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
