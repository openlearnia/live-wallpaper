using LiveWallpaper.Desktop;

namespace LiveWallpaper.Tests;

public class PauseRuleEngineDesktopShellTests
{
    [Fact]
    public void DesktopShellWindows_AreNotTreatedAsFullscreenTargets()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            Assert.True(DesktopWindowClassifier.IsDesktopShellWindow(progman));
        }

        var workerW = NativeMethods.FindWindow("WorkerW", null);
        if (workerW != IntPtr.Zero)
        {
            Assert.True(DesktopWindowClassifier.IsDesktopShellWindow(workerW));
        }
    }
}
