using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LiveWallpaper.Core;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Players;

/// <summary>
/// Phase 0 spike player: renders an animated test pattern into the injected desktop HWND.
/// </summary>
public sealed class TestPatternWallpaperPlayer : IWallpaperPlayer
{
    private readonly WallpaperHostWindow _host;
    private readonly Canvas _canvas = new();
    private readonly Rectangle _rect;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private bool _disposed;
    private bool _isPlaying;
    private double _hue;

    public TestPatternWallpaperPlayer(WallpaperHostWindow host)
    {
        _host = host;
        _rect = new Rectangle
        {
            Width = host.Monitor.Width,
            Height = host.Monitor.Height
        };
        _canvas.Children.Add(_rect);
        _timer.Tick += (_, _) =>
        {
            _hue = (_hue + 2) % 360;
            _rect.Fill = new SolidColorBrush(ColorFromHsv(_hue, 0.7, 0.35));
        };
    }

    public string DeviceId => _host.Monitor.DeviceId;
    public bool IsPlaying => _isPlaying;

    public Task LoadAsync(Core.Models.WallpaperDefinition definition, Core.Models.FitMode fit, double volume, Core.Models.PlayerLoadOptions options, CancellationToken cancellationToken = default)
    {
        _host.EnsureCreated();
        _host.SetContent(_canvas);
        return Task.CompletedTask;
    }

    public void Play()
    {
        _timer.Start();
        _isPlaying = true;
    }

    public void Pause()
    {
        _timer.Stop();
        _isPlaying = false;
    }

    public void Stop() => Pause();

    public void Resize(DisplayMonitor monitor)
    {
        _host.Resize(monitor);
        _rect.Width = monitor.Width;
        _rect.Height = monitor.Height;
    }

    public void SetMaxFps(int maxFps)
    {
        var interval = TimeSpan.FromMilliseconds(1000.0 / Math.Clamp(maxFps, 15, 120));
        UiDispatcher.Run(() => _timer.Interval = interval);
    }

    public void NotifyPausedState(bool paused)
    {
    }

    private static Color ColorFromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
