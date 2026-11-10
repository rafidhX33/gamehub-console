using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GameHub.Core;
using GameHub.App.Services;
using Microsoft.Win32;

namespace GameHub.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<GameEntry> Games { get; } = [];
    private readonly StateStore store = new(WindowsServices.StatePath);
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(2) };
    private ControllerService? controller;
    private LibraryState state = new();
    private GameEntry? selected;
    private string filter = "Library";
    private string mode = "Desktop";
    private bool scanning;
    private bool loaded;
    private bool closing;
    private bool preserveUnreadableState;
    private Rect desktopBounds = new(80, 80, 1180, 760);
    public GameEntry? MostRecent => state.Games.Where(g => g.LastPlayed is not null).OrderByDescending(g => g.LastPlayed).FirstOrDefault();
    public event PropertyChangedEventHandler? PropertyChanged;
    public GameEntry? Selected
    {
        get => selected;
        set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }
    public MainWindow() { InitializeComponent(); DataContext = this; Activated += OnConsoleActivated; Deactivated += OnConsoleDeactivated; }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        state = store.Load();
        preserveUnreadableState = store.LastLoadWarning is not null;
        desktopPinned = state.Topmost; Topmost = desktopPinned;
        loaded = true;
        Refresh();
        clock.Tick += (_, _) => UpdateSystem(); clock.Start(); UpdateSystem();
        controller = new();
        controller.Input += ControllerInput;
        controller.ConnectionChanged += connected => ControllerText.Text = connected ? "Xbox controller ready" : "Keyboard ready";
        SetMode(Environment.GetCommandLineArgs().Contains("--desktop") ? "Desktop" : Environment.GetCommandLineArgs().Contains("--dock") ? "Dock" : "Console");
        await ScanAsync();
        if (preserveUnreadableState) ShowNotice("LIBRARY RECOVERY", "File library lama tidak dapat dibaca dan tidak ditimpa. Pulihkan %LOCALAPPDATA%\\GameHub\\library.json sebelum menyimpan lagi.");
    }
    private void UpdateSystem()
    {
        var system = WindowsServices.ReadSystem();
        MemoryText.Text = system.Memory; PowerText.Text = system.Power; StorageText.Text = system.Storage;
        ClockText.Text = DateTime.Now.ToString("HH:mm");
        ConsoleClock.Text = ClockText.Text; ConsolePower.Text = system.Power.Replace("Connected to power", "AC");
        if (sessionTracker.GameExited()) { pendingLaunch = false; gameTookFocus = false; if (mode == "Console") { WindowState = WindowState.Normal; SessionService.Fullscreen(this); Activate(); ConsoleList.Focus(); SetStatus("Welcome back"); } }
    }
    private void Refresh()
    {
        if (!loaded) return;
        string? id = Selected?.Id;
        IEnumerable<GameEntry> list = state.Games;
        if (filter == "Favorites") list = list.Where(g => g.IsFavorite);
        if (filter == "Recent") list = list.Where(g => g.LastPlayed is not null).OrderByDescending(g => g.LastPlayed);
        string text = SearchBox.Text.Trim();
        if (text.Length > 0) list = list.Where(g => g.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || g.Source.Contains(text, StringComparison.OrdinalIgnoreCase));
        var items = list.ToList();
        Games.Clear(); foreach (var game in items) Games.Add(game);
        Selected = Games.FirstOrDefault(g => g.Id == id) ?? Games.FirstOrDefault();
        CountText.Text = $"{state.Games.Count} games installed";
        SectionTitle.Text = $"{filter} / {Games.Count}";
        PropertyChanged?.Invoke(this, new(nameof(MostRecent)));
        ConsoleSection.Text = filter.ToUpperInvariant(); ConsoleCount.Text = $"{filter} / {Games.Count}";
        EmptyText.Visibility = Games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlayButton.IsEnabled = Selected is not null;
        Brush active = new SolidColorBrush(Color.FromRgb(115, 54, 74));
        Brush normal = new SolidColorBrush(Color.FromRgb(38, 49, 67));
        LibraryTab.Background = filter == "Library" ? active : normal;
        FavoriteTab.Background = filter == "Favorites" ? active : normal;
        RecentTab.Background = filter == "Recent" ? active : normal;
        foreach (var button in new[] { ConsoleLibraryTab, ConsoleFavoriteTab, ConsoleRecentTab })
        {
            bool current = (string)button.Tag == filter;
            button.Background = new SolidColorBrush(current ? Color.FromRgb(215,207,230) : Color.FromArgb(35,26,38,56));
            button.Foreground = new SolidColorBrush(current ? Color.FromRgb(25,26,48) : Color.FromRgb(235,230,245));
        }
    }
    private async Task ScanAsync()
    {
        if (scanning || closing) return;
        scanning = true; ScanButton.IsEnabled = false; StatusText.Text = "Scanning Steam and Epic libraries…";
        try
        {
            var result = await Discovery.ScanAsync(WindowsServices.SteamRoots().ToArray(), WindowsServices.EpicManifests, lifetime.Token);
            if (closing) return;
            state = state with { Games = LibraryMerge.Merge(state.Games, result.Games, result.Warnings.Count > 0) };
            Refresh(); Save();
            StatusText.Text = preserveUnreadableState ? "Library asli belum dipulihkan; perubahan tidak disimpan." : result.Warnings.Count == 0 ? $"{state.Games.Count} games ready · A / Enter to play" : $"Scan selesai · {result.Warnings.Count} folder/manifest tidak terbaca";
            if (result.Warnings.Count > 0) Debug.WriteLine(string.Join(Environment.NewLine, result.Warnings));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing) StatusText.Text = "Scan failed: " + ex.Message; }
        finally { scanning = false; if (!closing) ScanButton.IsEnabled = true; }
        if (!closing) { ConsoleStatus.Text = StatusText.Text; await HydrateArtworkAsync(); }
    }
    private void Save()
    {
        if (preserveUnreadableState) return;
        try { store.Save(state with { Mode = mode, Topmost = desktopPinned }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = "Tidak bisa menyimpan: " + ex.Message; }
    }
    private void Play()
    {
        if (Selected is not GameEntry game) return;
        try
        {
            Topmost = false; pendingLaunch = true; gameTookFocus = false;
            WindowsServices.Launch(game);
            sessionTracker.Arm(game);
            Replace(game with { LastPlayed = DateTimeOffset.UtcNow });
            SetStatus("Opening " + game.Name);
        }
        catch (Exception ex) { pendingLaunch = false; if (mode == "Console") Topmost = true; ShowNotice("Tidak bisa membuka game", ex.Message); }
    }
    private void Replace(GameEntry game)
    {
        state = state with { Games = state.Games.Select(g => g.Id == game.Id ? game : g).ToList() };
        Refresh(); Save();
    }
    private void Favorite() { if (Selected is GameEntry game) Replace(game with { IsFavorite = !game.IsFavorite }); }
    private void MoveSelection(int delta)
    {
        if (Games.Count == 0) return;
        int i = Selected is null ? 0 : Games.IndexOf(Selected);
        Selected = Games[(i + delta + Games.Count) % Games.Count];
        GameList.ScrollIntoView(Selected); DockList.ScrollIntoView(Selected); ConsoleList.ScrollIntoView(Selected);
    }
    private void SetMode(string next)
    {
        if (next is not ("Desktop" or "Console" or "Dock")) next = "Desktop";
        if (mode == "Desktop" && WindowState == WindowState.Normal && IsVisible)
            desktopBounds = new Rect(Left, Top, Width, Height);
        WindowState = WindowState.Normal;
        mode = next;
        HideOverlay();
        MainPanel.Visibility = next == "Desktop" ? Visibility.Visible : Visibility.Collapsed;
        ConsolePanel.Visibility = next == "Console" ? Visibility.Visible : Visibility.Collapsed;
        Shell.Visibility = next == "Console" ? Visibility.Collapsed : Visibility.Visible;
        Root.Margin = next == "Console" ? new Thickness(0) : new Thickness(10);
        Topmost = next == "Console" || desktopPinned;
        DockPanel.Visibility = next == "Dock" ? Visibility.Visible : Visibility.Collapsed;
        if (next == "Console")
        {
            MinWidth = 320; MinHeight = 360; ResizeMode = ResizeMode.NoResize;
            SessionService.Fullscreen(this); ConsoleList.Focus();
        }
        else if (next == "Dock")
        {
            MinWidth = 520; MinHeight = 110; Width = Math.Min(980, SystemParameters.WorkArea.Width - 40); Height = 116;
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = SystemParameters.WorkArea.Bottom - Height - 16;
            ResizeMode = ResizeMode.NoResize;
        }
        else
        {
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinWidth = 900; MinHeight = 650;
            Width = Math.Max(MinWidth, Math.Min(desktopBounds.Width, SystemParameters.WorkArea.Width));
            Height = Math.Max(MinHeight, Math.Min(desktopBounds.Height, SystemParameters.WorkArea.Height));
            Left = Math.Clamp(desktopBounds.Left, SystemParameters.WorkArea.Left, Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - Width));
            Top = Math.Clamp(desktopBounds.Top, SystemParameters.WorkArea.Top, Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - Height));

        }
        OnSizeChanged(this, null!); Save();
    }
    private void ControllerInput(string action)
    {
        if (!IsActive) return;
        if (InOverlay) { OverlayInput(action); return; }
        switch (action)
        {
            case "Left": case "Up": MoveSelection(-1); break;
            case "Right": case "Down": MoveSelection(1); break;
            case "Play": Play(); break;
            case "Favorite": Favorite(); break;
            case "Back": case "Menu": ShowSystemMenu(); break;
            case "Search": ShowKeyboard("SEARCH LIBRARY", SearchBox.Text, text => { SearchBox.Text = text; Refresh(); }); break;
            case "Details": ShowGameOptions(); break;
            case "PreviousTab": ChangeTab(-1); break;
            case "NextTab": ChangeTab(1); break;
        }
    }
    private void ChangeTab(int delta)
    {
        string[] tabs = ["Library", "Favorites", "Recent"];
        filter = tabs[(Array.IndexOf(tabs, filter) + delta + 3) % 3]; Refresh();
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (InOverlay)
        {
            string? input = e.Key switch { Key.Left => "Left", Key.Right => "Right", Key.Up => "Up", Key.Down => "Down", Key.Enter => "Play", Key.Escape => "Back", _ => null };
            if (input is not null) { OverlayInput(input); e.Handled = true; }
            return;
        }
        if (e.Key == Key.F11) { SetMode(mode == "Console" ? "Desktop" : "Console"); e.Handled = true; return; }
        if (e.Key == Key.Escape) { ShowSystemMenu(); e.Handled = true; return; }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.F) { SetMode("Desktop"); SearchBox.Focus(); e.Handled = true; return; }
        if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is Button) return;
        if (e.Key is Key.Left or Key.Up) { MoveSelection(-1); e.Handled = true; }
        if (e.Key is Key.Right or Key.Down) { MoveSelection(1); e.Handled = true; }
        if (e.Key == Key.Enter) { Play(); e.Handled = true; }
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Shell is null) return;
        double w = Math.Max(100, ActualWidth - 20), h = Math.Max(80, ActualHeight - 20);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(new Point(30, 1), true, true);
            c.PolyLineTo(new Point[] { new(w * .64,1), new(w * .66,16), new(w * .78,16), new(w * .80,1), new(w-30,1), new(w-1,30), new(w-1,h-30), new(w-30,h-1), new(30,h-1), new(1,h-30), new(1,30) }, true, false);
        }
        Shell.Data = geometry;
        if (InfoColumn is not null) InfoColumn.Width = ActualWidth < 1120 ? new GridLength(0) : new GridLength(210);
    }
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (mode == "Console") return;
        if (e.ClickCount == 2 && mode != "Dock") { SetMode(mode == "Console" ? "Desktop" : "Console"); return; }
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not TextBox && e.OriginalSource is not Button)
            try { DragMove(); } catch (InvalidOperationException) { }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (SessionService.IsShellSession && !escapeDesktop)
        {
            e.Cancel = true;
            ShowMenu("EXIT CONSOLE SESSION", "Mulai Explorer dan keluar ke desktop Windows?", new (string, Action)[] { ("Exit to Windows desktop", ExitToDesktop), ("Stay in GameHub", () => { }) });
            return;
        }
        closing = true; lifetime.Cancel(); controller?.Dispose(); clock.Stop(); Save();
    }
    private void OnSearch(object sender, TextChangedEventArgs e) => Refresh();
    private void OnSelection(object sender, SelectionChangedEventArgs e) { if (PlayButton is not null) PlayButton.IsEnabled = Selected is not null; }
    private void OnDockSelection(object sender, SelectionChangedEventArgs e) { }
    private void OnGameDoubleClick(object sender, MouseButtonEventArgs e) { if (ItemsControl.ContainerFromElement((ListBox)sender, e.OriginalSource as DependencyObject) is ListBoxItem) Play(); }
    private void OnFilter(object sender, RoutedEventArgs e) { filter = (string)((Button)sender).Tag; Refresh(); }
    private void OnPlay(object sender, RoutedEventArgs e) => Play();
    private void OnFavorite(object sender, RoutedEventArgs e) => Favorite();
    private async void OnScan(object sender, RoutedEventArgs e) => await ScanAsync();
    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (mode == "Console") { BrowseGames(null); return; }
        var picker = new OpenFileDialog { Title = "Pilih game atau shortcut", Filter = "Game / shortcut|*.exe;*.lnk;*.url", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        var game = WindowsServices.ManualGame(picker.FileName);
        if (state.Games.Any(g => g.Id == game.Id)) { StatusText.Text = "Game sudah ada di library."; return; }
        state = state with { Games = state.Games.Append(game).ToList() }; filter = "Library"; SearchBox.Clear(); Refresh(); Selected = game; Save();
    }
    private void OnConsole(object sender, RoutedEventArgs e) => SetMode("Console");
    private void OnDesktop(object sender, RoutedEventArgs e) { SetMode("Desktop"); Activate(); }
    private void OnDock(object sender, RoutedEventArgs e) => SetMode("Dock");
    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnRandom(object sender, RoutedEventArgs e) { if (Games.Count > 0) { Selected = Games[Random.Shared.Next(Games.Count)]; GameList.ScrollIntoView(Selected); } }
    private void OnSettings(object sender, RoutedEventArgs e) => ShowSystemMenu();
}
