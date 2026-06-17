using System.Runtime.InteropServices;
using System.Threading;
using LiveWallpaper.Desktop;

namespace LiveWallpaper.Core;

public sealed class SingleInstanceManager : IDisposable
{
    private Mutex? _mutex;
    public bool IsFirstInstance { get; private set; }

    public bool TryAcquire(string appName = "LiveWallpaper")
    {
        _mutex = new Mutex(true, $"Global\\{appName}_SingleInstance", out var createdNew);
        IsFirstInstance = createdNew;
        return createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
    }
}

public static class SystemWallpaper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    public static void Restore(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return;
        }

        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, Path.GetFullPath(imagePath), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }
}
