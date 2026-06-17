using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Players;

public sealed class SlideshowWallpaperPlayer : IWallpaperPlayer
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    private readonly WallpaperHostWindow _host;
    private readonly Grid _root = new();
    private readonly Image _imageA = new() { Stretch = Stretch.UniformToFill };
    private readonly Image _imageB = new() { Stretch = Stretch.UniformToFill, Opacity = 0 };
    private readonly DispatcherTimer _timer = new();
    private readonly Random _random = new();

    private List<string> _images = new();
    private int _index;
    private bool _disposed;
    private bool _isPlaying;
    private int _intervalSeconds = 30;
    private bool _shuffle;
    private SlideshowTransition _transition = SlideshowTransition.Cut;
    private int _transitionDurationMs = 800;
    private bool _showA = true;

    public SlideshowWallpaperPlayer(WallpaperHostWindow host)
    {
        _host = host;
        _root.Children.Add(_imageA);
        _root.Children.Add(_imageB);
        _timer.Tick += (_, _) => ShowNext();
    }

    public string DeviceId => _host.Monitor.DeviceId;
    public bool IsPlaying => _isPlaying;

    public Task LoadAsync(
        WallpaperDefinition definition,
        FitMode fit,
        double volume,
        PlayerLoadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (definition.Type != WallpaperType.Slideshow || string.IsNullOrWhiteSpace(definition.Path))
        {
            throw new DirectoryNotFoundException("Slideshow folder not specified.");
        }

        var folder = definition.Path;
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Slideshow folder not found: {folder}");
        }

        _images = Directory.EnumerateFiles(folder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_images.Count == 0)
        {
            throw new InvalidOperationException("No images found in slideshow folder.");
        }

        _intervalSeconds = Math.Max(5, definition.SlideshowIntervalSeconds);
        _shuffle = definition.Shuffle;
        _transition = definition.Transition;
        _transitionDurationMs = Math.Clamp(definition.TransitionDurationMs, 200, 3000);
        _index = 0;
        var stretch = fit switch
        {
            FitMode.Fit => Stretch.Uniform,
            FitMode.Stretch => Stretch.Fill,
            _ => Stretch.UniformToFill
        };
        _imageA.Stretch = stretch;
        _imageB.Stretch = stretch;

        _host.EnsureCreated();
        _host.SetContent(_root);
        _showA = true;
        _imageB.Opacity = 0;
        ShowImageAt(_index, animate: false);
        return Task.CompletedTask;
    }

    public void Play()
    {
        _timer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
        _timer.Start();
        _isPlaying = true;
    }

    public void Pause()
    {
        _timer.Stop();
        _isPlaying = false;
    }

    public void Stop() => Pause();

    public void Resize(DisplayMonitor monitor) => _host.Resize(monitor);

    public void SetMaxFps(int maxFps)
    {
    }

    public void NotifyPausedState(bool paused)
    {
    }

    private void ShowNext()
    {
        if (_images.Count == 0)
        {
            return;
        }

        if (_shuffle)
        {
            _index = _random.Next(_images.Count);
        }
        else
        {
            _index = (_index + 1) % _images.Count;
        }

        ShowImageAt(_index, animate: _transition == SlideshowTransition.Fade);
    }

    private void ShowImageAt(int index, bool animate)
    {
        var path = _images[index];
        var bitmap = LoadBitmap(path);
        var incoming = _showA ? _imageB : _imageA;
        var outgoing = _showA ? _imageA : _imageB;
        incoming.Source = bitmap;

        if (!animate)
        {
            outgoing.Source = bitmap;
            outgoing.Opacity = 1;
            incoming.Opacity = 0;
            return;
        }

        incoming.Opacity = 0;
        var duration = TimeSpan.FromMilliseconds(_transitionDurationMs);
        incoming.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration));
        outgoing.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, duration));
        _showA = !_showA;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
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
