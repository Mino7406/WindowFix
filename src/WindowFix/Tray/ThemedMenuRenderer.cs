using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace WindowFix.Tray;

/// <summary>Paints the tray menu in the app's dark/light colours instead of the Windows classic look.</summary>
internal sealed class ThemedMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private readonly bool _dark;

    public ThemedMenuRenderer(bool dark) : base(new Colors(dark))
    {
        _dark = dark;
        RoundedEdges = true;
    }

    private Color Fg => _dark ? Color.FromArgb(0xE7, 0xE9, 0xEE) : Color.FromArgb(0x1D, 0x21, 0x29);
    /// <summary>Red for "끝내기": the light pink only reads on the dark menu; the light menu needs a deep red.</summary>
    private Color Danger => _dark ? Color.FromArgb(0xFF, 0x87, 0x87) : Color.FromArgb(0xC9, 0x2A, 0x2A);
    private Color Hover => _dark ? Color.FromArgb(0x2A, 0x2D, 0x35) : Color.FromArgb(0xF1, 0xF3, 0xF6);

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var path = Rounded(r, 6);
        using var b = new SolidBrush(Hover);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(b, path);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? (e.Item.Tag as string == "danger" ? Danger : Fg)
                                     : Color.FromArgb(0x80, Fg);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Fg;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        using var pen = new Pen(Color.FromArgb(0x4C, 0x6E, 0xF5), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, [new Point(r.Left + 3, r.Top + r.Height / 2), new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4), new Point(r.Right - 3, r.Top + 3)]);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Windows 11: ask DWM for rounded corners on the drop-down window.</summary>
    public static void RoundCorners(Forms.ToolStripDropDown menu)
    {
        int round = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(menu.Handle, 33, ref round, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private sealed class Colors(bool dark) : Forms.ProfessionalColorTable
    {
        private Color Bg => dark ? Color.FromArgb(0x22, 0x25, 0x2B) : Color.White;
        private Color Line => dark ? Color.FromArgb(0x2C, 0x30, 0x38) : Color.FromArgb(0xE3, 0xE6, 0xEB);
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
        public override Color CheckBackground => Bg;
        public override Color CheckSelectedBackground => Bg;
        public override Color CheckPressedBackground => Bg;
    }
}
