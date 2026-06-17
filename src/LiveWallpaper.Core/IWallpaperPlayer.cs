using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Core;

public interface IWallpaperPlayer : IDisposable
{
    string DeviceId { get; }
    bool IsPlaying { get; }
    Task LoadAsync(
        WallpaperDefinition definition,
        FitMode fit,
        double volume,
        PlayerLoadOptions options,
        CancellationToken cancellationToken = default);
    void Play();
    void Pause();
    void Stop();
    void Resize(DisplayMonitor monitor);
    void SetMaxFps(int maxFps);
    void NotifyPausedState(bool paused);
}

public interface IWallpaperPlayerFactory
{
    IWallpaperPlayer Create(WallpaperType type, WallpaperHostWindow host, PlayerLoadOptions options);
}
