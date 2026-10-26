namespace DesktopHide;

internal static class HotkeyFormatter
{
    public static string Format(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((modifiers & 0x0008) != 0) parts.Add("Win");
        string name = key switch
        {
            0x13 => "Pause", 0x1B => "Esc", 0x20 => "Mezerník", 0x09 => "Tab",
            >= 0x70 and <= 0x7B => $"F{key - 0x6F}",
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            0x6A => "Num *", 0x6B => "Num +", 0x6D => "Num -", 0x6E => "Num ,", 0x6F => "Num /",
            >= 0x60 and <= 0x69 => $"Num {key - 0x60}",
            _ => ((System.Windows.Forms.Keys)key).ToString()
        };
        parts.Add(name);
        return string.Join(" + ", parts);
    }
}

