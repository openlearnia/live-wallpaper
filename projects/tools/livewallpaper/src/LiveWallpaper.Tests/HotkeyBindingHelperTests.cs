using LiveWallpaper.Core;
using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Tests;

public class HotkeyBindingHelperTests
{
    [Fact]
    public void Format_DefaultPauseResume_MatchesExpected()
    {
        var binding = new HotkeyBinding { Modifiers = 0x0006, Key = 0x50 };
        Assert.Equal("Ctrl+Shift+P", HotkeyBindingHelper.Format(binding));
    }

    [Theory]
    [InlineData("Ctrl+Shift+P", 0x0006, 0x50)]
    [InlineData("Ctrl+Shift+Right", 0x0006, 0x27)]
    [InlineData("Alt+F4", 0x0001, 0x73)]
    public void TryParse_ValidHotkeys_RoundTrips(string text, int modifiers, int key)
    {
        Assert.True(HotkeyBindingHelper.TryParse(text, out var binding));
        Assert.Equal(modifiers, binding.Modifiers);
        Assert.Equal(key, binding.Key);
        Assert.Equal(text, HotkeyBindingHelper.Format(binding));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NotAHotkey")]
    [InlineData("Ctrl+Unknown")]
    public void TryParse_InvalidHotkeys_ReturnsFalse(string text) =>
        Assert.False(HotkeyBindingHelper.TryParse(text, out _));
}
