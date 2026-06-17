namespace LiveWallpaper.Core.Models;

public sealed class PlayerLoadOptions
{
    public int MaxFps { get; init; } = 30;
    public int MaxRenderHeight { get; init; }
    public VideoBackend VideoBackend { get; init; } = VideoBackend.Wpf;
}
