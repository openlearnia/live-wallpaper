using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiveWallpaper.Desktop;

public sealed class WallpaperHostWindow : IDisposable
{
    private readonly DisplayMonitor _monitor;
    private readonly Grid _root;
    private Window? _window;
    private HwndSource? _source;
    private DesktopHandles? _desktop;
    private bool _disposed;

    public WallpaperHostWindow(DisplayMonitor monitor)
    {
        _monitor = monitor;
        _root = new Grid
        {
            Background = Brushes.Black,
            Width = monitor.Width,
            Height = monitor.Height
        };
    }

    public IntPtr Handle
    {
        get
        {
            if (_window != null)
            {
                return new WindowInteropHelper(_window).Handle;
            }

            return _source?.Handle ?? IntPtr.Zero;
        }
    }

    public DisplayMonitor Monitor => _monitor;

    public event EventHandler? DesktopInvalidated;

    public void SetContent(UIElement content)
    {
        _root.Children.Clear();
        if (content is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;
            fe.VerticalAlignment = VerticalAlignment.Stretch;
            fe.Width = _monitor.Width;
            fe.Height = _monitor.Height;
        }

        _root.Children.Add(content);
        _root.UpdateLayout();
    }

    public void EnsureCreated()
    {
        if (_desktop != null && DesktopWindowFinder.IsDesktopValid(_desktop) && HasValidHandle())
        {
            if (IsAttachedToDesktop())
            {
                RefreshZOrder();
                return;
            }

            AttachToDesktop();
            return;
        }

        _desktop = DesktopWindowFinder.Discover();
        CreateHostWindow();
        AttachToDesktop();
        DesktopLog.Info($"Attached wallpaper host HWND=0x{Handle:X} to {DescribeAttachment()}");
    }

    public void RefreshZOrder()
    {
        if (!HasValidHandle())
        {
            EnsureCreated();
            return;
        }

        if (_desktop == null || !DesktopWindowFinder.IsDesktopValid(_desktop))
        {
            try
            {
                _desktop = DesktopWindowFinder.Discover();
                AttachToDesktop();
            }
            catch (Exception ex)
            {
                DesktopLog.Info($"Desktop refresh failed: {ex.Message}");
                DesktopInvalidated?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (!IsAttachedToDesktop())
        {
            AttachToDesktop();
            if (!IsAttachedToDesktop())
            {
                DesktopInvalidated?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        AttachToDesktop();
        if (!HasValidHandle() || !IsAttachedToDesktop())
        {
            DesktopInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Resize(DisplayMonitor monitor)
    {
        _root.Width = monitor.Width;
        _root.Height = monitor.Height;

        if (_window != null)
        {
            _window.Width = monitor.Width;
            _window.Height = monitor.Height;
        }

        if (!HasValidHandle())
        {
            return;
        }

        var (x, y) = GetChildPosition();
        NativeMethods.MoveWindow(Handle, x, y, monitor.Width, monitor.Height, true);
    }

    private bool HasValidHandle()
    {
        var handle = Handle;
        return handle != IntPtr.Zero && NativeMethods.IsWindow(handle);
    }

    private bool IsAttachedToDesktop()
    {
        if (_desktop == null || !HasValidHandle())
        {
            return false;
        }

        var parent = NativeMethods.GetParent(Handle);
        var expectedParent = _desktop.Model == DesktopModel.Raised
            ? _desktop.Progman
            : _desktop.WorkerW;

        return expectedParent != IntPtr.Zero && parent == expectedParent;
    }

    private void CloseHostWindow()
    {
        _root.Children.Clear();
        if (_window != null)
        {
            _window.Content = null;
            _window.Close();
            _window = null;
        }

        _source?.Dispose();
        _source = null;
    }

    private void CreateHostWindow()
    {
        var content = _root.Children.Count > 0 ? _root.Children[0] as UIElement : null;
        CloseHostWindow();

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = _monitor.X,
            Top = _monitor.Y,
            Width = _monitor.Width,
            Height = _monitor.Height,
            Background = Brushes.Black,
            Content = _root,
            Visibility = Visibility.Visible
        };

        _window.Show();

        var handle = Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create wallpaper host window.");
        }

        var exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, exStyle | (int)NativeMethods.WS_EX_TOOLWINDOW);

        if (content != null)
        {
            SetContent(content);
        }
    }

    private void AttachToDesktop()
    {
        if (_desktop == null || !HasValidHandle())
        {
            return;
        }

        var handle = Handle;
        var (x, y) = GetChildPosition();

        if (_desktop.Model == DesktopModel.Raised)
        {
            NativeMethods.SetParent(handle, _desktop.Progman);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_STYLE,
                unchecked((int)(NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS)));
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_EXSTYLE,
                NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE) | (int)NativeMethods.WS_EX_LAYERED);
            NativeMethods.SetLayeredWindowAttributes(handle, 0, 255, NativeMethods.LWA_ALPHA);

            NativeMethods.SetWindowPos(
                handle,
                _desktop.ShellDefView,
                x,
                y,
                _monitor.Width,
                _monitor.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            if (_desktop.WorkerW != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    _desktop.WorkerW,
                    handle,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }
        else
        {
            var wallpaperWorker = _desktop.WorkerW;
            if (wallpaperWorker == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not find legacy wallpaper WorkerW.");
            }

            var iconHost = _desktop.IconHost != IntPtr.Zero
                ? _desktop.IconHost
                : DesktopWindowFinder.FindIconHostWindow();

            NativeMethods.SetParent(handle, wallpaperWorker);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GWL_STYLE,
                unchecked((int)(NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPSIBLINGS)));

            if (iconHost != IntPtr.Zero && wallpaperWorker != iconHost)
            {
                NativeMethods.SetWindowPos(
                    wallpaperWorker,
                    iconHost,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                x,
                y,
                _monitor.Width,
                _monitor.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.ShowWindow(wallpaperWorker, 5);
        }
    }

    private string DescribeAttachment()
    {
        if (_desktop == null)
        {
            return "unknown";
        }

        return _desktop.Model == DesktopModel.Raised
            ? $"Progman (below DefView 0x{_desktop.ShellDefView:X})"
            : $"WorkerW 0x{_desktop.WorkerW:X} (behind icon host 0x{_desktop.IconHost:X})";
    }

    private (int X, int Y) GetChildPosition() => (0, 0);

    private void Recreate()
    {
        var content = _root.Children.Count > 0 ? _root.Children[0] as UIElement : null;
        _root.Children.Clear();
        CloseHostWindow();
        _desktop = DesktopWindowFinder.Discover();
        CreateHostWindow();
        AttachToDesktop();
        if (content != null)
        {
            SetContent(content);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseHostWindow();
        _disposed = true;
    }
}
