namespace LiveWallpaper.Desktop;

public static class DesktopLog
{
    public static Action<string>? Write { get; set; }

    public static void Info(string message) => Write?.Invoke(message);
}
