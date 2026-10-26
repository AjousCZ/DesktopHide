using System.Drawing;
using System.Windows.Forms;

namespace DesktopHide;

internal sealed class SaverOverlay : Form
{
    private const int WM_SETCURSOR = 0x0020;
    private const int WM_MOUSEMOVE = 0x0200;
    private static readonly Cursor InvisibleCursor = CreateInvisibleCursor();
    private Bitmap? wallpaper;
    private int dimPercent;

    internal static void ApplyInvisibleCursor()
    {
        if (InvisibleCursor != Cursors.Default)
            NativeMethods.SetCursor(InvisibleCursor.Handle);
    }

    private static Cursor CreateInvisibleCursor()
    {
        // One transparent monochrome pixel: AND=1, XOR=0.
        var handle = NativeMethods.CreateCursor(IntPtr.Zero, 0, 0, 1, 1, new byte[] { 0xFF, 0 }, new byte[] { 0, 0 });
        return handle == IntPtr.Zero ? Cursors.Default : new Cursor(handle);
    }

    public SaverOverlay(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        Opacity = 0;
        DoubleBuffered = true;
        ShowInTaskbar = false;
        TopMost = true;
        Cursor = InvisibleCursor;
    }

    public void SetWallpaper(string path)
    {
        using var source = Image.FromFile(path);
        var next = new Bitmap(source);
        var old = wallpaper;
        wallpaper = next;
        BackgroundImage = next;
        BackgroundImageLayout = ImageLayout.Zoom;
        old?.Dispose();
        UpdateDim();
        Invalidate();
    }

    public void SetDimPercent(int percent)
    {
        dimPercent = Math.Clamp(percent, 0, 100);
        UpdateDim();
        Invalidate();
    }

    public void ClearWallpaper()
    {
        BackgroundImage = null;
        BackgroundImageLayout = ImageLayout.None;
        wallpaper?.Dispose();
        wallpaper = null;
        UpdateDim();
        Invalidate();
    }

    private void UpdateDim()
    {
        // With a personal wallpaper the form itself stays opaque and paints a
        // translucent black layer. Otherwise its opacity darkens the desktop.
        Opacity = wallpaper is not null ? 1 : dimPercent == 0 ? 0.01 : dimPercent / 100d;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (wallpaper is null || dimPercent == 0) return;
        using var shade = new SolidBrush(Color.FromArgb((int)Math.Round(dimPercent * 2.55), Color.Black));
        e.Graphics.FillRectangle(shade, ClientRectangle);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000080 | 0x08000000; // tool window, no activate
            return parameters;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg is WM_SETCURSOR or WM_MOUSEMOVE)
        {
            ApplyInvisibleCursor();
            m.Result = m.Msg == WM_SETCURSOR ? new IntPtr(1) : IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) wallpaper?.Dispose();
        wallpaper = null;
        base.Dispose(disposing);
    }
}

