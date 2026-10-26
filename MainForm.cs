using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace DesktopHide;

internal sealed class MainForm : Form
{
    private const int HotkeyId = 0xD701;
    private const int SaverHotkeyId = 0xD702;
    private readonly AppSettings settings;
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem trayStatus;
    private readonly ToolStripMenuItem trayStartup;
    private readonly ToolStripMenuItem traySaverToggle;
    private readonly Label stateLabel;
    private readonly Label stateDescription;
    private readonly Label hotkeyLabel;
    private readonly Label saverHotkeyLabel;
    private readonly ModernToggle startupBox;
    private readonly ModernButton toggleButton;
    private readonly ModernToggle saverBox;
    private readonly ModernComboBox idleCombo;
    private readonly ModernToggle darkenBox;
    private readonly ModernToggle musicBox;
    private readonly TextBox musicPathBox;
    private readonly ModernSlider volumeBar;
    private readonly ModernSlider dimBar;
    private readonly Label dimValueLabel;
    private readonly ModernToggle mediaPauseBox;
    private readonly TextBox wallpaperFolderBox;
    private readonly ModernComboBox wallpaperIntervalCombo;
    private readonly Button saverModeButton;
    private readonly System.Windows.Forms.Timer idleTimer = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer fadeTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer wallpaperTimer = new() { Interval = 60_000 };
    private readonly List<SaverOverlay> overlays = new();
    private readonly List<(IntPtr Handle, NativeMethods.WINDOWPLACEMENT Placement)> minimizedWindows = new();
    private readonly Icon appIcon;
    // The pointer must move away from its entry point. Measuring radius (rather
    // than summing tiny deltas) prevents sensor jitter from waking the saver.
    private const double SaverMouseWakeDistance = 48;
    private bool iconsVisible;
    private HotkeyTarget? capturingHotkey;
    private bool iconsHotkeyRegistered;
    private bool saverHotkeyRegistered;
    private bool allowClose;
    private bool saverActive;
    private bool saverWasManual;
    private bool mainWasVisible;
    private bool musicOpen;
    private NativeMethods.POINT lastSaverCursorPosition;
    private double saverMouseDistance;
    private int cursorHideCalls;
    private bool haveSaverCursorPosition;
    private bool saverDesktopIconsWereVisible;
    private bool mediaBlocked;
    private long lastMediaCheck;
    private int wallpaperMinutesElapsed;
    private int wallpaperIndex = -1;
    private uint fadeStartedAt;

    private enum HotkeyTarget { DesktopIcons, SaverMode }

    public MainForm(AppSettings appSettings, bool startHidden)
    {
        settings = appSettings;
        Text = "Desktop Hide";
        FormBorderStyle = FormBorderStyle.None;
        Width = 760; Height = 900;
        MinimumSize = new Size(700, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(12, 17, 27);
        ForeColor = Color.FromArgb(237, 241, 248);
        Font = new Font("Segoe UI", 10.5F);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Resize += (_, _) => ApplyWindowCorners();
        ShowInTaskbar = !startHidden;
        appIcon = LoadAppIcon();
        Icon = appIcon;

        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 34, 44), ForeColor = ForeColor, ShowImageMargin = false };
        trayStatus = new ToolStripMenuItem { Enabled = false };
        menu.Items.Add(trayStatus);
        traySaverToggle = new ToolStripMenuItem("Zapnout Saver Mode");
        traySaverToggle.Click += (_, _) => ToggleSaverMode();
        menu.Items.Add(traySaverToggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Skrýt ikony na ploše", null, (_, _) => SetVisible(false));
        menu.Items.Add("Zobrazit ikony na ploše", null, (_, _) => SetVisible(true));
        menu.Items.Add("Otevřít nastavení", null, (_, _) => ShowSettings());
        trayStartup = new ToolStripMenuItem("Spouštět při startu Windows") { CheckOnClick = true };
        trayStartup.Click += (_, _) => SetStartup(trayStartup.Checked, true);
        menu.Items.Add(trayStartup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Smazat aplikaci…", null, (_, _) => Uninstall());
        menu.Items.Add("Ukončit aplikaci", null, (_, _) => ExitApplication());
        tray = new NotifyIcon { Icon = appIcon, Text = "Desktop Hide", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();

        var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor };
        Controls.Add(scrollHost);
        var root = new TableLayoutPanel { Dock = DockStyle.Top, Height = 890, Padding = new Padding(22, 18, 22, 18), ColumnCount = 1, RowCount = 13, BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scrollHost.Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        var brand = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        var logo = new PictureBox { Image = LoadAppLogo(), Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 10, 0) };
        var title = new Label { Text = "Desktop Hide", Font = new Font("Segoe UI Semibold", 21F), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 3, 0, 0) };
        brand.Controls.Add(logo); brand.Controls.Add(title);
        var windowButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty };
        var closeWindowButton = new ModernButton { Text = "×", Width = 38, Height = 34, FillColor = BackColor, ForeColor = Color.FromArgb(220, 225, 234), Font = new Font("Segoe UI", 15F), Margin = Padding.Empty, CornerRadius = 10 };
        closeWindowButton.Click += (_, _) => Hide();
        var minimizeWindowButton = new ModernButton { Text = "−", Width = 38, Height = 34, FillColor = BackColor, ForeColor = Color.FromArgb(220, 225, 234), Font = new Font("Segoe UI", 12F), Margin = Padding.Empty, CornerRadius = 10 };
        minimizeWindowButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        windowButtons.Controls.Add(closeWindowButton); windowButtons.Controls.Add(minimizeWindowButton);
        header.Controls.Add(brand, 0, 0); header.Controls.Add(windowButtons, 1, 0);
        header.MouseDown += BeginWindowDrag; brand.MouseDown += BeginWindowDrag; logo.MouseDown += BeginWindowDrag; title.MouseDown += BeginWindowDrag;
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(new Label { Text = "RYCHLÉ SKRYTÍ IKON PLOCHY", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(145, 156, 174), Dock = DockStyle.Fill }, 0, 1);
        var statusSurface = new ModernCard { Dock = DockStyle.Fill, SurfaceColor = Color.FromArgb(22, 30, 44), StrokeColor = Color.FromArgb(45, 58, 79), CornerRadius = 22, Padding = new Padding(16) };
        var statusCard = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1 };
        statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62)); statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var statusBadge = new StatusIndicator { Dock = DockStyle.Fill };
        var statusText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(16, 2, 0, 0) };
        statusText.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); statusText.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stateLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 17F) };
        stateDescription = new Label { Dock = DockStyle.Fill, Text = "Stiskni klávesovou zkratku pro skrytí ikon.", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(151, 178, 215), Font = new Font("Segoe UI", 9.5F) };
        statusText.Controls.Add(stateLabel, 0, 0); statusText.Controls.Add(stateDescription, 0, 1);
        statusCard.Controls.Add(statusBadge, 0, 0); statusCard.Controls.Add(statusText, 1, 0);
        statusSurface.Controls.Add(statusCard);
        root.Controls.Add(statusSurface, 0, 2);
        toggleButton = new ModernButton { Dock = DockStyle.Fill, Primary = true, AccentColor = Color.FromArgb(67, 111, 255), ForeColor = Color.White, Text = "Skrýt ikony na ploše", Font = new Font("Segoe UI Semibold", 12.5F), CornerRadius = 17 };
        toggleButton.Click += (_, _) => Toggle();
        root.Controls.Add(toggleButton, 0, 3);
        root.Controls.Add(SectionLabel("Klávesová zkratka"), 0, 4);
        var shortcutSurface = new ModernCard { Dock = DockStyle.Fill, SurfaceColor = Color.FromArgb(22, 30, 44), StrokeColor = Color.FromArgb(45, 58, 79), CornerRadius = 17, Padding = new Padding(1) };
        var shortcutRow = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1 };
        shortcutRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shortcutRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        hotkeyLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(190, 205, 227), BackColor = Color.FromArgb(24, 31, 43), Padding = new Padding(14, 0, 0, 0), Font = new Font("Segoe UI Semibold", 12F) };
        var hotkeyButton = MakeSecondaryButton("Nastavit zkratku"); hotkeyButton.Dock = DockStyle.Fill; hotkeyButton.Click += (_, _) => BeginCapture(HotkeyTarget.DesktopIcons);
        shortcutRow.Controls.Add(hotkeyLabel, 0, 0); shortcutRow.Controls.Add(hotkeyButton, 1, 0);
        shortcutSurface.Controls.Add(shortcutRow);
        root.Controls.Add(shortcutSurface, 0, 5);
        root.Controls.Add(SectionLabel("Spouštění s Windows"), 0, 6);
        var startupSurface = new ModernCard { Dock = DockStyle.Fill, SurfaceColor = Color.FromArgb(22, 30, 44), StrokeColor = Color.FromArgb(45, 58, 79), CornerRadius = 17, Padding = new Padding(10, 4, 10, 2) };
        var startupCard = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
        startupCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); startupCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        startupBox = new ModernToggle { Text = "Spouštět při startu Windows", Checked = settings.RunAtStartup, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(218, 225, 237) };
        startupBox.CheckedChanged += (_, _) => SetStartup(startupBox.Checked, false);
        startupCard.Controls.Add(startupBox, 0, 0);
        startupCard.Controls.Add(new Label { Text = "Aplikace se po přihlášení automaticky spustí na pozadí.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(151, 178, 215), Padding = new Padding(28, 0, 0, 0) }, 0, 1);
        startupSurface.Controls.Add(startupCard);
        root.Controls.Add(startupSurface, 0, 7);
        root.Controls.Add(SectionLabel("Nastavení spořiče obrazovky"), 0, 8);
        var saverSurface = new ModernCard { Dock = DockStyle.Fill, SurfaceColor = Color.FromArgb(22, 30, 44), StrokeColor = Color.FromArgb(45, 58, 79), CornerRadius = 21, Padding = new Padding(14, 10, 14, 8) };
        var saverCard = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 1, RowCount = 12 };
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 31)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 37)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); saverCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        saverBox = new ModernToggle { Text = "Spouštět spořič obrazovky", Checked = settings.ScreenSaverEnabled, ForeColor = Color.FromArgb(218, 225, 237), Dock = DockStyle.Fill };
        var saverSubtitle = new Label { Text = "Po určité době nečinnosti se spustí spořič obrazovky.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(151, 178, 215), Padding = new Padding(28, 0, 0, 0) };
        idleCombo = new ModernComboBox { Width = 205, Height = 34, Font = new Font("Segoe UI", 10F), Margin = Padding.Empty };
        foreach (var minutes in new[] { 1, 2, 3, 5, 10, 15, 20, 30, 60 }) idleCombo.Items.Add(minutes == 1 ? "1 minuta" : minutes is >= 2 and <= 4 ? $"{minutes} minuty" : $"{minutes} minut");
        var selectedIdle = new[] { 1, 2, 3, 5, 10, 15, 20, 30, 60 }.ToList().IndexOf(settings.IdleMinutes);
        idleCombo.SelectedIndex = selectedIdle >= 0 ? selectedIdle : 3;
        var idleRow = LabeledRow("Čas nečinnosti:", idleCombo);
        darkenBox = new ModernToggle { Text = "Ztmavit plochu při spuštění", Checked = settings.DarkenScreen, ForeColor = Color.FromArgb(207, 215, 229), Dock = DockStyle.Fill };
        dimBar = new ModernSlider { Minimum = 0, Maximum = 100, Value = Math.Clamp(settings.DimPercent, 0, 100), Width = 320, Height = 38 };
        dimValueLabel = new Label { Text = $"{dimBar.Value}%", Width = 45, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(174, 188, 211) };
        var dimRow = LabeledRow("Úroveň ztmavení:", dimBar, dimValueLabel);
        musicBox = new ModernToggle { Text = "Přehrávat hudbu", Checked = settings.PlayMusic, ForeColor = Color.FromArgb(207, 215, 229), Dock = DockStyle.Fill };
        musicPathBox = new TextBox { Text = settings.MusicFile, Width = 350, ReadOnly = true, BackColor = Color.FromArgb(30, 40, 56), ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle };
        var browseButton = MakeSecondaryButton("Vybrat hudbu…"); browseButton.Width = 130; browseButton.Height = 30; browseButton.Click += (_, _) => ChooseMusic();
        var musicRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        musicRow.Controls.Add(musicPathBox); musicRow.Controls.Add(browseButton);
        volumeBar = new ModernSlider { Minimum = 0, Maximum = 100, Value = Math.Clamp(settings.MusicVolume, 0, 100), Width = 360, Height = 38 };
        var volumePercent = new Label { Text = $"{volumeBar.Value}%", Width = 45, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(174, 188, 211) };
        volumeBar.ValueChanged += (_, _) => volumePercent.Text = $"{volumeBar.Value}%";
        var volumeRow = LabeledRow("Hlasitost:", volumeBar, volumePercent);
        saverHotkeyLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(190, 205, 227), Font = new Font("Segoe UI Semibold", 10.5F) };
        var saverHotkeyButton = MakeSecondaryButton("Zkratka Saver Mode"); saverHotkeyButton.Width = 190; saverHotkeyButton.Height = 36; saverHotkeyButton.Click += (_, _) => BeginCapture(HotkeyTarget.SaverMode);
        var saverHotkeyRow = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1 };
        saverHotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); saverHotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        saverHotkeyRow.Controls.Add(saverHotkeyLabel, 0, 0); saverHotkeyRow.Controls.Add(saverHotkeyButton, 1, 0);
        mediaPauseBox = new ModernToggle { Text = "Pozastavit automatický spořič při přehrávání zvuku", Checked = settings.PauseSaverDuringAudio, ForeColor = Color.FromArgb(207, 215, 229), Dock = DockStyle.Fill };
        wallpaperFolderBox = new TextBox { Text = settings.WallpaperFolder, Width = 370, ReadOnly = true, BackColor = Color.FromArgb(30, 40, 56), ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle };
        var wallpaperBrowse = MakeSecondaryButton("Vybrat složku…"); wallpaperBrowse.Width = 130; wallpaperBrowse.Height = 30; wallpaperBrowse.Click += (_, _) => ChooseWallpaperFolder();
        var wallpaperRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        wallpaperRow.Controls.Add(new Label { Text = "Vlastní tapety:", Width = 112, Height = 34, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(174, 188, 211) });
        wallpaperRow.Controls.Add(wallpaperFolderBox); wallpaperRow.Controls.Add(wallpaperBrowse);
        wallpaperIntervalCombo = new ModernComboBox { Width = 140, Height = 34, Font = new Font("Segoe UI", 9.5F), Margin = Padding.Empty };
        foreach (var minutes in new[] { 1, 2, 3, 5, 10, 15, 30 }) wallpaperIntervalCombo.Items.Add(minutes == 1 ? "1 minuta" : minutes is >= 2 and <= 4 ? $"{minutes} minuty" : $"{minutes} minut");
        int selectedWallpaperInterval = new[] { 1, 2, 3, 5, 10, 15, 30 }.ToList().IndexOf(settings.WallpaperIntervalMinutes);
        wallpaperIntervalCombo.SelectedIndex = selectedWallpaperInterval >= 0 ? selectedWallpaperInterval : 3;
        var wallpaperIntervalRow = LabeledRow("Měnit tapetu každých:", wallpaperIntervalCombo);
        saverCard.Controls.Add(saverBox, 0, 0); saverCard.Controls.Add(saverSubtitle, 0, 1); saverCard.Controls.Add(idleRow, 0, 2); saverCard.Controls.Add(darkenBox, 0, 3);
        saverCard.Controls.Add(dimRow, 0, 4); saverCard.Controls.Add(musicBox, 0, 5); saverCard.Controls.Add(musicRow, 0, 6); saverCard.Controls.Add(volumeRow, 0, 7); saverCard.Controls.Add(saverHotkeyRow, 0, 8);
        saverCard.Controls.Add(mediaPauseBox, 0, 9); saverCard.Controls.Add(wallpaperRow, 0, 10); saverCard.Controls.Add(wallpaperIntervalRow, 0, 11);
        saverCard.Margin = Padding.Empty;
        foreach (Control child in saverCard.Controls) child.Margin = Padding.Empty;
        saverSurface.Controls.Add(saverCard);
        root.Controls.Add(saverSurface, 0, 9);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 5, 0, 0) };
        saverModeButton = new ModernButton { Text = "Zapnout Saver Mode", Width = 220, Height = 44, Primary = true, AccentColor = Color.FromArgb(65, 108, 255), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10.5F), CornerRadius = 15 };
        saverModeButton.Click += (_, _) => ToggleSaverMode();
        var uninstallButton = new ModernButton { Text = "Smazat aplikaci…", Width = 175, Height = 44, FillColor = Color.FromArgb(48, 32, 42), OutlineColor = Color.FromArgb(104, 52, 68), ForeColor = Color.FromArgb(255, 151, 161), Font = new Font("Segoe UI", 9.5F), CornerRadius = 15 };
        uninstallButton.Click += (_, _) => Uninstall();
        saverModeButton.Margin = Padding.Empty; uninstallButton.Margin = Padding.Empty; footer.Padding = Padding.Empty;
        footer.Controls.Add(saverModeButton); footer.Controls.Add(uninstallButton);
        root.Controls.Add(footer, 0, 10);
        root.Controls.Add(new Label { Text = "Desktop Hide v1.1.5                                      Vyvinuto Ajous Production", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(132, 150, 177), Font = new Font("Segoe UI", 8.5F), TextAlign = ContentAlignment.MiddleCenter }, 0, 11);
        foreach (Control child in root.Controls) child.Margin = Padding.Empty;

        saverBox.CheckedChanged += (_, _) => SaveSaverSettings();
        idleCombo.SelectedIndexChanged += (_, _) => SaveSaverSettings();
        darkenBox.CheckedChanged += (_, _) => SaveSaverSettings();
        dimBar.ValueChanged += (_, _) => { dimValueLabel.Text = $"{dimBar.Value}%"; SaveSaverSettings(); };
        musicBox.CheckedChanged += (_, _) => SaveSaverSettings();
        volumeBar.ValueChanged += (_, _) => SaveSaverSettings();
        mediaPauseBox.CheckedChanged += (_, _) => SaveSaverSettings();
        wallpaperIntervalCombo.SelectedIndexChanged += (_, _) => SaveSaverSettings();
        idleTimer.Tick += (_, _) => CheckIdleState();
        fadeTimer.Tick += (_, _) => FadeStep();
        wallpaperTimer.Tick += (_, _) => AdvanceWallpaper();
        idleTimer.Start();

        iconsVisible = NativeMethods.AreIconsVisible();
        UpdateUi();
        Shown += (_, _) => { RegisterCurrentHotkey(!startHidden); SetStartup(settings.RunAtStartup, !startHidden); if (startHidden) Hide(); };
        FormClosing += (sender, e) =>
        {
            if (!allowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            if (saverActive) ExitSaver();
            idleTimer.Stop(); idleTimer.Dispose(); fadeTimer.Stop(); fadeTimer.Dispose(); wallpaperTimer.Stop(); wallpaperTimer.Dispose();
            if (iconsHotkeyRegistered) NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            if (saverHotkeyRegistered) NativeMethods.UnregisterHotKey(Handle, SaverHotkeyId);
            tray.Visible = false; tray.Dispose(); appIcon.Dispose();
        };
        if (startHidden) { Opacity = 0; ShowInTaskbar = false; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            if (m.WParam.ToInt32() == HotkeyId) { Toggle(); return; }
            if (m.WParam.ToInt32() == SaverHotkeyId) { ToggleSaverMode(); return; }
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = ModernButton.Rounded(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 20);
        using var pen = new Pen(Color.FromArgb(52, 67, 91), 1f);
        e.Graphics.DrawPath(pen, path);
    }

    private void ApplyWindowCorners()
    {
        if (Width < 2 || Height < 2) return;
        using var path = ModernButton.Rounded(new RectangleF(0, 0, Width, Height), 20);
        Region = new Region(path);
    }

    private void Toggle() => SetVisible(!iconsVisible);
    private void SetVisible(bool visible)
    {
        if (!NativeMethods.SetIconsVisible(visible, out var error)) { MessageBox.Show(this, error, "Desktop Hide", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        iconsVisible = visible; UpdateUi();
    }
    private void UpdateUi()
    {
        stateLabel.Text = iconsVisible ? "Ikony jsou viditelné" : "Ikony jsou skryté";
        stateLabel.ForeColor = iconsVisible ? Color.FromArgb(115, 220, 166) : Color.FromArgb(255, 183, 103);
        stateDescription.Text = mediaBlocked ? "Spořič čeká, dokud přehrává zvuk jiná aplikace." : iconsVisible ? $"Stiskni {settings.HotkeyDisplay} pro skrytí ikon." : $"Stiskni {settings.HotkeyDisplay} pro zobrazení ikon.";
        toggleButton.Text = iconsVisible ? "◉   Skrýt ikony na ploše" : "◉   Zobrazit ikony na ploše";
        hotkeyLabel.Text = $"Aktuální zkratka:   {settings.HotkeyDisplay}";
        saverHotkeyLabel.Text = capturingHotkey == HotkeyTarget.SaverMode ? "Stiskněte zkratku… (Esc zruší)" : $"Aktuální zkratka:   {settings.SaverHotkeyDisplay}";
        if (capturingHotkey == HotkeyTarget.DesktopIcons) hotkeyLabel.Text = "Stiskněte zkratku… (Esc zruší)";
        trayStatus.Text = $"{(iconsVisible ? "Ikony jsou viditelné" : "Ikony jsou skryté")} · {(saverActive ? "Saver Mode je zapnutý" : "Saver Mode je vypnutý")}";
        trayStartup.Checked = settings.RunAtStartup;
        traySaverToggle.Text = saverActive ? "Vypnout Saver Mode" : "Zapnout Saver Mode";
        saverModeButton.Text = saverActive ? "Vypnout Saver Mode" : "Zapnout Saver Mode";
    }

    private static Label SectionLabel(string text) => new() { Text = text, Font = new Font("Segoe UI Semibold", 12F), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

    private ModernButton MakeSecondaryButton(string text)
    {
        return new ModernButton { Text = text, FillColor = Color.FromArgb(30, 40, 56), OutlineColor = Color.FromArgb(53, 68, 91), ForeColor = Color.FromArgb(223, 231, 245), Font = new Font("Segoe UI", 9.5F), Cursor = Cursors.Hand, CornerRadius = 13 };
    }

    private FlowLayoutPanel LabeledRow(string label, Control control, Control? suffix = null)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
        row.Controls.Add(new Label { Text = label, AutoSize = false, ForeColor = Color.FromArgb(174, 188, 211), Margin = Padding.Empty, Width = 164, Height = 34, TextAlign = ContentAlignment.MiddleLeft });
        control.Margin = Padding.Empty;
        row.Controls.Add(control);
        if (suffix is not null) row.Controls.Add(suffix);
        return row;
    }
    private void BeginCapture(HotkeyTarget target)
    {
        capturingHotkey = target; UpdateUi(); Activate(); Focus();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (capturingHotkey is null) return base.ProcessCmdKey(ref msg, keyData);
        var key = keyData & Keys.KeyCode;
        if (key == Keys.Escape) { capturingHotkey = null; UpdateUi(); return true; }
        var mods = keyData & Keys.Modifiers;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return true;
        uint vk = (uint)key;
        uint nativeMods = 0;
        if ((mods & Keys.Control) != 0) nativeMods |= NativeMethods.MOD_CONTROL;
        if ((mods & Keys.Alt) != 0) nativeMods |= NativeMethods.MOD_ALT;
        if ((mods & Keys.Shift) != 0) nativeMods |= NativeMethods.MOD_SHIFT;
        if (NativeMethods.IsKeyDown(0x5B) || NativeMethods.IsKeyDown(0x5C)) nativeMods |= NativeMethods.MOD_WIN;
        var label = capturingHotkey == HotkeyTarget.DesktopIcons ? hotkeyLabel : saverHotkeyLabel;
        if (vk == 0x7B) { label.Text = "F12 je vyhrazeno systému Windows."; return true; }
        if (nativeMods == 0 && vk != 0x13 && !(vk >= 0x70 && vk <= 0x7A))
        { label.Text = "Samostatně lze použít Pause nebo F1 až F11. Jinak přidejte Ctrl, Alt, Shift nebo Win."; return true; }
        var target = capturingHotkey.Value;
        var id = target == HotkeyTarget.DesktopIcons ? HotkeyId : SaverHotkeyId;
        var previousKey = target == HotkeyTarget.DesktopIcons ? settings.HotkeyKey : settings.SaverHotkeyKey;
        var previousMods = target == HotkeyTarget.DesktopIcons ? settings.HotkeyModifiers : settings.SaverHotkeyModifiers;
        var wasRegistered = target == HotkeyTarget.DesktopIcons ? iconsHotkeyRegistered : saverHotkeyRegistered;
        if (wasRegistered) NativeMethods.UnregisterHotKey(Handle, id);
        SetHotkeyRegistered(target, false);
        if (!TryRegister(id, nativeMods, vk, out var error))
        {
            TryRegister(id, previousMods, previousKey, out _);
            SetHotkeyRegistered(target, true);
            MessageBox.Show(this, error, "Klávesová zkratka", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            capturingHotkey = null; UpdateUi(); return true;
        }
        SetHotkeyRegistered(target, true);
        if (target == HotkeyTarget.DesktopIcons) { settings.HotkeyKey = vk; settings.HotkeyModifiers = nativeMods; }
        else { settings.SaverHotkeyKey = vk; settings.SaverHotkeyModifiers = nativeMods; }
        capturingHotkey = null;
        try { SettingsStore.Save(settings); }
        catch (Exception ex) { MessageBox.Show(this, $"Zkratka je aktivní, ale nastavení se nepodařilo uložit: {ex.Message}", "Chyba ukládání", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        UpdateUi(); return true;
    }
    private void SetHotkeyRegistered(HotkeyTarget target, bool registered)
    {
        if (target == HotkeyTarget.DesktopIcons) iconsHotkeyRegistered = registered; else saverHotkeyRegistered = registered;
    }
    private bool TryRegister(int id, uint mods, uint key, out string error)
    {
        if (NativeMethods.RegisterHotKey(Handle, id, mods | NativeMethods.MOD_NOREPEAT, key)) { error = ""; return true; }
        int code = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        error = code == 1409 ? "Tuto zkratku už používá Windows nebo jiná aplikace. Zvolte jinou kombinaci." : $"Zkratku se nepodařilo zaregistrovat (kód {code}). Zkuste jinou.";
        return false;
    }
    private void RegisterCurrentHotkey(bool showError)
    {
        var iconsOk = TryRegister(HotkeyId, settings.HotkeyModifiers, settings.HotkeyKey, out var iconsError);
        iconsHotkeyRegistered = iconsOk;
        var saverOk = TryRegister(SaverHotkeyId, settings.SaverHotkeyModifiers, settings.SaverHotkeyKey, out var saverError);
        saverHotkeyRegistered = saverOk;
        if (!iconsOk || !saverOk)
        {
            var error = $"{(!iconsOk ? $"Zkratka pro ikony: {iconsError}" : "")}{(!iconsOk && !saverOk ? "\r\n" : "")}{(!saverOk ? $"Zkratka Saver Mode: {saverError}" : "")}";
            if (showError) MessageBox.Show(this, error, "Klávesová zkratka", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else tray.ShowBalloonTip(5000, "Desktop Hide – neaktivní zkratka", error, ToolTipIcon.Warning);
        }
    }
    private void SetStartup(bool enabled, bool showError)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled) key.SetValue("DesktopHide", $"\"{Application.ExecutablePath}\" --background"); else key.DeleteValue("DesktopHide", false);
            settings.RunAtStartup = enabled; SettingsStore.Save(settings);
            if (startupBox.Checked != enabled) startupBox.Checked = enabled;
            trayStartup.Checked = enabled;
        }
        catch (Exception ex) { if (showError) MessageBox.Show(this, $"Nastavení automatického spuštění se nepodařilo uložit: {ex.Message}", "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void Uninstall()
    {
        var answer = MessageBox.Show(this,
            "Opravdu odebrat Desktop Hide? Odebere se aplikace, zástupce, automatické spouštění i její nastavení. Soubory a ikony na ploše se tím nesmažou.",
            "Smazat Desktop Hide", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopHide");
            var scriptPath = Path.Combine(Path.GetTempPath(), $"DesktopHide-remove-{Environment.ProcessId}.ps1");
            static string Ps(string value) => "'" + value.Replace("'", "''") + "'";
            var msiProduct = InstallerMetadata.FindMsiProductCode() ?? "";
            var script = $"$target = {Ps(exePath)}\r\n$settings = {Ps(appDataPath)}\r\n$msiProduct = {Ps(msiProduct)}\r\n$pidToWait = {Environment.ProcessId}\r\n" +
                "try { Wait-Process -Id $pidToWait -ErrorAction SilentlyContinue } catch {}\r\n" +
                "if ($msiProduct) { Start-Process -FilePath 'msiexec.exe' -ArgumentList \"/x $msiProduct /qn /norestart\" -Wait } else { Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue }\r\n" +
                "Remove-Item -LiteralPath $settings -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'DesktopHide' -ErrorAction SilentlyContinue\r\n" +
                "Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue\r\n";
            File.WriteAllText(scriptPath, script, System.Text.Encoding.Unicode);
            Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Aplikaci se nepodařilo odebrat: {ex.Message}", "Chyba odinstalace", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSaverSettings()
    {
        settings.ScreenSaverEnabled = saverBox.Checked;
        settings.IdleMinutes = new[] { 1, 2, 3, 5, 10, 15, 20, 30, 60 }[Math.Max(0, idleCombo.SelectedIndex)];
        settings.DarkenScreen = darkenBox.Checked;
        settings.DimPercent = dimBar.Value;
        settings.PlayMusic = musicBox.Checked;
        settings.MusicVolume = volumeBar.Value;
        settings.PauseSaverDuringAudio = mediaPauseBox.Checked;
        settings.WallpaperIntervalMinutes = new[] { 1, 2, 3, 5, 10, 15, 30 }[Math.Max(0, wallpaperIntervalCombo.SelectedIndex)];
        if (!settings.PauseSaverDuringAudio && mediaBlocked) { mediaBlocked = false; UpdateUi(); }
        try { SettingsStore.Save(settings); }
        catch (Exception ex) { MessageBox.Show(this, $"Nastavení spořiče se nepodařilo uložit: {ex.Message}", "Chyba ukládání", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        if (!settings.ScreenSaverEnabled && saverActive && !saverWasManual) ExitSaver();
        else if (saverActive) { ApplyCurrentDim(); RestartMusic(); }
    }

    private void ChooseMusic()
    {
        using var dialog = new OpenFileDialog { Title = "Vyberte hudbu pro spořič", Filter = "Zvukové soubory|*.mp3;*.wav;*.wma;*.m4a|Všechny soubory|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        musicPathBox.Text = dialog.FileName;
        settings.MusicFile = dialog.FileName;
        SaveSaverSettings();
    }

    private void ChooseWallpaperFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Vyberte složku s obrázky, které se budou střídat jako tapety Saver Mode.", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(settings.WallpaperFolder) ? settings.WallpaperFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var images = GetWallpaperFiles(dialog.SelectedPath);
        if (images.Length == 0)
        {
            MessageBox.Show(this, "Ve vybrané složce nejsou žádné podporované obrázky (JPG, PNG, BMP nebo GIF).", "Žádné tapety", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        settings.WallpaperFolder = dialog.SelectedPath;
        wallpaperFolderBox.Text = dialog.SelectedPath;
        wallpaperIndex = -1;
        SaveSaverSettings();
        if (saverActive) AdvanceWallpaper();
    }

    private static string[] GetWallpaperFiles(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        try { return Directory.EnumerateFiles(folder).Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private void AdvanceWallpaper()
    {
        if (!saverActive) { wallpaperTimer.Stop(); return; }
        wallpaperMinutesElapsed++;
        if (wallpaperMinutesElapsed < Math.Clamp(settings.WallpaperIntervalMinutes, 1, 30)) return;
        wallpaperMinutesElapsed = 0;
        var files = GetWallpaperFiles(settings.WallpaperFolder);
        if (files.Length == 0) return;
        for (int attempts = 0; attempts < files.Length; attempts++)
        {
            wallpaperIndex = (wallpaperIndex + 1) % files.Length;
            try
            {
                foreach (var overlay in overlays) overlay.SetWallpaper(files[wallpaperIndex]);
                return;
            }
            catch (Exception ex)
            {
                if (attempts == files.Length - 1)
                    tray.ShowBalloonTip(4000, "Desktop Hide – tapety", $"Obrázek se nepodařilo otevřít: {ex.Message}", ToolTipIcon.Warning);
            }
        }
    }

    private void CheckIdleState()
    {
        var idle = NativeMethods.GetIdleMilliseconds();
        if (!saverActive && settings.ScreenSaverEnabled && idle >= (uint)(settings.IdleMinutes * 60_000))
        {
            if (settings.PauseSaverDuringAudio && IsAudioPlaybackActive())
            {
                if (!mediaBlocked) { mediaBlocked = true; UpdateUi(); }
            }
            else
            {
                if (mediaBlocked) { mediaBlocked = false; UpdateUi(); }
                EnterSaver(false);
            }
        }
        else if (saverActive) CheckSaverMouseMovement();
    }

    private bool IsAudioPlaybackActive()
    {
        long now = Environment.TickCount64;
        if (now - lastMediaCheck < 1500) return mediaBlocked;
        lastMediaCheck = now;
        mediaBlocked = MediaPlaybackDetector.IsAudioPlaying();
        return mediaBlocked;
    }

    private void ToggleSaverMode()
    {
        if (saverActive) ExitSaver();
        else EnterSaver(true);
    }

    private void EnterSaver(bool manual)
    {
        if (saverActive) return;
        if (manual && mediaBlocked) mediaBlocked = false;
        saverActive = true;
        saverWasManual = manual;
        saverMouseDistance = 0;
        haveSaverCursorPosition = NativeMethods.GetCursorPos(out lastSaverCursorPosition);
        cursorHideCalls = 0;
        HideMouseCursor();
        saverDesktopIconsWereVisible = NativeMethods.AreIconsVisible();
        minimizedWindows.Clear();
        var processId = (uint)Environment.ProcessId;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
            var className = new System.Text.StringBuilder(256);
            NativeMethods.GetClassName(hwnd, className, className.Capacity);
            if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            NativeMethods.GetWindowThreadProcessId(hwnd, out var windowProcess);
            if (windowProcess == processId || ((long)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;
            var placement = NativeMethods.WINDOWPLACEMENT.Create();
            if (!NativeMethods.GetWindowPlacement(hwnd, ref placement) || placement.showCmd == 2) return true;
            minimizedWindows.Add((hwnd, placement));
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
            return true;
        }, IntPtr.Zero);

        if (saverDesktopIconsWereVisible)
        {
            if (NativeMethods.SetIconsVisible(false, out var iconError)) iconsVisible = false;
            else
            {
                iconsVisible = NativeMethods.AreIconsVisible();
                tray.ShowBalloonTip(3500, "Desktop Hide", $"Ikony plochy se nepodařilo skrýt: {iconError}", ToolTipIcon.Warning);
            }
        }
        else iconsVisible = false;

        mainWasVisible = Visible;
        if (mainWasVisible) Hide();
        ShowOverlays(settings.DarkenScreen && settings.DimPercent > 0);
        wallpaperMinutesElapsed = 0;
        if (overlays.Count > 0 && GetWallpaperFiles(settings.WallpaperFolder).Length > 0)
        {
            wallpaperIndex = -1;
            wallpaperMinutesElapsed = Math.Max(0, Math.Clamp(settings.WallpaperIntervalMinutes, 1, 30) - 1);
            wallpaperTimer.Start();
            AdvanceWallpaper();
            wallpaperMinutesElapsed = 0;
        }
        RestartMusic();
        UpdateUi();
    }

    private void ExitSaver()
    {
        if (!saverActive) return;
        saverActive = false;
        saverWasManual = false;
        fadeTimer.Stop();
        wallpaperTimer.Stop();
        CloseOverlays();
        StopMusic();
        RestoreMouseCursor();
        foreach (var item in minimizedWindows)
        {
            var placement = item.Placement;
            NativeMethods.SetWindowPlacement(item.Handle, ref placement);
        }
        minimizedWindows.Clear();
        if (saverDesktopIconsWereVisible && !NativeMethods.SetIconsVisible(true, out var iconError))
            tray.ShowBalloonTip(3500, "Desktop Hide", $"Viditelnost ikon se nepodařilo obnovit: {iconError}", ToolTipIcon.Warning);
        iconsVisible = saverDesktopIconsWereVisible;
        if (mainWasVisible && !allowClose) { ShowInTaskbar = true; Opacity = 1; Show(); WindowState = FormWindowState.Normal; Activate(); }
        UpdateUi();
    }

    private void ShowOverlays(bool darken)
    {
        if (overlays.Count == 0)
        {
            foreach (var screen in Screen.AllScreens)
            {
                var overlay = new SaverOverlay(screen.Bounds);
                overlays.Add(overlay);
                overlay.Show();
            }
        }
        foreach (var overlay in overlays) overlay.SetDimPercent(darken ? 0 : 0);
        if (overlays.Count > 0)
        {
            NativeMethods.SetCapture(overlays[0].Handle);
            NativeMethods.SetCursor(IntPtr.Zero);
            NativeMethods.SendMessage(overlays[0].Handle, NativeMethods.WM_SETCURSOR, overlays[0].Handle, new IntPtr(1));
            NudgeCursorUnderOverlay();
        }
        fadeTimer.Stop();
        if (!darken) return;
        fadeStartedAt = unchecked((uint)Environment.TickCount);
        fadeTimer.Start();
    }

    private void FadeStep()
    {
        var elapsed = unchecked((uint)Environment.TickCount - fadeStartedAt);
        var progress = Math.Clamp(elapsed / 900f, 0f, 1f);
        var smooth = progress * progress * (3f - 2f * progress);
        var opacity = settings.DimPercent * smooth;
        foreach (var overlay in overlays) overlay.SetDimPercent((int)Math.Round(opacity));
        if (progress >= 1f) fadeTimer.Stop();
    }

    private void ApplyCurrentDim()
    {
        var percent = settings.DarkenScreen ? settings.DimPercent : 0;
        foreach (var overlay in overlays) overlay.SetDimPercent(percent);
    }

    private void CloseOverlays()
    {
        NativeMethods.ReleaseCapture();
        foreach (var overlay in overlays) { overlay.Close(); overlay.Dispose(); }
        overlays.Clear();
    }

    private void CheckSaverMouseMovement()
    {
        if (!NativeMethods.GetCursorPos(out var current)) return;
        if (!haveSaverCursorPosition)
        {
            lastSaverCursorPosition = current;
            haveSaverCursorPosition = true;
            return;
        }
        int dx = current.X - lastSaverCursorPosition.X;
        int dy = current.Y - lastSaverCursorPosition.Y;
        // Keep the entry point fixed: small jitter that wanders back and forth
        // must not accumulate into a wake event.
        saverMouseDistance = Math.Sqrt((double)dx * dx + (double)dy * dy);
        if (saverMouseDistance >= SaverMouseWakeDistance) { ExitSaver(); return; }
        if (NativeMethods.IsCursorVisible()) HideMouseCursor();
        SaverOverlay.ApplyInvisibleCursor();
    }

    private void HideMouseCursor()
    {
        while (cursorHideCalls < 32)
        {
            int count = NativeMethods.ShowCursor(false);
            cursorHideCalls++;
            if (count < 0) break;
        }
    }

    private static void NudgeCursorUnderOverlay()
    {
        if (!NativeMethods.GetCursorPos(out var cursor)) return;
        var bounds = SystemInformation.VirtualScreen;
        if (bounds.Width < 2 || bounds.Height < 1) return;
        int nudgeX = cursor.X < bounds.Right - 1 ? cursor.X + 1 : cursor.X - 1;
        if (!NativeMethods.SetCursorPos(nudgeX, cursor.Y)) return;
        Application.DoEvents();
        NativeMethods.SetCursorPos(cursor.X, cursor.Y);
        Application.DoEvents();
        SaverOverlay.ApplyInvisibleCursor();
    }

    private void RestoreMouseCursor()
    {
        while (cursorHideCalls > 0)
        {
            NativeMethods.ShowCursor(true);
            cursorHideCalls--;
        }
        for (int i = 0; i < 32 && !NativeMethods.IsCursorVisible(); i++) NativeMethods.ShowCursor(true);
    }

    private void RestartMusic()
    {
        StopMusic();
        if (!saverActive || !settings.PlayMusic || string.IsNullOrWhiteSpace(settings.MusicFile) || !File.Exists(settings.MusicFile)) return;
        var file = settings.MusicFile.Replace("\"", "");
        var type = Path.GetExtension(file).Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "waveaudio" : "mpegvideo";
        if (NativeMethods.mciSendString($"open \"{file}\" type {type} alias DesktopHideMusic", null, 0, IntPtr.Zero) != 0)
        { tray.ShowBalloonTip(3500, "Desktop Hide", "Hudební soubor se nepodařilo otevřít.", ToolTipIcon.Warning); return; }
        musicOpen = true;
        NativeMethods.mciSendString($"setaudio DesktopHideMusic volume to {settings.MusicVolume * 10}", null, 0, IntPtr.Zero);
        NativeMethods.mciSendString("play DesktopHideMusic repeat", null, 0, IntPtr.Zero);
    }

    private void StopMusic()
    {
        if (!musicOpen) return;
        NativeMethods.mciSendString("stop DesktopHideMusic", null, 0, IntPtr.Zero);
        NativeMethods.mciSendString("close DesktopHideMusic", null, 0, IntPtr.Zero);
        musicOpen = false;
    }

    private static Icon LoadAppIcon()
    {
        using var logo = LoadAppLogo();
        using var small = new Bitmap(logo, new Size(64, 64));
        var handle = small.GetHicon();
        try { using var generated = Icon.FromHandle(handle); return (Icon)generated.Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }

    private static Bitmap LoadAppLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopHide.DesktopHideLogo.png");
        if (stream is null) return new Bitmap(64, 64);
        using var image = new Bitmap(stream);
        return new Bitmap(image);
    }

    private void ExitApplication()
    {
        allowClose = true;
        Close();
    }

    private void BeginWindowDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    private void ShowSettings() { ShowInTaskbar = true; Opacity = 1; Show(); WindowState = FormWindowState.Normal; Activate(); }
}








