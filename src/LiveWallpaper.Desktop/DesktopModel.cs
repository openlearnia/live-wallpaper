namespace LiveWallpaper.Desktop;

public enum DesktopModel
{
    Legacy,
    Raised
}

public sealed class DesktopHandles
{
    public required IntPtr Progman { get; init; }
    public required IntPtr ShellDefView { get; init; }
    public IntPtr WorkerW { get; init; }
    public IntPtr IconHost { get; init; }
    public required DesktopModel Model { get; init; }
}
