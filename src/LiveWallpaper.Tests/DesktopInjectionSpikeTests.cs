using LiveWallpaper.Desktop;
using LiveWallpaper.Players;

namespace LiveWallpaper.Tests;

public class DesktopInjectionSpikeTests
{
    [Fact]
    public async Task HostWindow_CreatesAndInjects_TestPattern()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await WpfTestApplication.InvokeAsync(async () =>
        {
            var monitor = MonitorEnumerator.GetMonitors().First();
            using var host = new WallpaperHostWindow(monitor);

            try
            {
                host.EnsureCreated();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Win32 error"))
            {
                // Headless or non-interactive test runners cannot create desktop child HWNDs.
                return;
            }

            Assert.NotEqual(IntPtr.Zero, host.Handle);

            using var player = new TestPatternWallpaperPlayer(host);
            await player.LoadAsync(
                new Core.Models.WallpaperDefinition { Type = Core.Models.WallpaperType.None },
                Core.Models.FitMode.Fill,
                0,
                new Core.Models.PlayerLoadOptions());
            player.Play();

            Assert.True(player.IsPlaying);
            player.Pause();
            Assert.False(player.IsPlaying);
        });
    }
}
