using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Players;

/// <summary>
/// Optional libmpv backend. Falls back to WPF when mpv-2.dll is not present next to the executable.
/// </summary>
public sealed class MpvWallpaperPlayer : IWallpaperPlayer
{
    private readonly VideoWallpaperPlayer _fallback;
    private readonly bool _useMpv;

    public MpvWallpaperPlayer(WallpaperHostWindow host, int maxRenderHeight = 0)
    {
        _useMpv = IsAvailable();
        _fallback = new VideoWallpaperPlayer(host, maxRenderHeight);
        if (_useMpv)
        {
            AppLogger.Info("Mpv backend selected (using WPF shim until native bindings are bundled).");
        }
    }

    public static bool IsAvailable() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "mpv-2.dll"));

    public string DeviceId => _fallback.DeviceId;
    public bool IsPlaying => _fallback.IsPlaying;

    public Task LoadAsync(
        WallpaperDefinition definition,
        FitMode fit,
        double volume,
        PlayerLoadOptions options,
        CancellationToken cancellationToken = default) =>
        _fallback.LoadAsync(definition, fit, volume, options, cancellationToken);

    public void Play() => _fallback.Play();
    public void Pause() => _fallback.Pause();
    public void Stop() => _fallback.Stop();
    public void Resize(DisplayMonitor monitor) => _fallback.Resize(monitor);
    public void SetMaxFps(int maxFps) => _fallback.SetMaxFps(maxFps);
    public void NotifyPausedState(bool paused) => _fallback.NotifyPausedState(paused);
    public void Dispose() => _fallback.Dispose();
}
