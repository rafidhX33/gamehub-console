using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GameHub.Core;
using GameHub.App.Services;

namespace GameHub.App;

public partial class MainWindow
{
    private readonly HttpClient artworkHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
    private OnlineArtwork? artwork;
    private bool fetchingArtwork;
    private readonly List<(string Label, Action Action)> choices = [];
    private readonly List<Button> choiceButtons = [];
    private int choiceIndex;
    private bool virtualKeyboard;
    private string keyboardText = "";
    private Action<string>? keyboardDone;
    private Action? menuBack;
    private bool desktopPinned;
    private readonly GameSessionTracker sessionTracker = new();
    private bool pendingLaunch;
    private bool gameTookFocus;
    private bool escapeDesktop;
    private bool InOverlay => OverlayHost.Visibility == Visibility.Visible;

    private void ShowMenu(string title, string description, IEnumerable<(string Label, Action Action)> actions, Action? back = null)
    {
        virtualKeyboard = false; menuBack = back;
        choices.Clear(); choices.AddRange(actions);
        choiceButtons.Clear(); OverlayItems.Children.Clear(); choiceIndex = 0;
        OverlayTitle.Text = title; OverlayDescription.Text = description;
        OverlayHint.Text = "D-pad / Stick: pilih    A: konfirmasi    B: kembali";
        foreach (var item in choices)
        {
            int index = choiceButtons.Count;
            var button = new Button { Content = item.Label, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 16, Padding = new Thickness(20, 12, 20, 12) };
            button.Click += (_, _) => { choiceIndex = index; ExecuteChoice(); };
            choiceButtons.Add(button); OverlayItems.Children.Add(button);
        }
        OverlayHost.Visibility = Visibility.Visible;
        HighlightChoice();
    }
    private void HighlightChoice()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
            choiceButtons[i].Background = new SolidColorBrush(i == choiceIndex ? Color.FromRgb(127, 60, 84) : Color.FromRgb(38, 49, 67));
        if (choiceButtons.Count > 0) { choiceButtons[choiceIndex].Focus(); choiceButtons[choiceIndex].BringIntoView(); }
    }
    private void ExecuteChoice()
    {
        if (choiceIndex < 0 || choiceIndex >= choices.Count) return;
        var action = choices[choiceIndex].Action;
        if (!virtualKeyboard) HideOverlay();
        action();
    }
    private void HideOverlay() { OverlayHost.Visibility = Visibility.Collapsed; virtualKeyboard = false; ConsoleList.Focus(); }
    private void BackOverlay() { var back = menuBack; HideOverlay(); back?.Invoke(); }
    private void OverlayInput(string input)
    {
        if (input == "Back") { BackOverlay(); return; }
        if (input == "Play") { ExecuteChoice(); return; }
        if (virtualKeyboard && input == "Favorite") { EditKeyboard("BACK"); return; }
        if (virtualKeyboard && input is "Search" or "Menu") { FinishKeyboard(); return; }
        int delta = input is "Left" or "Up" ? -1 : input is "Right" or "Down" ? 1 : 0;
        if (virtualKeyboard && input == "Up") delta = -10;
        if (virtualKeyboard && input == "Down") delta = 10;
        if (delta != 0 && choices.Count > 0) { choiceIndex = (choiceIndex + delta + choices.Count) % choices.Count; HighlightChoice(); }
    }
    private void OnConsoleOptions(object sender, RoutedEventArgs e) => ShowGameOptions();
    private void OnContinue(object sender, RoutedEventArgs e) => ContinueGame();
    private void ContinueGame() { if (MostRecent is GameEntry game) { filter = "Library"; SearchBox.Clear(); Refresh(); Selected = Games.FirstOrDefault(g => g.Id == game.Id); Play(); } }
    private void ShowGameOptions()
    {
        if (Selected is not GameEntry game) { ShowNotice("GAME", "Pilih game terlebih dahulu."); return; }
        var actions = new List<(string, Action)> {
            ("Play " + game.Name, Play),
            (game.FavoriteLabel, Favorite),
            ("Find / change online artwork", () => { _ = FindArtworkAsync(); }),
            ("Search library", () => ShowKeyboard("SEARCH LIBRARY", SearchBox.Text, text => { SearchBox.Text = text; Refresh(); }))
        };
        if (game.Source == "Manual") actions.Add(("Remove from library", () => {
            ShowMenu("REMOVE GAME", "Hanya menghapus dari library; file game tidak dihapus.", new (string, Action)[] {
                ("Confirm remove", () => { state = state with { Games = state.Games.Where(g => g.Id != game.Id).ToList() }; Refresh(); Save(); }),
                ("Cancel", () => { }) });
        }));
        ShowMenu(game.Name, game.Source + " · game options", actions);
    }
    private void ShowSystemMenu()
    {
        ShowMenu("GAME HUB / SYSTEM", "Antarmuka utama untuk sesi bermain. B kembali ke library.", new (string, Action)[] {
            ("Return to games", () => { }),
            ("Continue last played game", ContinueGame),
            ("Search library", () => ShowKeyboard("SEARCH LIBRARY", SearchBox.Text, text => { SearchBox.Text = text; Refresh(); })),
            ("Scan installed games", () => { _ = ScanAsync(); }),
            ("Add game / shortcut", () => BrowseGames(null)),
            ("Find online cover for selected game", () => { _ = FindArtworkAsync(); }),
            ("Refresh missing online artwork", () => { _ = HydrateArtworkAsync(); }),
            (state.OnlineArtworkEnabled ? "Online artwork: ON → turn off" : "Online artwork: OFF → turn on", () => { state = state with { OnlineArtworkEnabled = !state.OnlineArtworkEnabled }; Save(); ShowSystemMenu(); }),
            ("Startup / shell information", ShowSessionOptions),
            ("Switch to desktop window mode", () => SetMode("Desktop")),
            ("Switch to compact dock", () => SetMode("Dock")),
            ("Exit to Windows desktop", ExitToDesktop)
        });
    }
    private void ShowSessionOptions()
    {
        ShowMenu("SESSION", SessionService.IsShellSession ? "Custom shell session aktif. Exit to desktop memulai Explorer; pengaturan shell tetap berlaku pada login berikutnya." : "True shell replacement dapat diaktifkan di menu ini pada edisi Windows yang mendukung Custom User Interface. Startup biasa tidak mengganti Explorer.", new (string, Action)[] {
            ("Enable GameHub as the Windows user shell…", () => ShowMenu("ENABLE CONSOLE SHELL", "Eksperimental: uji pada akun terpisah terlebih dahulu. Pada login berikutnya, GameHub menggantikan Explorer untuk akun ini. Windows Pro/Enterprise/Education diperlukan. Paket disalin ke LocalAppData. Gunakan Exit to Windows atau Restore Windows untuk kembali.", new (string, Action)[] {
                ("Confirm: use GameHub on next login", () => { _ = EnableShellFromMenuAsync(); }), ("Cancel", ShowSessionOptions)
            }, ShowSessionOptions)),
            (WindowsServices.StartupEnabled ? "Disable console at login (regular startup)" : "Enable console at login (regular startup)", () => { try { WindowsServices.SetStartup(!WindowsServices.StartupEnabled); ShowSessionOptions(); } catch (Exception ex) { ShowNotice("STARTUP", ex.Message); } }),
            (desktopPinned ? "Always on top: ON" : "Always on top: OFF", () => { desktopPinned = !desktopPinned; if (mode != "Console") Topmost = desktopPinned; Save(); ShowSessionOptions(); }),
            ("Disable GameHub custom shell and open Windows", () => ShowMenu("RESTORE WINDOWS", "Kembali ke Explorer pada login berikutnya. Hanya policy shell milik GameHub yang dihapus.", new (string, Action)[] { ("Confirm restore Windows", () => { try { SessionService.DisableShell(); escapeDesktop = true; Close(); } catch (Exception ex) { ShowNotice("RESTORE", ex.Message); } }), ("Cancel", ShowSystemMenu) }, ShowSystemMenu)),
            ("Back", ShowSystemMenu)
        }, ShowSystemMenu);
    }
    private async Task EnableShellFromMenuAsync()
    {
        try { SetStatus("Preparing console session…"); await SessionService.EnableShellAsync(lifetime.Token); if (!closing) ShowNotice("CONSOLE SHELL READY", "Berlaku pada login berikutnya. Tidak perlu mengganti bootloader atau menghapus Windows. Exit to Windows tetap tersedia. Untuk membatalkan permanen, pilih Session > Disable GameHub custom shell."); }
        catch (Exception ex) { if (!closing) ShowNotice("SHELL SETUP", ex.Message); }
    }
    private void ExitToDesktop()
    {
        try { SessionService.StartDesktop(); escapeDesktop = true; Close(); }
        catch (Exception ex) { ShowNotice("DESKTOP", ex.Message); }
    }
    private void ShowNotice(string title, string message) => ShowMenu(title, message, new (string, Action)[] { ("Back to games", () => { }) });
    private void OnConsoleSearch(object sender, RoutedEventArgs e) => ShowKeyboard("SEARCH LIBRARY", SearchBox.Text, text => { SearchBox.Text = text; Refresh(); });
    private void ShowKeyboard(string title, string initial, Action<string> done)
    {
        ShowMenu(title, initial, [], ShowSystemMenu);
        virtualKeyboard = true; keyboardText = initial; keyboardDone = done;
        var keys = new UniformGrid { Columns = 10 };
        string[] symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Select(c => c.ToString()).Concat(["SPACE", "BACK", "CLEAR", "DONE"]).ToArray();
        foreach (string symbol in symbols)
        {
            int index = choiceButtons.Count;
            choices.Add((symbol, () => EditKeyboard(symbol)));
            var button = new Button { Content = symbol, MinHeight = 48, Padding = new Thickness(5), FontSize = symbol.Length > 1 ? 11 : 18 };
            button.Click += (_, _) => { choiceIndex = index; ExecuteChoice(); };
            choiceButtons.Add(button); keys.Children.Add(button);
        }
        OverlayItems.Children.Add(keys); OverlayHint.Text = "D-pad: pilih huruf   A: ketik   X: hapus   Y / Start: selesai   B: batal";
        HighlightChoice();
    }
    private void EditKeyboard(string key)
    {
        if (key == "DONE") { FinishKeyboard(); return; }
        keyboardText = key switch { "BACK" => keyboardText.Length > 0 ? keyboardText[..^1] : "", "CLEAR" => "", "SPACE" => keyboardText + " ", _ => keyboardText.Length < 100 ? keyboardText + key : keyboardText };
        OverlayDescription.Text = keyboardText.Length == 0 ? "_" : keyboardText;
    }
    private void FinishKeyboard() { string text = keyboardText; var done = keyboardDone; HideOverlay(); done?.Invoke(text); }
    private void BrowseGames(string? folder)
    {
        var actions = new List<(string, Action)>();
        try
        {
            if (folder is null)
            {
                foreach (var path in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) }.Where(Directory.Exists))
                { string item = path; actions.Add((item, () => BrowseGames(item))); }
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                { string item = drive.RootDirectory.FullName; actions.Add(("Drive " + item, () => BrowseGames(item))); }
            }
            else
            {
                string? parent = Directory.GetParent(folder)?.FullName;
                actions.Add(("↑ Parent / Drives", () => BrowseGames(parent)));
                foreach (string directory in Directory.EnumerateDirectories(folder).OrderBy(p => p).Take(300))
                { string item = directory; actions.Add(("▸ " + Path.GetFileName(item), () => BrowseGames(item))); }
                foreach (string file in Directory.EnumerateFiles(folder).Where(p => new[] { ".exe", ".lnk", ".url" }.Contains(Path.GetExtension(p).ToLowerInvariant())).OrderBy(p => p).Take(300))
                { string item = file; actions.Add(("＋ " + Path.GetFileName(item), () => AddManualGame(item))); }
            }
            ShowMenu("ADD GAME", folder ?? "Pilih folder atau drive memakai controller.", actions, ShowSystemMenu);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowNotice("FOLDER", ex.Message); }
    }
    private void AddManualGame(string path)
    {
        var game = WindowsServices.ManualGame(path);
        if (!state.Games.Any(g => g.Id == game.Id)) state = state with { Games = state.Games.Append(game).ToList() };
        filter = "Library"; SearchBox.Clear(); Refresh(); Selected = Games.FirstOrDefault(g => g.Id == game.Id); Save();
        _ = FindArtworkAsync();
    }
    private async Task FindArtworkAsync()
    {
        if (Selected is not GameEntry game) { ShowNotice("ARTWORK", "Pilih game terlebih dahulu."); return; }
        if (!state.OnlineArtworkEnabled) { ShowNotice("ARTWORK", "Online artwork sedang dimatikan. Aktifkan lewat System menu."); return; }
        try
        {
            artwork ??= new(artworkHttp, Path.Combine(Path.GetDirectoryName(WindowsServices.StatePath)!, "artwork"));
            if (game.Id.StartsWith("steam:") && int.TryParse(game.Id[6..], out int id)) { await ApplyArtworkAsync(game, id); return; }
            SetStatus("Searching cover: " + game.Name);
            var matches = await artwork.SearchAsync(game.Name, lifetime.Token);
            if (closing) return;
            var actions = matches.Select(match => (match.Name, (Action)(() => { _ = ApplyArtworkAsync(game, match.AppId); }))).ToList();
            actions.Add(("Search another title…", () => ShowKeyboard("ARTWORK TITLE", game.Name, text => { _ = SearchArtworkTitleAsync(game, text); })));
            ShowMenu("CHOOSE GAME ARTWORK", "Pilih judul yang sesuai; A mengunduh cover dan banner. Target peluncuran game tetap sama.", actions, ShowSystemMenu);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException) { if (!closing) ShowNotice("ARTWORK OFFLINE", "Library tetap bisa dimainkan. " + ex.Message); }
    }
    private async Task SearchArtworkTitleAsync(GameEntry game, string title)
    {
        try
        {
            var matches = await artwork!.SearchAsync(title, lifetime.Token);
            if (closing) return;
            var actions = matches.Select(match => (match.Name, (Action)(() => { _ = ApplyArtworkAsync(game, match.AppId); }))).ToList();
            actions.Add(("Try another title", () => ShowKeyboard("ARTWORK TITLE", title, text => { _ = SearchArtworkTitleAsync(game, text); })));
            ShowMenu("ARTWORK RESULTS", matches.Count == 0 ? "Tidak ditemukan. Coba judul lain." : "Pilih judul yang benar.", actions, ShowSystemMenu);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing) ShowNotice("ARTWORK", ex.Message); }
    }
    private async Task ApplyArtworkAsync(GameEntry game, int appId)
    {
        try
        {
            artwork ??= new(artworkHttp, Path.Combine(Path.GetDirectoryName(WindowsServices.StatePath)!, "artwork"));
            var images = await artwork.DownloadAsync(appId, lifetime.Token);
            if (closing) return;
            // Apply to current metadata, not a stale pre-download snapshot.
            var current = state.Games.FirstOrDefault(g => g.Id == game.Id);
            if (current is null) return;
            if (images.Cover is null && images.Hero is null) { SetStatus("Artwork belum tersedia untuk " + game.Name); return; }
            Replace(current with { ArtworkAppId = appId, CoverPath = images.Cover ?? current.CoverPath, HeroPath = images.Hero ?? current.HeroPath });
            SetStatus("Artwork ready: " + game.Name);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException) { if (!closing) SetStatus("Artwork offline: " + ex.Message); }
    }
    private async Task HydrateArtworkAsync()
    {
        if (fetchingArtwork || !state.OnlineArtworkEnabled || closing) return;
        fetchingArtwork = true;
        try
        {
            artwork ??= new(artworkHttp, Path.Combine(Path.GetDirectoryName(WindowsServices.StatePath)!, "artwork"));
            foreach (var game in state.Games.Where(g => !File.Exists(g.CoverPath) || !File.Exists(g.HeroPath)).Take(24).ToArray())
            {
                if (closing) break;
                int? id = game.ArtworkAppId;
                if (id is null && game.Id.StartsWith("steam:") && int.TryParse(game.Id[6..], out int steamId)) id = steamId;
                if (id is null)
                {
                    var matches = await artwork.SearchAsync(game.Name, lifetime.Token);
                    id = OnlineArtwork.ExactMatch(game.Name, matches)?.AppId;
                }
                if (id is int appId) await ApplyArtworkAsync(game, appId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException) { if (!closing) SetStatus("Offline artwork · cached library remains available"); }
        finally { fetchingArtwork = false; }
    }
    private void SetStatus(string text) { StatusText.Text = text; ConsoleStatus.Text = text; }
    private void OnConsoleActivated(object? sender, EventArgs e)
    {
        if (mode == "Console") Topmost = true;
        if (pendingLaunch && gameTookFocus) { pendingLaunch = false; gameTookFocus = false; SetStatus("Welcome back · choose your next game"); ConsoleList.Focus(); }
    }
    private void OnConsoleDeactivated(object? sender, EventArgs e)
    { if (mode == "Console") Topmost = false; if (pendingLaunch) gameTookFocus = true; }
}
