using Microsoft.Win32;
using System.Threading;

namespace DesktopHide;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "Local\\DesktopHide.SingleInstance", out bool created);
        if (!created) return;
        ApplicationConfiguration.Initialize();
        var settings = SettingsStore.Load();
        // The startup switch keeps the launch unobtrusive; tray icon remains available.
        Application.Run(new MainForm(settings, args.Contains("--background", StringComparer.OrdinalIgnoreCase)));
    }
}

