namespace LiveWallpaper.Core.Models;

public enum PowerProfile
{
    Custom,
    BatterySaver,
    Balanced,
    Performance
}

public static class PowerProfileDefaults
{
    public static int GetMaxFps(PowerProfile profile) => profile switch
    {
        PowerProfile.BatterySaver => 24,
        PowerProfile.Balanced => 30,
        PowerProfile.Performance => 60,
        _ => 30
    };

    public static PowerProfile DetectProfile(int maxFps) => maxFps switch
    {
        24 => PowerProfile.BatterySaver,
        30 => PowerProfile.Balanced,
        60 => PowerProfile.Performance,
        _ => PowerProfile.Custom
    };
}
