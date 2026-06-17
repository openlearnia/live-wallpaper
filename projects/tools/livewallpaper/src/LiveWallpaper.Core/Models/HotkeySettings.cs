namespace LiveWallpaper.Core.Models;

public sealed class HotkeyBinding
{
    public int Modifiers { get; set; }
    public int Key { get; set; }
}

public sealed class HotkeySettings
{
    public HotkeyBinding PauseResume { get; set; } = new() { Modifiers = 0x0006, Key = 0x50 }; // Ctrl+Shift+P
    public HotkeyBinding NextWallpaper { get; set; } = new() { Modifiers = 0x0006, Key = 0x27 }; // Ctrl+Shift+Right
    public HotkeyBinding PreviousWallpaper { get; set; } = new() { Modifiers = 0x0006, Key = 0x25 }; // Ctrl+Shift+Left
    public HotkeyBinding ShowWindow { get; set; } = new() { Modifiers = 0x0006, Key = 0x57 }; // Ctrl+Shift+W
}
