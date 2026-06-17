using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Tests;

public class PowerProfileTests
{
    [Theory]
    [InlineData(PowerProfile.BatterySaver, 24)]
    [InlineData(PowerProfile.Balanced, 30)]
    [InlineData(PowerProfile.Performance, 60)]
    public void GetMaxFps_ReturnsPreset(PowerProfile profile, int expected)
    {
        Assert.Equal(expected, PowerProfileDefaults.GetMaxFps(profile));
    }

    [Theory]
    [InlineData(24, PowerProfile.BatterySaver)]
    [InlineData(30, PowerProfile.Balanced)]
    [InlineData(60, PowerProfile.Performance)]
    [InlineData(45, PowerProfile.Custom)]
    public void DetectProfile_MapsKnownValues(int maxFps, PowerProfile expected)
    {
        Assert.Equal(expected, PowerProfileDefaults.DetectProfile(maxFps));
    }
}
