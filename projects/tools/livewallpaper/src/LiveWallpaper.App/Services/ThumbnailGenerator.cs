using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.App.Services;

public sealed class ThumbnailGenerator
{
    private readonly string _cacheDir;

    public ThumbnailGenerator()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDir = Path.Combine(appData, "LiveWallpaper", "thumbs");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<ImageSource?> GetThumbnailAsync(LibraryEntry entry, CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachePath(entry.Path);
        if (File.Exists(cachePath))
        {
            return await LoadImageAsync(cachePath, cancellationToken);
        }

        try
        {
            var generated = entry.Type switch
            {
                WallpaperType.Video => await GenerateVideoThumbnailAsync(entry.Path, cachePath, cancellationToken),
                WallpaperType.Web => GeneratePlaceholder(WallpaperType.Web),
                _ when Directory.Exists(entry.Path) => GeneratePlaceholder(WallpaperType.Slideshow),
                _ => await GenerateImageThumbnailAsync(entry.Path, cachePath, cancellationToken)
            };

            if (generated != null && File.Exists(cachePath))
            {
                return await LoadImageAsync(cachePath, cancellationToken);
            }

            return generated;
        }
        catch
        {
            return GeneratePlaceholder(entry.Type);
        }
    }

    private static string GetCachePath(string path)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path)))).ToLowerInvariant();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "LiveWallpaper", "thumbs", $"{hash}.jpg");
    }

    private static async Task<ImageSource?> LoadImageAsync(string path, CancellationToken cancellationToken)
    {
        return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 192;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return (ImageSource)bitmap;
        }, System.Windows.Threading.DispatcherPriority.Background, cancellationToken);
    }

    private static async Task<ImageSource?> GenerateImageThumbnailAsync(string path, string cachePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return GeneratePlaceholder(WallpaperType.Slideshow);
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 192;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            SaveJpeg(bitmap, cachePath);
        }, System.Windows.Threading.DispatcherPriority.Background, cancellationToken);

        return null;
    }

    private static async Task<ImageSource?> GenerateVideoThumbnailAsync(string path, string cachePath, CancellationToken cancellationToken)
    {
        return await System.Windows.Application.Current.Dispatcher.InvokeAsync<ImageSource?>(() =>
        {
            if (!File.Exists(path))
            {
                return GeneratePlaceholder(WallpaperType.Video);
            }

            var player = new MediaPlayer();
            var drawing = new VideoDrawing { Player = player };
            var drawingImage = new DrawingImage(drawing);
            var image = new System.Windows.Controls.Image { Source = drawingImage, Width = 192, Height = 108 };
            var loaded = false;
            player.MediaOpened += (_, _) =>
            {
                drawing.Rect = new Rect(0, 0, player.NaturalVideoWidth, player.NaturalVideoHeight);
                player.Position = TimeSpan.FromSeconds(1);
                player.Pause();
                image.Measure(new System.Windows.Size(192, 108));
                image.Arrange(new Rect(0, 0, 192, 108));
                var rtb = new RenderTargetBitmap(192, 108, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(image);
                SaveJpeg(rtb, cachePath);
                loaded = true;
                player.Close();
            };
            player.Open(new Uri(path));
            return loaded ? null : GeneratePlaceholder(WallpaperType.Video);
        }, System.Windows.Threading.DispatcherPriority.Background, cancellationToken);
    }

    private static ImageSource GeneratePlaceholder(WallpaperType type)
    {
        var color = type switch
        {
            WallpaperType.Video => Colors.SteelBlue,
            WallpaperType.Web => Colors.MediumPurple,
            _ => Colors.DarkSeaGreen
        };

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 192, 108));
            var text = new FormattedText(
                type.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                14,
                System.Windows.Media.Brushes.White,
                1.0);
            dc.DrawText(text, new System.Windows.Point(8, 40));
        }

        var bmp = new RenderTargetBitmap(192, 108, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static void SaveJpeg(BitmapSource source, string path)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
