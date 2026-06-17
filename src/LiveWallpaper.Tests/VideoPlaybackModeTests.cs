using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Tests;

public class VideoPlaybackModeTests
{
    [Theory]
    [InlineData(24, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(60, false)]
    public void UsesThrottledPlayback_MatchesThreshold(int maxFps, bool expected) =>
        Assert.Equal(expected, VideoPlaybackMode.UsesThrottledPlayback(maxFps));

    [Theory]
    [InlineData(24, "throttled")]
    [InlineData(60, "native")]
    public void DescribePlayback_ReturnsLabel(int maxFps, string expected) =>
        Assert.Equal(expected, VideoPlaybackMode.DescribePlayback(maxFps));
}
