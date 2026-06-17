using LiveWallpaper.Desktop;

namespace LiveWallpaper.Tests;

public class DesktopWindowClassifierTests
{
    [Fact]
    public void IsDesktopShellWindow_ReturnsFalse_ForZeroHandle()
    {
        Assert.False(DesktopWindowClassifier.IsDesktopShellWindow(IntPtr.Zero));
    }
}
