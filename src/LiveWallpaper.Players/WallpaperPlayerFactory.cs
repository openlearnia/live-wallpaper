using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Players;

public sealed class WallpaperPlayerFactory : IWallpaperPlayerFactory
{
    public IWallpaperPlayer Create(WallpaperType type, WallpaperHostWindow host, PlayerLoadOptions options)
    {
        return type switch
        {
            WallpaperType.Video when options.VideoBackend == VideoBackend.Mpv => CreateVideoPlayer(host, options),
            WallpaperType.Video => new VideoWallpaperPlayer(host, options.MaxRenderHeight),
            WallpaperType.Slideshow => new SlideshowWallpaperPlayer(host),
            WallpaperType.Web => new WebWallpaperPlayer(host),
            _ => throw new NotSupportedException($"Wallpaper type '{type}' is not supported.")
        };
    }

    private static IWallpaperPlayer CreateVideoPlayer(WallpaperHostWindow host, PlayerLoadOptions options)
    {
        if (MpvWallpaperPlayer.IsAvailable())
        {
            return new MpvWallpaperPlayer(host, options.MaxRenderHeight);
        }

        AppLogger.Info("Mpv backend requested but libmpv not found; using WPF video player.");
        return new VideoWallpaperPlayer(host, options.MaxRenderHeight);
    }
}
