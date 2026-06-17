using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using LiveWallpaper.App.Services;
using LiveWallpaper.Core.Models;
using Application = System.Windows.Application;

namespace LiveWallpaper.App;

public sealed class TrayIconService : IDisposable
{
    private readonly WallpaperOrchestratorService _service;
    private readonly NotifyIcon _notifyIcon;
    private ToolStripMenuItem? _pauseMenuItem;
    private ToolStripMenuItem? _currentWallpaperItem;
    private ToolStripMenuItem? _profileMenu;
    private bool _disposed;

    public TrayIconService(WallpaperOrchestratorService service)
    {
        _service = service;
        _notifyIcon = new NotifyIcon
        {
            Text = "Live Wallpaper",
            Icon = LoadTrayIcon(),
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowMainWindow());
        _pauseMenuItem = new ToolStripMenuItem(_service.Orchestrator.IsPaused ? "Resume" : "Pause", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add("Previous Wallpaper", null, async (_, _) => await CycleWallpaper(-1));
        menu.Items.Add("Next Wallpaper", null, async (_, _) => await CycleWallpaper(1));
        _currentWallpaperItem = new ToolStripMenuItem(GetCurrentWallpaperLabel()) { Enabled = false };
        menu.Items.Add(_currentWallpaperItem);
        _profileMenu = BuildProfileMenu();
        menu.Items.Add(_profileMenu);
        menu.Items.Add("-");
        menu.Items.Add("About Openlearnia", null, (_, _) => OpenlearniaInfo.ShowAbout());
        menu.Items.Add("openlearnia.com", null, (_, _) => OpenlearniaInfo.OpenWebsite());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        _service.Orchestrator.WallpaperApplied += (_, _) => RefreshCurrentWallpaperLabel();
    }

    public void Show() => _notifyIcon.Visible = true;

    public void UpdatePauseMenuLabel(bool paused)
    {
        if (_pauseMenuItem != null)
        {
            _pauseMenuItem.Text = paused ? "Resume" : "Pause";
        }
    }

    public void RefreshFromSettings()
    {
        RefreshCurrentWallpaperLabel();
        RebuildProfileMenu();
    }

    public void ShowAppliedNotification(string wallpaperName)
    {
        if (!_service.Orchestrator.Settings.ShowTrayNotifications)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "Live Wallpaper";
        _notifyIcon.BalloonTipText = $"Applied: {wallpaperName}";
        _notifyIcon.ShowBalloonTip(2500);
        RefreshCurrentWallpaperLabel();
    }

    private ToolStripMenuItem BuildProfileMenu()
    {
        var root = new ToolStripMenuItem("Power profile");
        foreach (var profile in Enum.GetValues<PowerProfile>())
        {
            if (profile == PowerProfile.Custom)
            {
                continue;
            }

            var item = new ToolStripMenuItem(profile.ToString(), null, (_, _) =>
            {
                _service.Orchestrator.SetPowerProfile(profile);
                RebuildProfileMenu();
            });
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private void RebuildProfileMenu()
    {
        if (_profileMenu == null)
        {
            return;
        }

        var current = _service.Orchestrator.Settings.PowerProfile;
        foreach (ToolStripMenuItem item in _profileMenu.DropDownItems)
        {
            item.Checked = item.Text == current.ToString();
        }
    }

    private string GetCurrentWallpaperLabel()
    {
        var deviceId = _service.Orchestrator.MonitorManager.GetMonitors().FirstOrDefault()?.DeviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            return "Current: None";
        }

        return $"Current: {_service.Orchestrator.GetCurrentWallpaperName(deviceId)}";
    }

    private void RefreshCurrentWallpaperLabel()
    {
        if (_currentWallpaperItem != null)
        {
            _currentWallpaperItem.Text = GetCurrentWallpaperLabel();
        }
    }

    private async Task CycleWallpaper(int step)
    {
        if (Application.Current.MainWindow is MainWindow window)
        {
            await window.CycleWallpaperAsync(step);
            RefreshCurrentWallpaperLabel();
        }
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
    }

    private void ShowMainWindow()
    {
        if (Application.Current.MainWindow is MainWindow window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void TogglePause()
    {
        var paused = !_service.Orchestrator.IsPaused;
        _service.Orchestrator.SetPaused(paused);
        UpdatePauseMenuLabel(paused);
        if (Application.Current.MainWindow is MainWindow window)
        {
            window.SyncPauseUi(paused);
        }
    }

    private async Task ExitAsync()
    {
        await _service.ShutdownAsync();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }
}
