namespace LiveWallpaper.Core.Models;

public sealed class LibraryEntry
{
    public string Path { get; set; } = string.Empty;
    public WallpaperType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
}

public sealed class LibraryMetadata
{
    public List<LibraryEntry> Entries { get; set; } = new();
    public List<string> ExcludedPaths { get; set; } = new();
}

public enum LibrarySortMode
{
    Name,
    Type,
    RecentlyApplied,
    FavoritesFirst
}
