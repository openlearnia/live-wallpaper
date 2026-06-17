using System.IO;
using System.Windows;
using System.Windows.Controls;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;
using Microsoft.Web.WebView2.Wpf;

namespace LiveWallpaper.Players;

public sealed class WebWallpaperPlayer : IWallpaperPlayer
{
    private readonly WallpaperHostWindow _host;
    private readonly Grid _root = new();
    private WebView2? _webView;
    private bool _disposed;
    private bool _isPlaying;
    private int _maxFps = 30;
    private bool _fpsScriptRegistered;
    private bool _paused;

    public WebWallpaperPlayer(WallpaperHostWindow host)
    {
        _host = host;
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
            if (definition.Type != WallpaperType.Web)
            {
                throw new InvalidOperationException("Invalid wallpaper type for web player.");
            }

            _maxFps = Math.Clamp(options.MaxFps, 15, 120);
            DisposeWebView();

            _host.EnsureCreated();
            _webView = new WebView2();
            _root.Children.Clear();
            _root.Children.Add(_webView);
            _host.SetContent(_root);

            await _webView.EnsureCoreWebView2Async();
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            await EnsureBridgeScriptAsync(_paused);

            if (!string.IsNullOrWhiteSpace(definition.Url))
            {
                _webView.Source = new Uri(definition.Url);
            }
            else if (!string.IsNullOrWhiteSpace(definition.Path))
            {
                var path = definition.Path;
                if (File.Exists(path))
                {
                    _webView.Source = new Uri(Path.GetFullPath(path));
                }
                else if (Directory.Exists(path))
                {
                    var index = Path.Combine(path, "index.html");
                    if (!File.Exists(index))
                    {
                        throw new FileNotFoundException("index.html not found in web wallpaper folder.", index);
                    }

                    _webView.Source = new Uri(index);
                }
                else
                {
                    throw new FileNotFoundException("Web wallpaper path not found.", path);
                }
            }
            else
            {
                throw new InvalidOperationException("Web wallpaper requires a URL or local path.");
            }

            AppLogger.Info($"Web wallpaper loaded: {definition.Path ?? definition.Url}");
        });
    }

    public void SetMaxFps(int maxFps)
    {
        UiDispatcher.Run(() =>
        {
            _maxFps = Math.Clamp(maxFps, 15, 120);
            if (_webView?.CoreWebView2 == null)
            {
                return;
            }

            _ = _webView.CoreWebView2.ExecuteScriptAsync(WebFpsThrottleScript.SetMaxFpsCall(_maxFps));
        });
    }

    public void NotifyPausedState(bool paused)
    {
        UiDispatcher.Run(() =>
        {
            _paused = paused;
            if (_webView?.CoreWebView2 == null)
            {
                return;
            }

            _ = _webView.CoreWebView2.ExecuteScriptAsync(WebFpsThrottleScript.SetPausedCall(paused));
        });
    }

    public void Play()
    {
        UiDispatcher.Run(() =>
        {
            if (_webView != null)
            {
                _webView.Visibility = Visibility.Visible;
            }

            _isPlaying = true;
            _paused = false;
            NotifyPausedState(false);
        });
    }

    public void Pause()
    {
        UiDispatcher.Run(() =>
        {
            if (_webView != null)
            {
                _webView.Visibility = Visibility.Collapsed;
            }

            _isPlaying = false;
            _paused = true;
            NotifyPausedState(true);
        });
    }

    public void Stop()
    {
        UiDispatcher.Run(() =>
        {
            if (_webView?.CoreWebView2 != null)
            {
                try
                {
                    _webView.CoreWebView2.Navigate("about:blank");
                }
                catch
                {
                }
            }

            _isPlaying = false;
        });
    }

    public void Resize(DisplayMonitor monitor) => _host.Resize(monitor);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UiDispatcher.Run(DisposeWebView);
    }

    private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        AppLogger.Info($"Web wallpaper message: {e.TryGetWebMessageAsString()}");
    }

    private async Task EnsureBridgeScriptAsync(bool paused)
    {
        if (_webView?.CoreWebView2 == null)
        {
            return;
        }

        if (!_fpsScriptRegistered)
        {
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                WebFpsThrottleScript.ForMaxFps(_maxFps, paused));
            _fpsScriptRegistered = true;
            return;
        }

        await _webView.CoreWebView2.ExecuteScriptAsync(WebFpsThrottleScript.SetMaxFpsCall(_maxFps));
        await _webView.CoreWebView2.ExecuteScriptAsync(WebFpsThrottleScript.SetPausedCall(paused));
    }

    private void DisposeWebView()
    {
        if (_webView == null)
        {
            return;
        }

        var view = _webView;
        _webView = null;
        _fpsScriptRegistered = false;
        _root.Children.Clear();

        try
        {
            if (view.CoreWebView2 != null)
            {
                view.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                view.CoreWebView2.Stop();
            }

            view.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to dispose WebView2", ex);
        }
    }
}
