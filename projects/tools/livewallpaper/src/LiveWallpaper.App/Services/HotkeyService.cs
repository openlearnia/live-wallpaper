using System.Runtime.InteropServices;
using System.Windows.Interop;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.App.Services;

public enum HotkeyAction
{
    PauseResume = 1,
    NextWallpaper = 2,
    PreviousWallpaper = 3,
    ShowWindow = 4
}

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly Dictionary<int, HotkeyAction> _registered = new();
    private HwndSource? _source;
    private int _nextId = 100;
    private bool _disposed;

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public void Register(IntPtr hwnd, HotkeySettings settings)
    {
        UnregisterAll();
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        RegisterOne(HotkeyAction.PauseResume, settings.PauseResume);
        RegisterOne(HotkeyAction.NextWallpaper, settings.NextWallpaper);
        RegisterOne(HotkeyAction.PreviousWallpaper, settings.PreviousWallpaper);
        RegisterOne(HotkeyAction.ShowWindow, settings.ShowWindow);
    }

    private void RegisterOne(HotkeyAction action, HotkeyBinding binding)
    {
        if (_source == null)
        {
            return;
        }

        var id = _nextId++;
        if (RegisterHotKey(_source.Handle, id, binding.Modifiers, binding.Key))
        {
            _registered[id] = action;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            HotkeyPressed?.Invoke(this, action);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void UnregisterAll()
    {
        if (_source != null)
        {
            foreach (var id in _registered.Keys)
            {
                UnregisterHotKey(_source.Handle, id);
            }
        }

        _registered.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
