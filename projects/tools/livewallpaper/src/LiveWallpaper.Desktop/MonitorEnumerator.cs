using System.Runtime.InteropServices;

namespace LiveWallpaper.Desktop;

public static class MonitorEnumerator
{
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    public static IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var monitors = new List<DisplayMonitor>();
        var index = 0;

        bool Callback(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var isPrimary = (info.dwFlags & 1) != 0;
            monitors.Add(new DisplayMonitor
            {
                DeviceId = info.szDevice.TrimEnd('\0'),
                DisplayName = $"Display {++index}",
                X = info.rcMonitor.Left,
                Y = info.rcMonitor.Top,
                Width = info.rcMonitor.Width,
                Height = info.rcMonitor.Height,
                IsPrimary = isPrimary,
                Handle = hMonitor
            });

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            monitors.Add(new DisplayMonitor
            {
                DeviceId = @"\\.\DISPLAY1",
                DisplayName = "Display 1",
                X = 0,
                Y = 0,
                Width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN),
                Height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN),
                IsPrimary = true,
                Handle = IntPtr.Zero
            });
        }

        return monitors;
    }

    public static DisplayMonitor? FromPoint(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        var hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return GetMonitors().FirstOrDefault(m => m.Handle == hMonitor);
    }

    public static DisplayMonitor? FromWindow(IntPtr hwnd)
    {
        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return GetMonitors().FirstOrDefault(m => m.Handle == hMonitor);
    }
}
