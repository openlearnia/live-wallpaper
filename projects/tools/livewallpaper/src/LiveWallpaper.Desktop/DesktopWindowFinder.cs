namespace LiveWallpaper.Desktop;

public static class DesktopWindowFinder
{
    private const uint SPAWN_WORKER = 0x052C;
    private const int MinWallpaperArea = 100_000;
    private static bool _workerSpawned;

    public static DesktopHandles Discover(bool forceSpawnWorker = false)
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not find Progman window.");
        }

        if (forceSpawnWorker || !_workerSpawned)
        {
            SpawnWorkerW(progman);
            _workerSpawned = true;
        }

        var shellDefView = IntPtr.Zero;
        var iconHost = IntPtr.Zero;

        NativeMethods.EnumWindows((topHandle, _) =>
        {
            var shell = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell == IntPtr.Zero)
            {
                return true;
            }

            iconHost = topHandle;
            shellDefView = shell;
            return false;
        }, IntPtr.Zero);

        if (shellDefView == IntPtr.Zero)
        {
            shellDefView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellDefView != IntPtr.Zero)
            {
                iconHost = progman;
            }
        }

        if (shellDefView == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not find SHELLDLL_DefView.");
        }

        var isRaised = IsRaisedDesktop(progman, shellDefView);
        var workerW = FindWallpaperWorker(progman, iconHost, shellDefView, isRaised);

        DesktopLog.Info(
            $"Desktop: model={(isRaised ? "Raised" : "Legacy")}, " +
            $"Progman=0x{progman:X}, IconHost=0x{iconHost:X}, DefView=0x{shellDefView:X}, WorkerW=0x{workerW:X}, " +
            $"WorkerRect={DescribeRect(workerW)}");

        return new DesktopHandles
        {
            Progman = progman,
            ShellDefView = shellDefView,
            WorkerW = workerW,
            IconHost = iconHost,
            Model = isRaised ? DesktopModel.Raised : DesktopModel.Legacy
        };
    }

    public static IntPtr FindIconHostWindow()
    {
        IntPtr result = IntPtr.Zero;
        NativeMethods.EnumWindows((topHandle, _) =>
        {
            if (NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                result = topHandle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static IntPtr FindWallpaperWorker(
        IntPtr progman,
        IntPtr iconHost,
        IntPtr shellDefView,
        bool isRaised)
    {
        if (isRaised)
        {
            var child = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (child != IntPtr.Zero)
            {
                return child;
            }

            return NativeMethods.FindWindowEx(progman, shellDefView, "WorkerW", null);
        }

        if (iconHost != IntPtr.Zero)
        {
            var sibling = NativeMethods.FindWindowEx(IntPtr.Zero, iconHost, "WorkerW", null);
            if (IsUsableWallpaperWorker(sibling))
            {
                return sibling;
            }
        }

        return FindLargestEmptyWorkerW();
    }

    public static IntPtr FindTopLevelWorkerWWithoutDefView() => FindLargestEmptyWorkerW();

    private static IntPtr FindLargestEmptyWorkerW()
    {
        IntPtr best = IntPtr.Zero;
        var bestArea = 0;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (NativeMethods.GetClassName(hWnd) != "WorkerW")
            {
                return true;
            }

            if (NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var area = rect.Width * rect.Height;
            if (area > bestArea)
            {
                bestArea = area;
                best = hWnd;
            }

            return true;
        }, IntPtr.Zero);

        return best;
    }

    private static bool IsUsableWallpaperWorker(IntPtr worker)
    {
        if (worker == IntPtr.Zero || !NativeMethods.IsWindow(worker))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(worker, out var rect))
        {
            return false;
        }

        return rect.Width * rect.Height >= MinWallpaperArea;
    }

    private static string DescribeRect(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return "n/a";
        }

        return $"{rect.Width}x{rect.Height}@({rect.Left},{rect.Top})";
    }

    private static void SpawnWorkerW(IntPtr progman)
    {
        NativeMethods.SendMessageTimeout(
            progman,
            SPAWN_WORKER,
            new IntPtr(0xD),
            IntPtr.Zero,
            NativeMethods.SMTO_NORMAL,
            1000,
            out _);

        NativeMethods.SendMessageTimeout(
            progman,
            SPAWN_WORKER,
            new IntPtr(0xD),
            new IntPtr(1),
            NativeMethods.SMTO_NORMAL,
            1000,
            out _);
    }

    private static bool IsRaisedDesktop(IntPtr progman, IntPtr shellDefView)
    {
        var exStyle = NativeMethods.GetWindowLong(progman, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_NOREDIRECTIONBITMAP) != 0)
        {
            return true;
        }

        return NativeMethods.GetParent(shellDefView) == progman;
    }

    public static bool IsDesktopValid(DesktopHandles handles)
    {
        if (!NativeMethods.IsWindow(handles.Progman)
            || !NativeMethods.IsWindow(handles.ShellDefView)
            || !NativeMethods.IsWindow(handles.WorkerW))
        {
            return false;
        }

        return IsUsableWallpaperWorker(handles.WorkerW);
    }

    public static void ResetWorkerSpawnState() => _workerSpawned = false;
}
