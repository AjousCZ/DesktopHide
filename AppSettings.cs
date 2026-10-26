using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopHide;

internal sealed class AppSettings
{
    public uint HotkeyKey { get; set; } = 0x13; // Pause
    public uint HotkeyModifiers { get; set; } = 0;
    public uint SaverHotkeyKey { get; set; } = 0x53; // S
    public uint SaverHotkeyModifiers { get; set; } = 0x0003; // Ctrl+Alt
    public bool RunAtStartup { get; set; } = true;
    public bool ScreenSaverEnabled { get; set; }
    public int IdleMinutes { get; set; } = 5;
    public bool DarkenScreen { get; set; } = true;
    public int DimPercent { get; set; } = 80;
    public bool PlayMusic { get; set; }
    public string MusicFile { get; set; } = "";
    public int MusicVolume { get; set; } = 50;
    public bool PauseSaverDuringAudio { get; set; } = true;
    public string WallpaperFolder { get; set; } = "";
    public int WallpaperIntervalMinutes { get; set; } = 5;
    [JsonIgnore]
    public string HotkeyDisplay => HotkeyFormatter.Format(HotkeyModifiers, HotkeyKey);
    [JsonIgnore]
    public string SaverHotkeyDisplay => HotkeyFormatter.Format(SaverHotkeyModifiers, SaverHotkeyKey);
}

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopHide");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    public static AppSettings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            bool repaired = false;
            if (!IsUsableHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
            {
                settings.HotkeyKey = 0x13; // Pause
                settings.HotkeyModifiers = 0;
                repaired = true;
            }
            if (!IsUsableHotkey(settings.SaverHotkeyModifiers, settings.SaverHotkeyKey))
            {
                settings.SaverHotkeyKey = 0x53; // Ctrl+Alt+S
                settings.SaverHotkeyModifiers = 0x0003;
                repaired = true;
            }
            if (repaired) Save(settings);
            return settings;
        }
        catch { return new(); }
    }

    private static bool IsUsableHotkey(uint modifiers, uint key)
    {
        if (key is 0 or 0x03 or 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0x7B) return false;
        if ((modifiers & ~0x000F) != 0) return false;
        return modifiers != 0 || key == 0x13 || key is >= 0x70 and <= 0x7A;
    }
    public static void Save(AppSettings value)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}

