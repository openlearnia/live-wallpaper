using System.IO;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(path);
        var settings = store.Load();
        Assert.Equal(3, settings.Version);
        Assert.Empty(settings.Monitors);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(path);
        var original = new AppSettings
        {
            Startup = true,
            MaxFps = 30,
            Monitors =
            [
                new MonitorSettings
                {
                    DeviceId = @"\\.\DISPLAY1",
                    Wallpaper = new WallpaperDefinition { Type = WallpaperType.Video, Path = "C:\\test.mp4" }
                }
            ]
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.True(loaded.Startup);
        Assert.Equal(30, loaded.MaxFps);
        Assert.Single(loaded.Monitors);
        Assert.Equal(WallpaperType.Video, loaded.Monitors[0].Wallpaper.Type);
    }
}
