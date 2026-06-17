using LiveWallpaper.Desktop;

namespace LiveWallpaper.Tests;

public class DesktopWindowFinderTests
{
    [Fact]
    public void Discover_FindsProgmanAndDefView()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handles = DesktopWindowFinder.Discover();
        Assert.NotEqual(IntPtr.Zero, handles.Progman);
        Assert.NotEqual(IntPtr.Zero, handles.ShellDefView);
        Assert.True(handles.Model is DesktopModel.Legacy or DesktopModel.Raised);
    }
}
