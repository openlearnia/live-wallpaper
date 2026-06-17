using LiveWallpaper.Core.Models;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Core;

public sealed class MonitorManager
{
    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<DisplayMonitor> GetMonitors() => MonitorEnumerator.GetMonitors();

    public void Refresh()
    {
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public MonitorSettings GetOrCreateSettings(AppSettings settings, DisplayMonitor monitor)
    {
        var existing = settings.Monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            return existing;
        }

        var created = new MonitorSettings
        {
            DeviceId = monitor.DeviceId,
            Wallpaper = new WallpaperDefinition()
        };
        settings.Monitors.Add(created);
        return created;
    }

    public DisplayMonitor? FindByDeviceId(string deviceId)
    {
        return GetMonitors().FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    }
}
