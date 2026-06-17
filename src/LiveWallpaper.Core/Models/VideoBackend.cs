namespace LiveWallpaper.Core.Models;

public enum VideoBackend
{
    Wpf,
    Mpv
}

public static class VideoPlaybackMode
{
    public const int ThrottledFpsThreshold = 29;

    public static bool UsesThrottledPlayback(int maxFps) => maxFps <= ThrottledFpsThreshold;

    public static string DescribePlayback(int maxFps) =>
        UsesThrottledPlayback(maxFps) ? "throttled" : "native";
}
