namespace LiveWallpaper.Desktop;

public static class DesktopWindowClassifier
{
    public static bool IsDesktopShellWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var className = NativeMethods.GetClassName(hwnd);
        if (className is "Progman" or "WorkerW" or "SHELLDLL_DefView" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return true;
        }

        return NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
    }
}
