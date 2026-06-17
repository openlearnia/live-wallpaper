namespace LiveWallpaper.Core.Models;

public enum WallpaperType
{
    None,
    Video,
    Slideshow,
    Web
}

public enum FitMode
{
    Fill,
    Fit,
    Stretch
}

public enum SlideshowTransition
{
    Cut,
    Fade
}

public sealed class WallpaperDefinition
{
    public WallpaperType Type { get; set; } = WallpaperType.None;
    public string Path { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int SlideshowIntervalSeconds { get; set; } = 30;
    public bool Shuffle { get; set; }
    public SlideshowTransition Transition { get; set; } = SlideshowTransition.Cut;
    public int TransitionDurationMs { get; set; } = 800;
}

public sealed class WallpaperPlaylist
{
    public string Name { get; set; } = string.Empty;
    public List<string> Paths { get; set; } = new();
    public bool Shuffle { get; set; }
    public int IntervalSeconds { get; set; } = 300;
    public int CurrentIndex { get; set; }
}

public sealed class MonitorSettings
{
    public string DeviceId { get; set; } = string.Empty;
    public WallpaperDefinition Wallpaper { get; set; } = new();
    public FitMode Fit { get; set; } = FitMode.Fill;
    public double Volume { get; set; }
    public WallpaperPlaylist? Playlist { get; set; }
}

public sealed class PauseRules
{
    public bool OnFullscreen { get; set; } = true;
    public bool OnBattery { get; set; }
    public int IdleMinutes { get; set; }
}

public sealed class AppSettings
{
    public int Version { get; set; } = 3;
    public bool Startup { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool ManuallyPaused { get; set; }
    public bool ShowTrayNotifications { get; set; } = true;
    public List<MonitorSettings> Monitors { get; set; } = new();
    public PauseRules PauseRules { get; set; } = new();
    public PowerProfile PowerProfile { get; set; } = PowerProfile.Balanced;
    public int MaxFps { get; set; } = 30;
    public VideoBackend VideoBackend { get; set; } = VideoBackend.Wpf;
    public int MaxRenderHeight { get; set; }
    public HotkeySettings Hotkeys { get; set; } = new();
    public LibraryMetadata Library { get; set; } = new();
    public string LibraryRootPath { get; set; } = string.Empty;
}
