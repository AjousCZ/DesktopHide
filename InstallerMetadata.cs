using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace DesktopHide;

internal static class InstallerMetadata
{
    public static string? FindMsiProductCode()
    {
        using var uninstallRoot = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstallRoot is null) return null;
        foreach (var name in uninstallRoot.GetSubKeyNames())
        {
            using var entry = uninstallRoot.OpenSubKey(name);
            if (!string.Equals(entry?.GetValue("DisplayName") as string, "Desktop Hide", StringComparison.OrdinalIgnoreCase)) continue;
            var command = entry?.GetValue("UninstallString") as string;
            var match = command is null ? Match.Empty : Regex.Match(command, @"\{[0-9a-fA-F-]{36}\}");
            if (match.Success) return match.Value;
        }
        return null;
    }
}

