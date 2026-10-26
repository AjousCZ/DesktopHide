using System.Runtime.InteropServices;
using System.Text;

namespace DesktopHide;

internal static class NativeMethods
{
    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_SETCURSOR = 0x0020;
    internal const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000;
    internal const int SW_HIDE = 0, SW_SHOW = 5, SW_MINIMIZE = 6, SW_RESTORE = 9, GW_OWNER = 4, GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint CURSOR_SHOWING = 0x00000001;
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr FindWindow(string? className, string? windowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int virtualKey);
    internal static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] internal static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] internal static extern int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool show);
    [DllImport("user32.dll")] internal static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr CreateCursor(IntPtr instance, int hotSpotX, int hotSpotY, int width, int height, byte[] andMask, byte[] xorMask);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool DestroyIcon(IntPtr handle);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] internal static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public POINT minPosition, maxPosition;
        public RECT normalPosition;
        public static WINDOWPLACEMENT Create() => new() { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
    }
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
        public static CURSORINFO Create() => new() { cbSize = Marshal.SizeOf<CURSORINFO>() };
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct LASTINPUTINFO { public uint cbSize, dwTime; }

    internal static uint GetIdleMilliseconds()
    {
        var lastInput = GetLastInputTick();
        return unchecked((uint)Environment.TickCount - lastInput);
    }

    internal static uint GetLastInputTick()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? info.dwTime : unchecked((uint)Environment.TickCount);
    }

    internal static bool IsCursorVisible()
    {
        var info = CURSORINFO.Create();
        return GetCursorInfo(ref info) && (info.flags & CURSOR_SHOWING) != 0;
    }

    internal static IntPtr FindDesktopListView()
    {
        IntPtr result = IntPtr.Zero;
        void Search(IntPtr parent)
        {
            if (parent == IntPtr.Zero) return;
            var view = FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero) result = FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
        }
        Search(FindWindow("Progman", "Program Manager"));
        if (result != IntPtr.Zero) return result;
        EnumWindows((top, _) => { Search(top); return result == IntPtr.Zero; }, IntPtr.Zero);
        return result;
    }

    internal static bool SetIconsVisible(bool visible, out string error)
    {
        error = string.Empty;
        var list = FindDesktopListView();
        if (list == IntPtr.Zero) { error = "Nepodařilo se najít seznam ikon na ploše. Zkuste restartovat Průzkumníka Windows."; return false; }
        ShowWindow(list, visible ? SW_SHOW : SW_HIDE);
        if (IsWindowVisible(list) != visible) { error = "Windows odmítl změnit viditelnost ikon. Zkuste to znovu."; return false; }
        return true;
    }

    internal static bool AreIconsVisible()
    {
        var list = FindDesktopListView();
        return list == IntPtr.Zero || IsWindowVisible(list);
    }
}


