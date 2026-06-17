using System.IO;
using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Tests;

public class LibraryServiceTests
{
    [Fact]
    public void RemoveFromLibrary_RemovesMetadataAndExcludesFromRescan()
    {
        var metadata = new LibraryMetadata
        {
            Entries =
            [
                new LibraryEntry { Path = @"C:\wallpapers\clip.mp4", Type = WallpaperType.Video },
                new LibraryEntry { Path = @"C:\wallpapers\other.mp4", Type = WallpaperType.Video }
            ]
        };

        Assert.True(LibraryService.RemoveFromLibrary(metadata, @"C:\wallpapers\clip.mp4"));
        Assert.Single(metadata.Entries);
        Assert.Contains(metadata.ExcludedPaths, p =>
            string.Equals(Path.GetFullPath(p), @"C:\wallpapers\clip.mp4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemoveFromLibrary_ReturnsFalse_WhenMissing()
    {
        var metadata = new LibraryMetadata();
        Assert.False(LibraryService.RemoveFromLibrary(metadata, @"C:\missing.mp4"));
    }
}
