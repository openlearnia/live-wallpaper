using LiveWallpaper.Core.Models;

namespace LiveWallpaper.Core;

public static class HotkeyBindingHelper
{
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWin = 0x0008;

    private static readonly Dictionary<string, int> KeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Left"] = 0x25,
        ["Right"] = 0x27,
        ["Up"] = 0x26,
        ["Down"] = 0x28,
        ["Space"] = 0x20,
        ["Enter"] = 0x0D,
        ["Tab"] = 0x09,
        ["Escape"] = 0x1B,
        ["Back"] = 0x08,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["Insert"] = 0x2D
    };

    static HotkeyBindingHelper()
    {
        for (var i = 0; i < 26; i++)
        {
            KeyNames[((char)('A' + i)).ToString()] = 0x41 + i;
        }

        for (var i = 0; i < 10; i++)
        {
            KeyNames[i.ToString()] = 0x30 + i;
        }

        for (var i = 1; i <= 12; i++)
        {
            KeyNames[$"F{i}"] = 0x70 + i - 1;
        }
    }

    public static string Format(HotkeyBinding binding)
    {
        var parts = new List<string>();
        if ((binding.Modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((binding.Modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((binding.Modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((binding.Modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(binding.Key));
        return string.Join("+", parts);
    }

    public static bool TryParse(string text, out HotkeyBinding binding)
    {
        binding = new HotkeyBinding();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var modifiers = 0;
        var keyIndex = parts.Length - 1;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!TryParseModifier(parts[i], out var mod))
            {
                return false;
            }

            modifiers |= mod;
        }

        if (!TryParseKey(parts[keyIndex], out var key))
        {
            return false;
        }

        binding.Modifiers = modifiers;
        binding.Key = key;
        return true;
    }

    private static bool TryParseModifier(string part, out int modifier)
    {
        modifier = part.ToLowerInvariant() switch
        {
            "ctrl" or "control" => ModControl,
            "shift" => ModShift,
            "alt" => ModAlt,
            "win" or "windows" => ModWin,
            _ => 0
        };

        return modifier != 0;
    }

    private static bool TryParseKey(string part, out int key)
    {
        if (KeyNames.TryGetValue(part, out key))
        {
            return true;
        }

        if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
        {
            return KeyNames.TryGetValue(part.ToUpperInvariant(), out key);
        }

        key = 0;
        return false;
    }

    private static string FormatKey(int key)
    {
        foreach (var pair in KeyNames)
        {
            if (pair.Value == key)
            {
                return pair.Key.Length == 1 ? pair.Key.ToUpperInvariant() : pair.Key;
            }
        }

        return $"0x{key:X}";
    }
}
