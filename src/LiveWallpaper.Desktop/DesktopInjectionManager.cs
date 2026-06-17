using System.Runtime.InteropServices;

namespace LiveWallpaper.Desktop;

public sealed class DesktopInjectionManager : IDisposable
{
    private readonly Dictionary<string, WallpaperHostWindow> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private IntPtr _winEventHook;
    private NativeMethods.WinEventDelegate? _winEventHandler;
    private System.Threading.Timer? _healthTimer;
    private int _refreshScheduled;
    private bool _disposed;

    public event EventHandler? DesktopStructureChanged;

    public IReadOnlyDictionary<string, WallpaperHostWindow> Hosts
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<string, WallpaperHostWindow>(_hosts);
            }
        }
    }

    public WallpaperHostWindow GetOrCreateHost(DisplayMonitor monitor)
    {
        return UiDispatcher.Run(() =>
        {
            lock (_sync)
            {
                if (_hosts.TryGetValue(monitor.DeviceId, out var existing))
                {
                    existing.EnsureCreated();
                    return existing;
                }

                var host = new WallpaperHostWindow(monitor);
                host.DesktopInvalidated += OnHostDesktopInvalidated;
                host.EnsureCreated();
                _hosts[monitor.DeviceId] = host;
                EnsureHooks();
                return host;
            }
        });
    }

    public void RefreshAll()
    {
        UiDispatcher.Run(() =>
        {
            lock (_sync)
            {
                foreach (var host in _hosts.Values)
                {
                    host.RefreshZOrder();
                }
            }
        });
    }

    public void RemoveHost(string deviceId)
    {
        UiDispatcher.Run(() =>
        {
            lock (_sync)
            {
                if (_hosts.TryGetValue(deviceId, out var host))
                {
                    host.Dispose();
                    _hosts.Remove(deviceId);
                }
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _healthTimer?.Dispose();
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }

        UiDispatcher.Run(() =>
        {
            lock (_sync)
            {
                foreach (var host in _hosts.Values)
                {
                    host.Dispose();
                }

                _hosts.Clear();
            }
        });

        _disposed = true;
    }

    private void OnHostDesktopInvalidated(object? sender, EventArgs e)
    {
        ScheduleRefresh();
        DesktopStructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureHooks()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            return;
        }

        _winEventHandler = OnWinEvent;
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY,
            NativeMethods.EVENT_OBJECT_DESTROY,
            IntPtr.Zero,
            _winEventHandler,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _healthTimer = new System.Threading.Timer(_ => ScheduleRefresh(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private void ScheduleRefresh()
    {
        if (Interlocked.CompareExchange(ref _refreshScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(750);
                UiDispatcher.BeginRun(RefreshAll);
            }
            catch (Exception ex)
            {
                DesktopLog.Info($"Desktop refresh failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshScheduled, 0);
            }
        });
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var className = NativeMethods.GetClassName(hwnd);
        if (className is "Progman" or "WorkerW" or "SHELLDLL_DefView")
        {
            ScheduleRefresh();
        }
    }
}
