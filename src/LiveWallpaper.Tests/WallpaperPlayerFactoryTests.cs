using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;
using LiveWallpaper.Players;

namespace LiveWallpaper.Tests;

public class WallpaperPlayerFactoryTests
{
    [Fact]
    public void Create_RoutesPlayersByTypeAndBackend()
    {
        WpfTestApplication.Invoke(() =>
        {
            var monitor = CreateTestMonitor();
            using var host = new WallpaperHostWindow(monitor);
            var factory = new WallpaperPlayerFactory();

            using (var wpfPlayer = factory.Create(
                       WallpaperType.Video,
                       host,
                       new PlayerLoadOptions { VideoBackend = VideoBackend.Wpf, MaxFps = 30 }))
            {
                Assert.IsType<VideoWallpaperPlayer>(wpfPlayer);
            }

            using (var mpvPlayer = factory.Create(
                       WallpaperType.Video,
                       host,
                       new PlayerLoadOptions { VideoBackend = VideoBackend.Mpv, MaxFps = 30 }))
            {
                Assert.IsType<VideoWallpaperPlayer>(mpvPlayer);
            }

            using (var slideshowPlayer = factory.Create(
                       WallpaperType.Slideshow,
                       host,
                       new PlayerLoadOptions()))
            {
                Assert.IsType<SlideshowWallpaperPlayer>(slideshowPlayer);
            }

            using (var webPlayer = factory.Create(
                       WallpaperType.Web,
                       host,
                       new PlayerLoadOptions()))
            {
                Assert.IsType<WebWallpaperPlayer>(webPlayer);
            }
        });
    }

    private static DisplayMonitor CreateTestMonitor() => new()
    {
        DeviceId = @"\\.\DISPLAY_TEST",
        DisplayName = "Test",
        X = 0,
        Y = 0,
        Width = 1920,
        Height = 1080,
        IsPrimary = true
    };
}
