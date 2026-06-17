namespace LiveWallpaper.Desktop;

public sealed class DisplayMonitor
{
    public required string DeviceId { get; init; }
    public required string DisplayName { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool IsPrimary { get; init; }
    public IntPtr Handle { get; init; }

    public override string ToString() => $"{DisplayName} ({Width}x{Height})";
}
