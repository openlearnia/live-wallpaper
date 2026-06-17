using System.Runtime.InteropServices;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Core;

public sealed class PauseRuleEngine : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private DateTime _lastInput = DateTime.UtcNow;
    private bool _disposed;

    public bool OnFullscreen { get; set; } = true;
    public bool OnBattery { get; set; }
    public int IdleMinutes { get; set; }
    public bool ManuallyPaused { get; set; }

    public event EventHandler<bool>? PauseStateChanged;

    public bool IsPaused { get; private set; }

    public PauseRuleEngine()
    {
        _timer = new System.Threading.Timer(_ => Evaluate(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void NotifyUserActivity()
    {
        _lastInput = DateTime.UtcNow;
    }

    public void Evaluate()
    {
        var shouldPause = ManuallyPaused
            || (OnBattery && IsOnBattery())
            || (IdleMinutes > 0 && (DateTime.UtcNow - _lastInput).TotalMinutes >= IdleMinutes)
            || (OnFullscreen && IsAnyMonitorFullscreen());

        if (shouldPause != IsPaused)
        {
            IsPaused = shouldPause;
            PauseStateChanged?.Invoke(this, shouldPause);
        }
    }

    private static bool IsOnBattery()
    {
        try
        {
            var status = System.Windows.Forms.SystemInformation.PowerStatus;
            return status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAnyMonitorFullscreen()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        if (DesktopWindowClassifier.IsDesktopShellWindow(foreground))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(foreground, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorEnumerator.FromWindow(foreground);
        if (monitor == null)
        {
            return false;
        }

        var coversWidth = windowRect.Width >= monitor.Width - 8;
        var coversHeight = windowRect.Height >= monitor.Height - 8;
        var nearOrigin = Math.Abs(windowRect.Left - monitor.X) <= 8 && Math.Abs(windowRect.Top - monitor.Y) <= 8;

        return coversWidth && coversHeight && nearOrigin;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Dispose();
        _disposed = true;
    }
}
