using System.IO;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Core;

public sealed class LibraryService
{
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".wmv", ".avi", ".mkv" };

    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };

    public IReadOnlyList<LibraryEntry> ScanAndMerge(string rootPath, LibraryMetadata metadata, LibrarySortMode sort = LibrarySortMode.Name)
    {
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<LibraryEntry>();
        }

        var existing = metadata.Entries.ToDictionary(
            e => Path.GetFullPath(e.Path),
            e => e,
            StringComparer.OrdinalIgnoreCase);

        var excluded = new HashSet<string>(
            metadata.ExcludedPaths.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        var discovered = new List<LibraryEntry>();
        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (excluded.Contains(full))
            {
                continue;
            }

            var ext = Path.GetExtension(file);
            if (!VideoExt.Contains(ext) && !ImageExt.Contains(ext) &&
                !ext.Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            discovered.Add(CreateEntry(file, ClassifyFile(file), existing));
        }

        foreach (var dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(dir);
            if (excluded.Contains(full))
            {
                continue;
            }

            if (!File.Exists(Path.Combine(dir, "index.html")))
            {
                continue;
            }

            discovered.Add(CreateEntry(dir, WallpaperType.Web, existing));
        }

        metadata.Entries = discovered;
        return SortEntries(discovered, sort);
    }

    public static LibraryEntry CreateEntry(string path, WallpaperType type, IReadOnlyDictionary<string, LibraryEntry> existing)
    {
        var full = Path.GetFullPath(path);
        if (existing.TryGetValue(full, out var entry))
        {
            entry.Type = type;
            if (string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                entry.DisplayName = GetDisplayName(path);
            }

            return entry;
        }

        return new LibraryEntry
        {
            Path = full,
            Type = type,
            DisplayName = GetDisplayName(path)
        };
    }

    public static WallpaperType ClassifyPath(string path)
    {
        if (Directory.Exists(path))
        {
            return WallpaperType.Web;
        }

        return ClassifyFile(path);
    }

    private static WallpaperType ClassifyFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".webm" or ".wmv" or ".avi" or ".mkv" => WallpaperType.Video,
            ".html" => WallpaperType.Web,
            _ when ImageExt.Contains(ext) => WallpaperType.Slideshow,
            _ => WallpaperType.Slideshow
        };
    }

    public static string GetDisplayName(string path) =>
        Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static IReadOnlyList<LibraryEntry> SortEntries(IEnumerable<LibraryEntry> entries, LibrarySortMode sort) =>
        sort switch
        {
            LibrarySortMode.Type => entries.OrderBy(e => e.Type).ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
            LibrarySortMode.RecentlyApplied => entries
                .OrderByDescending(e => e.LastAppliedUtc ?? DateTime.MinValue)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LibrarySortMode.FavoritesFirst => entries
                .OrderByDescending(e => e.IsFavorite)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList()
        };

    public static void MarkApplied(LibraryMetadata metadata, string path)
    {
        var full = Path.GetFullPath(path);
        var entry = metadata.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFullPath(e.Path), full, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            entry.LastAppliedUtc = DateTime.UtcNow;
        }
    }

    public static void ToggleFavorite(LibraryMetadata metadata, string path)
    {
        var full = Path.GetFullPath(path);
        var entry = metadata.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFullPath(e.Path), full, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            entry.IsFavorite = !entry.IsFavorite;
        }
    }

    public static bool RemoveFromLibrary(LibraryMetadata metadata, string path)
    {
        var full = Path.GetFullPath(path);
        var wasListed = metadata.Entries.RemoveAll(e =>
            string.Equals(Path.GetFullPath(e.Path), full, StringComparison.OrdinalIgnoreCase)) > 0;
        var alreadyExcluded = metadata.ExcludedPaths.Any(p =>
            string.Equals(Path.GetFullPath(p), full, StringComparison.OrdinalIgnoreCase));
        if (!alreadyExcluded && wasListed)
        {
            metadata.ExcludedPaths.Add(full);
        }

        return wasListed || alreadyExcluded;
    }
}
