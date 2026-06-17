namespace LiveWallpaper.Core;

public static class AppLogger
{
    private static readonly object Lock = new();
    private static string? _logPath;

    public static void Initialize(string? appName = null)
    {
        appName ??= "LiveWallpaper";
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, appName, "logs");
        Directory.CreateDirectory(folder);
        _logPath = Path.Combine(folder, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var text = ex == null ? message : $"{message}{Environment.NewLine}{ex}";
        Write("ERROR", text);
    }

    private static void Write(string level, string message)
    {
        if (_logPath == null)
        {
            Initialize();
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (Lock)
        {
            File.AppendAllText(_logPath!, line + Environment.NewLine);
        }
    }
}
