using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace LiveWallpaper.App;

public static class OpenlearniaInfo
{
    public const string Publisher = "Openlearnia";
    public const string WebsiteUrl = "https://openlearnia.com";
    public const string RepoUrl = "https://github.com/openlearnia/live-wallpaper";
    public const string ReleasesUrl = "https://github.com/openlearnia/live-wallpaper/releases";

    public static string GetVersionLabel()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.1.0";
        return $"v{version}";
    }

    public static void ShowAbout(Window? owner = null)
    {
        var message =
            $"Live Wallpaper {GetVersionLabel()}\n\n" +
            $"An {Publisher} project\n\n" +
            $"{WebsiteUrl}\n" +
            $"{RepoUrl}";

        if (owner != null)
        {
            System.Windows.MessageBox.Show(owner, message, "About Live Wallpaper", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show(message, "About Live Wallpaper", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public static void OpenWebsite() =>
        Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true });
}
