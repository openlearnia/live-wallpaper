using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using LiveWallpaper.App.Services;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.App;

public partial class MainWindow : Window
{
    private readonly WallpaperOrchestratorService _service;
    private readonly ThumbnailGenerator _thumbnails = new();
    private readonly TrayIconService? _tray;
    private readonly HotkeyService? _hotkeyService;
    private string _libraryPath = string.Empty;
    private List<LibraryViewModel> _allLibraryItems = new();
    private string _libraryFilter = "All";
    private LibrarySortMode _librarySort = LibrarySortMode.Name;
    private bool _loadingSettings;

    public MainWindow(WallpaperOrchestratorService service, TrayIconService? tray = null, HotkeyService? hotkeyService = null)
    {
        _service = service;
        _tray = tray;
        _hotkeyService = hotkeyService;
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        _service.Orchestrator.DisplaysChanged += (_, _) => Dispatcher.Invoke(RefreshMonitors);
        _service.Orchestrator.WallpaperApplied += (_, name) => Dispatcher.Invoke(() => _tray?.ShowAppliedNotification(name));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _libraryPath = string.IsNullOrWhiteSpace(_service.Orchestrator.Settings.LibraryRootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LiveWallpapers")
            : _service.Orchestrator.Settings.LibraryRootPath;
        Directory.CreateDirectory(_libraryPath);
        LibraryPathText.Text = _libraryPath;

        PowerProfileCombo.ItemsSource = Enum.GetValues<PowerProfile>();
        VideoBackendCombo.ItemsSource = Enum.GetValues<VideoBackend>();
        SlideshowTransitionCombo.ItemsSource = Enum.GetValues<SlideshowTransition>();
        LibrarySortCombo.ItemsSource = Enum.GetValues<LibrarySortMode>();
        MaxRenderHeightCombo.ItemsSource = new[] { "Off", "1080p", "720p" };

        LibrarySortCombo.SelectedItem = LibrarySortMode.Name;
        SlideshowTransitionCombo.SelectedItem = SlideshowTransition.Fade;
        MaxRenderHeightCombo.SelectedIndex = 0;
        VersionText.Text = OpenlearniaInfo.GetVersionLabel();

        RefreshMonitors();
        LoadSettingsUi();
        RefreshLibrary();
        TrySeedSampleWallpaper();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        StatusText.Text = "Running in tray. Double-click tray icon to reopen.";
    }

    private void TrySeedSampleWallpaper()
    {
        var seeded = TrySeedSampleFolder("sample-web") | TrySeedSampleFolder("sample-mandelbrot");
        if (seeded)
        {
            RefreshLibrary();
        }
    }

    private bool TrySeedSampleFolder(string folderName)
    {
        var sampleDest = Path.Combine(_libraryPath, folderName);
        if (Directory.Exists(sampleDest))
        {
            return false;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "assets", folderName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", folderName),
            Path.Combine(Environment.CurrentDirectory, "assets", folderName)
        };

        foreach (var source in candidates)
        {
            var full = Path.GetFullPath(source);
            if (!Directory.Exists(full))
            {
                continue;
            }

            CopyDirectory(full, sampleDest);
            return true;
        }

        return false;
    }

    private void RefreshMonitors()
    {
        var monitors = _service.Orchestrator.MonitorManager.GetMonitors();
        var rows = monitors.Select(m => new MonitorRow(
            m,
            _service.Orchestrator.GetWallpaperSummary(m.DeviceId))).ToList();

        MonitorList.ItemsSource = rows;
        if (rows.Count > 0 && MonitorList.SelectedItem == null)
        {
            MonitorList.SelectedIndex = 0;
        }
    }

    private void LibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HighlightAppliedLibraryItem();
        UpdateSlideshowSettingsUi();
    }

    private void UpdateSlideshowSettingsUi()
    {
        if (LibraryList.SelectedItem is not LibraryViewModel item || item.Entry.Type != WallpaperType.Slideshow)
        {
            SlideshowSettingsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SlideshowSettingsPanel.Visibility = Visibility.Visible;
        var monitor = GetSelectedMonitor();
        if (monitor == null)
        {
            return;
        }

        var wallpaper = _service.Orchestrator.Settings.Monitors
            .FirstOrDefault(m => string.Equals(m.DeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase))
            ?.Wallpaper;
        if (wallpaper?.Type == WallpaperType.Slideshow &&
            PathsMatch(wallpaper.Path, item.Entry.Path))
        {
            SlideshowIntervalBox.Text = wallpaper.SlideshowIntervalSeconds.ToString();
            SlideshowShuffleCheck.IsChecked = wallpaper.Shuffle;
            if (wallpaper.Transition != SlideshowTransition.Cut)
            {
                SlideshowTransitionCombo.SelectedItem = wallpaper.Transition;
            }
        }
    }

    private static bool PathsMatch(string appliedPath, string entryPath)
    {
        if (string.IsNullOrWhiteSpace(appliedPath) || string.IsNullOrWhiteSpace(entryPath))
        {
            return false;
        }

        var applied = Path.GetFullPath(appliedPath);
        var entry = Path.GetFullPath(entryPath);
        if (string.Equals(applied, entry, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Directory.Exists(applied) && Directory.Exists(entry))
        {
            return string.Equals(applied, entry, StringComparison.OrdinalIgnoreCase);
        }

        if (File.Exists(entryPath))
        {
            var entryDir = Path.GetDirectoryName(entry);
            return !string.IsNullOrWhiteSpace(entryDir) &&
                   string.Equals(applied, Path.GetFullPath(entryDir), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void MonitorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        HighlightAppliedLibraryItem();
        UpdateSlideshowSettingsUi();
    }

    private DisplayMonitor? GetSelectedMonitor() =>
        MonitorList.SelectedItem is MonitorRow row ? row.Monitor : null;

    private string GetPrimaryDeviceId() =>
        GetSelectedMonitor()?.DeviceId ?? _service.Orchestrator.MonitorManager.GetMonitors().FirstOrDefault()?.DeviceId ?? string.Empty;

    private void LoadSettingsUi()
    {
        _loadingSettings = true;
        var settings = _service.Orchestrator.Settings;
        StartupCheck.IsChecked = settings.Startup;
        StartMinimizedCheck.IsChecked = settings.StartMinimizedToTray;
        TrayNotificationsCheck.IsChecked = settings.ShowTrayNotifications;
        FullscreenPauseCheck.IsChecked = settings.PauseRules.OnFullscreen;
        BatteryPauseCheck.IsChecked = settings.PauseRules.OnBattery;
        IdleMinutesBox.Text = settings.PauseRules.IdleMinutes.ToString();
        MaxFpsBox.Text = settings.MaxFps.ToString();
        PausedCheck.IsChecked = settings.ManuallyPaused;
        VideoBackendCombo.SelectedItem = settings.VideoBackend;

        var profile = settings.PowerProfile;
        if (profile == PowerProfile.Custom && PowerProfileDefaults.DetectProfile(settings.MaxFps) != PowerProfile.Custom)
        {
            profile = PowerProfileDefaults.DetectProfile(settings.MaxFps);
            settings.PowerProfile = profile;
        }

        PowerProfileCombo.SelectedItem = profile;
        UpdatePowerProfileUi(profile);
        MaxRenderHeightCombo.SelectedIndex = settings.MaxRenderHeight switch
        {
            1080 => 1,
            720 => 2,
            _ => 0
        };
        HotkeyPauseBox.Text = HotkeyBindingHelper.Format(settings.Hotkeys.PauseResume);
        HotkeyNextBox.Text = HotkeyBindingHelper.Format(settings.Hotkeys.NextWallpaper);
        HotkeyPrevBox.Text = HotkeyBindingHelper.Format(settings.Hotkeys.PreviousWallpaper);
        HotkeyShowBox.Text = HotkeyBindingHelper.Format(settings.Hotkeys.ShowWindow);
        _loadingSettings = false;
    }

    private void About_Click(object sender, RoutedEventArgs e) => OpenlearniaInfo.ShowAbout(this);

    private void PowerProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || PowerProfileCombo.SelectedItem is not PowerProfile profile)
        {
            return;
        }

        UpdatePowerProfileUi(profile);
        if (profile != PowerProfile.Custom)
        {
            MaxFpsBox.Text = PowerProfileDefaults.GetMaxFps(profile).ToString();
        }
    }

    private void UpdatePowerProfileUi(PowerProfile profile) =>
        MaxFpsBox.IsEnabled = profile == PowerProfile.Custom;

    private async void RefreshLibrary()
    {
        _service.Orchestrator.Settings.LibraryRootPath = _libraryPath;
        var entries = _service.Orchestrator.GetLibraryEntries(_libraryPath, _librarySort);
        _allLibraryItems = entries.Select(e => new LibraryViewModel(e)).ToList();
        ApplyLibraryFilter();
        _ = LoadThumbnailsAsync(_allLibraryItems);
    }

    private async Task LoadThumbnailsAsync(IEnumerable<LibraryViewModel> items)
    {
        foreach (var item in items)
        {
            var thumb = await _thumbnails.GetThumbnailAsync(item.Entry);
            if (thumb != null)
            {
                item.Thumbnail = thumb;
            }
        }
    }

    private void ApplyLibraryFilter()
    {
        IEnumerable<LibraryViewModel> filtered = _libraryFilter switch
        {
            "Video" => _allLibraryItems.Where(i => i.Entry.Type == WallpaperType.Video),
            "Web" => _allLibraryItems.Where(i => i.Entry.Type == WallpaperType.Web),
            "Slideshow" => _allLibraryItems.Where(i => i.Entry.Type == WallpaperType.Slideshow),
            "Favorites" => _allLibraryItems.Where(i => i.Entry.IsFavorite),
            _ => _allLibraryItems
        };

        var search = LibrarySearchBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(i =>
                i.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Entry.Path.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        LibraryList.ItemsSource = filtered.ToList();
        HighlightAppliedLibraryItem();
    }

    private void HighlightAppliedLibraryItem()
    {
        var monitor = GetSelectedMonitor();
        if (monitor == null)
        {
            return;
        }

        var settings = _service.Orchestrator.Settings.Monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase));
        if (settings?.Wallpaper.Path == null)
        {
            return;
        }

        var appliedPath = Path.GetFullPath(settings.Wallpaper.Path);
        if (LibraryList.ItemsSource is IEnumerable<LibraryViewModel> items)
        {
            LibraryList.SelectedItem = items.FirstOrDefault(i =>
                string.Equals(Path.GetFullPath(i.Entry.Path), appliedPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string filter)
        {
            _libraryFilter = filter;
            ApplyLibraryFilter();
        }
    }

    private void LibrarySearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyLibraryFilter();

    private void LibrarySortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || LibrarySortCombo.SelectedItem is not LibrarySortMode sort)
        {
            return;
        }

        _librarySort = sort;
        RefreshLibrary();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e) =>
        await ApplySelectedItemAsync(LibraryList.SelectedItem as LibraryViewModel);

    private async Task ApplySelectedItemAsync(LibraryViewModel? item)
    {
        if (item == null)
        {
            System.Windows.MessageBox.Show("Select a wallpaper from the library.", "Live Wallpaper", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var monitors = ApplyAllMonitorsCheck.IsChecked == true
            ? _service.Orchestrator.MonitorManager.GetMonitors()
            : GetSelectedMonitor() is { } selected ? new[] { selected } : Array.Empty<DisplayMonitor>();

        if (monitors.Count == 0)
        {
            System.Windows.MessageBox.Show("Select a monitor.", "Live Wallpaper", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var definition = BuildDefinition(item.Entry);
            var fit = Enum.TryParse<FitMode>(FitCombo.SelectedItem?.ToString(), out var parsedFit)
                ? parsedFit
                : FitMode.Fill;
            var volume = VolumeSlider.Value;

            if (CreatePlaylistCheck.IsChecked == true && LibraryList.ItemsSource is IEnumerable<LibraryViewModel> filtered)
            {
                var paths = filtered.Select(v => v.Entry.Path).ToList();
                foreach (var monitor in monitors)
                {
                    var ms = _service.Orchestrator.MonitorManager.GetOrCreateSettings(_service.Orchestrator.Settings, monitor);
                    ms.Playlist = new WallpaperPlaylist
                    {
                        Name = "Library filter",
                        Paths = paths,
                        Shuffle = SlideshowShuffleCheck.IsChecked == true,
                        IntervalSeconds = int.TryParse(SlideshowIntervalBox.Text, out var interval)
                            ? Math.Max(5, interval)
                            : 300,
                        CurrentIndex = paths.FindIndex(p => string.Equals(p, item.Entry.Path, StringComparison.OrdinalIgnoreCase))
                    };
                }
            }

            foreach (var monitor in monitors)
            {
                await _service.Orchestrator.ApplyWallpaperAsync(monitor.DeviceId, definition, fit, volume);
            }

            _service.Orchestrator.SaveSettings();
            RefreshMonitors();
            HighlightAppliedLibraryItem();
            StatusText.Text = monitors.Count == 1
                ? $"Applied {item.Entry.Type} to {monitors[0].DisplayName}"
                : $"Applied {item.Entry.Type} to {monitors.Count} monitors";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Apply failed", ex);
            System.Windows.MessageBox.Show(ex.Message, "Apply failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task CycleWallpaperAsync(int step)
    {
        if (LibraryList.ItemsSource is not IList<LibraryViewModel> items || items.Count == 0)
        {
            RefreshLibrary();
            items = LibraryList.ItemsSource as IList<LibraryViewModel> ?? new List<LibraryViewModel>();
        }

        var entries = items.Select(i => i.Entry).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var deviceId = GetPrimaryDeviceId();
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        if (step > 0)
        {
            await _service.Orchestrator.ApplyNextWallpaperAsync(deviceId, entries);
        }
        else
        {
            await _service.Orchestrator.ApplyPreviousWallpaperAsync(deviceId, entries);
        }

        RefreshMonitors();
        HighlightAppliedLibraryItem();
    }

    private async void LibraryList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        await ApplySelectedItemAsync(LibraryList.SelectedItem as LibraryViewModel);

    private async void LibraryContextApply_Click(object sender, RoutedEventArgs e) =>
        await ApplySelectedItemAsync(LibraryList.SelectedItem as LibraryViewModel);

    private void LibraryContextFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryViewModel item)
        {
            return;
        }

        LibraryService.ToggleFavorite(_service.Orchestrator.Settings.Library, item.Entry.Path);
        item.RefreshFavorite();
        _service.Orchestrator.SaveSettings();
        ApplyLibraryFilter();
    }

    private void LibraryContextOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryViewModel item)
        {
            return;
        }

        var path = File.Exists(item.Entry.Path)
            ? Path.GetDirectoryName(item.Entry.Path)
            : item.Entry.Path;
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
    }

    private void LibraryContextRemove_Click(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryViewModel item)
        {
            return;
        }

        var name = item.DisplayName;
        var result = System.Windows.MessageBox.Show(
            $"Remove \"{name}\" from the library list?\n\nFiles on disk are not deleted.",
            "Remove from Library",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (LibraryService.RemoveFromLibrary(_service.Orchestrator.Settings.Library, item.Entry.Path))
        {
            _service.Orchestrator.SaveSettings();
            RefreshLibrary();
            StatusText.Text = $"Removed {name} from library.";
        }
    }

    private WallpaperDefinition BuildDefinition(LibraryEntry entry)
    {
        var definition = WallpaperOrchestrator.BuildDefinitionFromEntry(entry);
        if (entry.Type == WallpaperType.Slideshow && File.Exists(entry.Path))
        {
            definition.Path = Path.GetDirectoryName(entry.Path) ?? entry.Path;
        }

        if (definition.Type == WallpaperType.Slideshow &&
            SlideshowTransitionCombo.SelectedItem is SlideshowTransition transition)
        {
            definition.Transition = transition;
            definition.TransitionDurationMs = 800;
            definition.SlideshowIntervalSeconds = int.TryParse(SlideshowIntervalBox.Text, out var interval)
                ? Math.Max(5, interval)
                : 30;
            definition.Shuffle = SlideshowShuffleCheck.IsChecked == true;
        }

        return definition;
    }

    private void BrowseLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = _libraryPath };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _libraryPath = dialog.SelectedPath;
            LibraryPathText.Text = _libraryPath;
            RefreshLibrary();
        }
    }

    private void AddVideo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Video files|*.mp4;*.webm;*.wmv;*.avi;*.mkv|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            var dest = Path.Combine(_libraryPath, Path.GetFileName(dialog.FileName));
            if (!File.Exists(dest))
            {
                File.Copy(dialog.FileName, dest);
            }

            RefreshLibrary();
        }
    }

    private void AddSlideshowFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var folderName = new DirectoryInfo(dialog.SelectedPath).Name;
            var dest = Path.Combine(_libraryPath, folderName);
            CopyDirectory(dialog.SelectedPath, dest);
            RefreshLibrary();
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _service.Orchestrator.Settings;
        settings.Startup = StartupCheck.IsChecked == true;
        settings.StartMinimizedToTray = StartMinimizedCheck.IsChecked == true;
        settings.ShowTrayNotifications = TrayNotificationsCheck.IsChecked == true;
        settings.PowerProfile = PowerProfileCombo.SelectedItem is PowerProfile profile
            ? profile
            : PowerProfile.Balanced;
        settings.MaxFps = settings.PowerProfile == PowerProfile.Custom
            ? int.TryParse(MaxFpsBox.Text, out var customFps) ? Math.Clamp(customFps, 15, 120) : 30
            : PowerProfileDefaults.GetMaxFps(settings.PowerProfile);
        MaxFpsBox.Text = settings.MaxFps.ToString();
        settings.VideoBackend = VideoBackendCombo.SelectedItem is VideoBackend backend
            ? backend
            : VideoBackend.Wpf;
        settings.MaxRenderHeight = MaxRenderHeightCombo.SelectedIndex switch
        {
            1 => 1080,
            2 => 720,
            _ => 0
        };
        settings.PauseRules = new PauseRules
        {
            OnFullscreen = FullscreenPauseCheck.IsChecked == true,
            OnBattery = BatteryPauseCheck.IsChecked == true,
            IdleMinutes = int.TryParse(IdleMinutesBox.Text, out var idle) ? Math.Max(0, idle) : 0
        };

        if (!TrySaveHotkeys(settings))
        {
            return;
        }

        _service.Orchestrator.UpdatePauseRules(settings.PauseRules);
        _service.Orchestrator.SetPaused(PausedCheck.IsChecked == true);
        _service.Orchestrator.ApplyRuntimeSettings();
        StartupRegistry.SetRunAtStartup(settings.Startup);
        _service.Orchestrator.SaveSettings();
        _tray?.UpdatePauseMenuLabel(_service.Orchestrator.IsPaused);
        _tray?.RefreshFromSettings();
        StatusText.Text = "Settings saved.";
    }

    private bool TrySaveHotkeys(AppSettings settings)
    {
        var fields = new (string Label, string Text, Action<HotkeyBinding> Assign)[]
        {
            ("Pause / Resume", HotkeyPauseBox.Text, b => settings.Hotkeys.PauseResume = b),
            ("Next wallpaper", HotkeyNextBox.Text, b => settings.Hotkeys.NextWallpaper = b),
            ("Previous wallpaper", HotkeyPrevBox.Text, b => settings.Hotkeys.PreviousWallpaper = b),
            ("Show window", HotkeyShowBox.Text, b => settings.Hotkeys.ShowWindow = b)
        };

        var parsed = new List<HotkeyBinding>();
        foreach (var (label, text, _) in fields)
        {
            if (!HotkeyBindingHelper.TryParse(text, out var binding))
            {
                System.Windows.MessageBox.Show(
                    $"Invalid hotkey for {label}: \"{text}\".\nUse format like Ctrl+Shift+P.",
                    "Invalid Hotkey",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            parsed.Add(binding);
        }

        if (parsed.DistinctBy(b => (b.Modifiers, b.Key)).Count() != parsed.Count)
        {
            System.Windows.MessageBox.Show(
                "Each hotkey must be unique.",
                "Invalid Hotkey",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        foreach (var (label, text, assign) in fields)
        {
            HotkeyBindingHelper.TryParse(text, out var binding);
            assign(binding);
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            _hotkeyService?.Register(hwnd, settings.Hotkeys);
        }

        return true;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        var paused = !_service.Orchestrator.IsPaused;
        _service.Orchestrator.SetPaused(paused);
        SyncPauseUi(paused);
        StatusText.Text = paused ? "Paused" : "Resumed";
    }

    public void SyncPauseUi(bool paused)
    {
        PausedCheck.IsChecked = paused;
        _tray?.UpdatePauseMenuLabel(paused);
    }

    private sealed class LibraryViewModel : INotifyPropertyChanged
    {
        private ImageSource? _thumbnail;

        public LibraryViewModel(LibraryEntry entry) => Entry = entry;

        public LibraryEntry Entry { get; }
        public string DisplayName => Entry.DisplayName;
        public string TypeLabel => Entry.Type.ToString();
        public string FavoriteMark => Entry.IsFavorite ? "★" : string.Empty;

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            }
        }

        public void RefreshFavorite() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteMark)));

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record MonitorRow(DisplayMonitor Monitor, string WallpaperSummary)
    {
        public string Display => $"{Monitor.DisplayName} — {WallpaperSummary}";
    }
}
