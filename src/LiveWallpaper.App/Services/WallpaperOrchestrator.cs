using System.IO;
using System.Windows.Threading;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.App.Services;

public sealed class WallpaperOrchestrator : IDisposable
{
    private readonly DesktopInjectionManager _injection = new();
    private readonly MonitorManager _monitorManager = new();
    private readonly PauseRuleEngine _pauseEngine = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly LibraryService _libraryService = new();
    private readonly IWallpaperPlayerFactory _playerFactory;
    private readonly Dictionary<string, IWallpaperPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DispatcherTimer> _playlistTimers = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;
    private bool _disposed;

    private int _recoveryRunning;
    private int _recoveryScheduled;
    private DateTime _lastRecoveryUtc = DateTime.MinValue;

    public event EventHandler? DisplaysChanged;
    public event EventHandler<string>? WallpaperApplied;

    private EventHandler? _displaySettingsChangedHandler;

    public WallpaperOrchestrator(IWallpaperPlayerFactory playerFactory)
    {
        _playerFactory = playerFactory;
        _settings = _settingsStore.Load();
        _pauseEngine.OnFullscreen = _settings.PauseRules.OnFullscreen;
        _pauseEngine.OnBattery = _settings.PauseRules.OnBattery;
        _pauseEngine.IdleMinutes = _settings.PauseRules.IdleMinutes;
        _pauseEngine.ManuallyPaused = _settings.ManuallyPaused;
        _pauseEngine.PauseStateChanged += OnPauseStateChanged;
        _injection.DesktopStructureChanged += (_, _) => ScheduleRecovery();
        _displaySettingsChangedHandler = (_, _) => _ = OnDisplaySettingsChangedAsync();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += _displaySettingsChangedHandler;
    }

    public AppSettings Settings => _settings;
    public SettingsStore SettingsStore => _settingsStore;
    public MonitorManager MonitorManager => _monitorManager;
    public PauseRuleEngine PauseEngine => _pauseEngine;
    public LibraryService LibraryService => _libraryService;
    public bool IsPaused => _pauseEngine.IsPaused;

    public async Task InitializeAsync()
    {
        AppLogger.Info("Initializing wallpaper orchestrator.");
        await ApplyAllAsync();
    }

    public PlayerLoadOptions BuildPlayerLoadOptions() => new()
    {
        MaxFps = _settings.MaxFps,
        MaxRenderHeight = _settings.MaxRenderHeight,
        VideoBackend = _settings.VideoBackend
    };

    public async Task ApplyWallpaperAsync(string deviceId, WallpaperDefinition wallpaper, FitMode fit, double volume)
    {
        await RunOnUiAsync(async () =>
        {
            var monitor = _monitorManager.FindByDeviceId(deviceId)
                ?? throw new InvalidOperationException($"Monitor not found: {deviceId}");

            await StopPlayerAsync(deviceId);

            if (wallpaper.Type == WallpaperType.None)
            {
                SaveMonitorSettings(monitor, wallpaper, fit, volume);
                return;
            }

            var options = BuildPlayerLoadOptions();
            var host = _injection.GetOrCreateHost(monitor);
            var player = _playerFactory.Create(wallpaper.Type, host, options);
            await player.LoadAsync(wallpaper, fit, volume, options);
            _players[deviceId] = player;

            if (!_pauseEngine.IsPaused)
            {
                player.Play();
            }
            else
            {
                player.NotifyPausedState(true);
            }

            SaveMonitorSettings(monitor, wallpaper, fit, volume);
            LibraryService.MarkApplied(_settings.Library, wallpaper.Path);
            StartPlaylistTimer(deviceId);
            AppLogger.Info($"Applied {wallpaper.Type} wallpaper to {deviceId}. HWND=0x{host.Handle:X}");
            WallpaperApplied?.Invoke(this, Path.GetFileName(wallpaper.Path));
        });
    }

    public async Task ApplyAllAsync()
    {
        var monitors = _monitorManager.GetMonitors();
        foreach (var monitor in monitors)
        {
            var monitorSettings = _monitorManager.GetOrCreateSettings(_settings, monitor);
            if (monitorSettings.Wallpaper.Type == WallpaperType.None)
            {
                continue;
            }

            try
            {
                await ApplyWallpaperAsync(
                    monitor.DeviceId,
                    monitorSettings.Wallpaper,
                    monitorSettings.Fit,
                    monitorSettings.Volume);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to apply wallpaper on {monitor.DeviceId}", ex);
            }
        }
    }

    public async Task ApplyNextWallpaperAsync(string deviceId, IReadOnlyList<LibraryEntry> libraryEntries)
    {
        await CycleLibraryWallpaperAsync(deviceId, libraryEntries, step: 1);
    }

    public async Task ApplyPreviousWallpaperAsync(string deviceId, IReadOnlyList<LibraryEntry> libraryEntries)
    {
        await CycleLibraryWallpaperAsync(deviceId, libraryEntries, step: -1);
    }

    private async Task CycleLibraryWallpaperAsync(string deviceId, IReadOnlyList<LibraryEntry> libraryEntries, int step)
    {
        if (libraryEntries.Count == 0)
        {
            return;
        }

        var monitorSettings = _monitorManager.GetOrCreateSettings(
            _settings,
            _monitorManager.FindByDeviceId(deviceId) ?? _monitorManager.GetMonitors().First());

        var currentPath = monitorSettings.Wallpaper.Path;
        var index = libraryEntries.ToList().FindIndex(e =>
            string.Equals(Path.GetFullPath(e.Path), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            index = (index + step + libraryEntries.Count) % libraryEntries.Count;
        }

        var entry = libraryEntries[index];
        var definition = BuildDefinitionFromEntry(entry);
        await ApplyWallpaperAsync(deviceId, definition, monitorSettings.Fit, monitorSettings.Volume);
    }

    public static WallpaperDefinition BuildDefinitionFromEntry(LibraryEntry entry)
    {
        return entry.Type switch
        {
            WallpaperType.Video => new WallpaperDefinition { Type = WallpaperType.Video, Path = entry.Path },
            WallpaperType.Web => new WallpaperDefinition { Type = WallpaperType.Web, Path = entry.Path },
            _ => new WallpaperDefinition
            {
                Type = WallpaperType.Slideshow,
                Path = Directory.Exists(entry.Path) ? entry.Path : Path.GetDirectoryName(entry.Path) ?? entry.Path,
                SlideshowIntervalSeconds = 30,
                Shuffle = false,
                Transition = SlideshowTransition.Fade,
                TransitionDurationMs = 800
            }
        };
    }

    public void SetPowerProfile(PowerProfile profile)
    {
        _settings.PowerProfile = profile;
        if (profile != PowerProfile.Custom)
        {
            _settings.MaxFps = PowerProfileDefaults.GetMaxFps(profile);
        }

        ApplyRuntimeSettings();
        SaveSettings();
    }

    public string GetCurrentWallpaperName(string deviceId)
    {
        var monitorSettings = _settings.Monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (monitorSettings?.Wallpaper.Path == null)
        {
            return "None";
        }

        return Path.GetFileName(monitorSettings.Wallpaper.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public void SetPaused(bool paused)
    {
        _pauseEngine.ManuallyPaused = paused;
        _settings.ManuallyPaused = paused;
        _pauseEngine.Evaluate();
        SaveSettings();
    }

    public void UpdatePauseRules(PauseRules rules)
    {
        _settings.PauseRules = rules;
        _pauseEngine.OnFullscreen = rules.OnFullscreen;
        _pauseEngine.OnBattery = rules.OnBattery;
        _pauseEngine.IdleMinutes = rules.IdleMinutes;
        _pauseEngine.Evaluate();
        SaveSettings();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(_settings);
    }

    public void ApplyRuntimeSettings()
    {
        ApplyMaxFpsToPlayers(_settings.MaxFps);
        foreach (var player in _players.Values)
        {
            player.NotifyPausedState(_pauseEngine.IsPaused);
        }
    }

    public void ApplyMaxFpsToPlayers(int maxFps)
    {
        foreach (var player in _players.Values)
        {
            player.SetMaxFps(maxFps);
        }
    }

    public string GetWallpaperSummary(string deviceId)
    {
        var monitorSettings = _settings.Monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (monitorSettings == null || monitorSettings.Wallpaper.Type == WallpaperType.None)
        {
            return "None";
        }

        var path = monitorSettings.Wallpaper.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return monitorSettings.Wallpaper.Type.ToString();
        }

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var playback = monitorSettings.Wallpaper.Type == WallpaperType.Video
            ? $" [{VideoPlaybackMode.DescribePlayback(_settings.MaxFps)}]"
            : string.Empty;
        return $"{monitorSettings.Wallpaper.Type}: {name}{playback}";
    }

    public IReadOnlyList<string> ScanLibrary(string rootPath) =>
        _libraryService.ScanAndMerge(rootPath, _settings.Library).Select(e => e.Path).ToList();

    public IReadOnlyList<LibraryEntry> GetLibraryEntries(string rootPath, LibrarySortMode sort = LibrarySortMode.Name) =>
        _libraryService.ScanAndMerge(rootPath, _settings.Library, sort);

    private void StartPlaylistTimer(string deviceId)
    {
        StopPlaylistTimer(deviceId);
        var monitorSettings = _settings.Monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        var playlist = monitorSettings?.Playlist;
        if (playlist == null || playlist.Paths.Count < 2 || playlist.IntervalSeconds < 5)
        {
            return;
        }

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(playlist.IntervalSeconds)
        };
        timer.Tick += async (_, _) =>
        {
            if (_pauseEngine.IsPaused)
            {
                return;
            }

            playlist.CurrentIndex = playlist.Shuffle
                ? Random.Shared.Next(playlist.Paths.Count)
                : (playlist.CurrentIndex + 1) % playlist.Paths.Count;

            var path = playlist.Paths[playlist.CurrentIndex];
            var entry = new LibraryEntry
            {
                Path = path,
                Type = LibraryService.ClassifyPath(path),
                DisplayName = LibraryService.GetDisplayName(path)
            };
            var definition = BuildDefinitionFromEntry(entry);
            if (monitorSettings != null)
            {
                await ApplyWallpaperAsync(deviceId, definition, monitorSettings.Fit, monitorSettings.Volume);
            }
        };
        timer.Start();
        _playlistTimers[deviceId] = timer;
    }

    private void StopPlaylistTimer(string deviceId)
    {
        if (_playlistTimers.TryGetValue(deviceId, out var timer))
        {
            timer.Stop();
            _playlistTimers.Remove(deviceId);
        }
    }

    private void ScheduleRecovery()
    {
        if (Interlocked.CompareExchange(ref _recoveryScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000);
                await RecoverAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _recoveryScheduled, 0);
            }
        });
    }

    public async Task RecoverAsync()
    {
        if (Interlocked.CompareExchange(ref _recoveryRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _lastRecoveryUtc = DateTime.UtcNow;
            AppLogger.Warn("Recovering wallpapers after desktop structure change.");
            await RunOnUiAsync(async () =>
            {
                _injection.RefreshAll();

                var snapshots = _players.ToList();
                foreach (var (deviceId, player) in snapshots)
                {
                    var monitorSettings = _settings.Monitors.FirstOrDefault(m =>
                        string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
                    if (monitorSettings == null || monitorSettings.Wallpaper.Type == WallpaperType.None)
                    {
                        continue;
                    }

                    var monitor = _monitorManager.FindByDeviceId(deviceId);
                    if (monitor == null)
                    {
                        continue;
                    }

                    var host = _injection.GetOrCreateHost(monitor);
                    if (host.Handle != IntPtr.Zero && NativeMethods.IsWindow(host.Handle))
                    {
                        continue;
                    }

                    try
                    {
                        await StopPlayerAsync(deviceId);
                        await ApplyWallpaperAsync(
                            deviceId,
                            monitorSettings.Wallpaper,
                            monitorSettings.Fit,
                            monitorSettings.Volume);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Recovery failed for {deviceId}", ex);
                    }
                }
            });
        }
        finally
        {
            Interlocked.Exchange(ref _recoveryRunning, 0);
        }
    }

    public async Task ShutdownAsync()
    {
        await RunOnUiAsync(async () =>
        {
            foreach (var deviceId in _players.Keys.ToList())
            {
                await StopPlayerAsync(deviceId);
            }

            foreach (var deviceId in _injection.Hosts.Keys.ToList())
            {
                _injection.RemoveHost(deviceId);
            }
        });
    }

    private void OnPauseStateChanged(object? sender, bool paused)
    {
        _ = RunOnUiAsync(() =>
        {
            foreach (var player in _players.Values)
            {
                if (paused)
                {
                    player.Pause();
                }
                else
                {
                    player.Play();
                }

                player.NotifyPausedState(paused);
            }

            return Task.CompletedTask;
        });
    }

    private async Task StopPlayerAsync(string deviceId)
    {
        StopPlaylistTimer(deviceId);
        if (_players.TryGetValue(deviceId, out var player))
        {
            player.Stop();
            player.Dispose();
            _players.Remove(deviceId);
        }

        await Task.Yield();
    }

    private static Task RunOnUiAsync(Func<Task> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private void SaveMonitorSettings(DisplayMonitor monitor, WallpaperDefinition wallpaper, FitMode fit, double volume)
    {
        var settings = _monitorManager.GetOrCreateSettings(_settings, monitor);
        settings.Wallpaper = wallpaper;
        settings.Fit = fit;
        settings.Volume = volume;
        SaveSettings();
    }

    private async Task OnDisplaySettingsChangedAsync()
    {
        AppLogger.Info("Display settings changed; refreshing monitors and wallpapers.");
        await RunOnUiAsync(async () =>
        {
            var monitors = _monitorManager.GetMonitors();
            foreach (var monitor in monitors)
            {
                if (_players.TryGetValue(monitor.DeviceId, out var player))
                {
                    player.Resize(monitor);
                }
            }

            await ApplyAllAsync();
            DisplaysChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var timer in _playlistTimers.Values)
        {
            timer.Stop();
        }

        _playlistTimers.Clear();

        foreach (var player in _players.Values)
        {
            player.Dispose();
        }

        _players.Clear();
        _pauseEngine.Dispose();
        if (_displaySettingsChangedHandler != null)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= _displaySettingsChangedHandler;
        }

        _injection.Dispose();
        _disposed = true;
    }
}
