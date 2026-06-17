using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Players;

public sealed class VideoWallpaperPlayer : IWallpaperPlayer
{
    private readonly WallpaperHostWindow _host;
    private readonly MediaPlayer _mediaPlayer = new();
    private readonly VideoDrawing _videoDrawing = new();
    private readonly DrawingImage _drawingImage;
    private readonly Image _image;
    private readonly Viewbox _viewbox;
    private DispatcherTimer? _frameTimer;
    private bool _disposed;
    private bool _isPlaying;
    private bool _pendingPlay;
    private int _maxFps = 30;
    private int _maxRenderHeight;

    public VideoWallpaperPlayer(WallpaperHostWindow host, int maxRenderHeight = 0)
    {
        _host = host;
        _maxRenderHeight = maxRenderHeight;
        _drawingImage = new DrawingImage(_videoDrawing);
        _image = new Image
        {
            Source = _drawingImage,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _viewbox = new Viewbox
        {
            Stretch = Stretch.UniformToFill,
            Child = _image,
            Width = host.Monitor.Width,
            Height = host.Monitor.Height
        };

        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.LowQuality);
        _videoDrawing.Player = _mediaPlayer;

        _mediaPlayer.MediaEnded += (_, _) => UiDispatcher.Run(OnMediaEnded);
        _mediaPlayer.MediaOpened += (_, _) => UiDispatcher.Run(() =>
        {
            UpdateVideoRect();
            ApplyRenderScale();
            AppLogger.Info(
                $"Video opened: {_mediaPlayer.NaturalVideoWidth}x{_mediaPlayer.NaturalVideoHeight} " +
                $"(max {_maxFps} fps, {VideoPlaybackMode.DescribePlayback(_maxFps)})");
            if (_pendingPlay || _isPlaying)
            {
                StartPlayback();
            }
        });
        _mediaPlayer.MediaFailed += (_, e) =>
        {
            AppLogger.Error($"Video playback failed: {e.ErrorException?.Message ?? "unknown"}", e.ErrorException);
        };
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
        return UiDispatcher.RunAsync(async () =>
        {
            if (definition.Type != WallpaperType.Video || string.IsNullOrWhiteSpace(definition.Path) || !File.Exists(definition.Path))
            {
                throw new FileNotFoundException("Video file not found.", definition.Path);
            }

            _host.EnsureCreated();
            _maxRenderHeight = options.MaxRenderHeight;
            SetMaxFpsInternal(options.MaxFps);

            _mediaPlayer.Close();
            _image.Stretch = ToStretch(fit);
            _mediaPlayer.IsMuted = volume <= 0;
            _mediaPlayer.Volume = Math.Clamp(volume, 0, 1);

            _host.SetContent(_viewbox);
            _videoDrawing.Player = _mediaPlayer;
            _mediaPlayer.Open(new Uri(Path.GetFullPath(definition.Path)));
            AppLogger.Info($"Loading video: {definition.Path}");

            _pendingPlay = true;
            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                UpdateVideoRect();
                ApplyRenderScale();
                StartPlayback();
            }

            await Task.CompletedTask;
        });
    }

    public void SetMaxFps(int maxFps)
    {
        UiDispatcher.Run(() =>
        {
            SetMaxFpsInternal(maxFps);
            if (_isPlaying)
            {
                StartPlayback();
            }
        });
    }

    public void NotifyPausedState(bool paused)
    {
    }

    public void Play()
    {
        UiDispatcher.Run(() =>
        {
            _pendingPlay = true;
            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                StartPlayback();
            }
        });
    }

    public void Pause()
    {
        UiDispatcher.Run(() =>
        {
            _pendingPlay = false;
            StopFrameLimiter();
            _mediaPlayer.Pause();
            _isPlaying = false;
        });
    }

    public void Stop()
    {
        UiDispatcher.Run(() =>
        {
            _pendingPlay = false;
            StopFrameLimiter();
            _mediaPlayer.Pause();
            _mediaPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
        });
    }

    public void Resize(DisplayMonitor monitor)
    {
        UiDispatcher.Run(() =>
        {
            _viewbox.Width = monitor.Width;
            _viewbox.Height = monitor.Height;
            UpdateVideoRect();
            ApplyRenderScale();
            _host.Resize(monitor);
        });
    }

    private void SetMaxFpsInternal(int maxFps)
    {
        _maxFps = Math.Clamp(maxFps, 15, 120);
        EnsureFrameLimiterConfigured();
    }

    private void ApplyRenderScale()
    {
        if (_maxRenderHeight <= 0 || _mediaPlayer.NaturalVideoHeight <= 0)
        {
            _viewbox.MaxHeight = double.PositiveInfinity;
            return;
        }

        if (_mediaPlayer.NaturalVideoHeight <= _maxRenderHeight)
        {
            _viewbox.MaxHeight = double.PositiveInfinity;
            return;
        }

        var scale = (double)_maxRenderHeight / _mediaPlayer.NaturalVideoHeight;
        _viewbox.MaxHeight = _host.Monitor.Height * scale;
        AppLogger.Info($"Video render cap: max height {_maxRenderHeight}px (scale {scale:P0})");
    }

    private void EnsureFrameLimiterConfigured()
    {
        StopFrameLimiter();
        if (!VideoPlaybackMode.UsesThrottledPlayback(_maxFps))
        {
            return;
        }

        _frameTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps)
        };
        _frameTimer.Tick += OnThrottledFrameTick;
    }

    private void StartPlayback()
    {
        _isPlaying = true;
        _pendingPlay = false;
        StopFrameLimiter();
        EnsureFrameLimiterConfigured();

        if (VideoPlaybackMode.UsesThrottledPlayback(_maxFps))
        {
            StartThrottledPlayback();
            return;
        }

        _mediaPlayer.ScrubbingEnabled = false;
        _mediaPlayer.Play();
    }

    private void StartThrottledPlayback()
    {
        _mediaPlayer.ScrubbingEnabled = true;
        _mediaPlayer.Pause();
        if (_mediaPlayer.Position != TimeSpan.Zero)
        {
            _mediaPlayer.Position = TimeSpan.Zero;
        }

        _frameTimer?.Start();
        AdvanceThrottledFrame();
    }

    private void OnThrottledFrameTick(object? sender, EventArgs e) => AdvanceThrottledFrame();

    private void AdvanceThrottledFrame()
    {
        if (!_isPlaying || !_mediaPlayer.NaturalDuration.HasTimeSpan)
        {
            return;
        }

        var frameTime = TimeSpan.FromSeconds(1.0 / _maxFps);
        var next = _mediaPlayer.Position + frameTime;
        if (next >= _mediaPlayer.NaturalDuration.TimeSpan)
        {
            next = TimeSpan.Zero;
        }

        _mediaPlayer.Position = next;
    }

    private void OnMediaEnded()
    {
        _mediaPlayer.Position = TimeSpan.Zero;
        if (!_isPlaying)
        {
            return;
        }

        if (VideoPlaybackMode.UsesThrottledPlayback(_maxFps))
        {
            AdvanceThrottledFrame();
        }
        else
        {
            _mediaPlayer.Play();
        }
    }

    private void StopFrameLimiter()
    {
        if (_frameTimer == null)
        {
            return;
        }

        _frameTimer.Tick -= OnThrottledFrameTick;
        _frameTimer.Stop();
    }

    private void UpdateVideoRect()
    {
        if (_mediaPlayer.NaturalVideoWidth > 0 && _mediaPlayer.NaturalVideoHeight > 0)
        {
            _videoDrawing.Rect = new Rect(
                0,
                0,
                _mediaPlayer.NaturalVideoWidth,
                _mediaPlayer.NaturalVideoHeight);
        }
    }

    private static Stretch ToStretch(FitMode fit) => fit switch
    {
        FitMode.Fit => Stretch.Uniform,
        FitMode.Stretch => Stretch.Fill,
        _ => Stretch.UniformToFill
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UiDispatcher.Run(() =>
        {
            StopFrameLimiter();
            _frameTimer = null;
            _mediaPlayer.Pause();
            _mediaPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            _mediaPlayer.Close();
            _videoDrawing.Player = null;
        });
    }
}
