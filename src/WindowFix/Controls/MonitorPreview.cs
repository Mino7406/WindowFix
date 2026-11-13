using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowFix.Core.Model;

namespace WindowFix.Controls;

public sealed record PreviewRect(Guid Id, string Label, PixelRect Abs, bool Emphasized);

/// <summary>Draws the real monitor arrangement, scaled to fit, with window rectangles on top.</summary>
public sealed class MonitorPreview : FrameworkElement
{
    private const double WindowInset = 1;
    private const double WindowCornerRadius = 6;
    private const double TitleBarHeight = 14;
    private const double MonitorLabelTopInset = 24;
    private const double PreviewBottomInset = 8;
    private const double MonitorContentInset = 2;
    private const double ShadowOffsetY = 2;
    private const byte ShadowAlpha = 0x40;

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x58, 0x65, 0xF2), Color.FromRgb(0xF5, 0xC5, 0x18), Color.FromRgb(0x2E, 0xB6, 0x7D),
        Color.FromRgb(0xE8, 0x5D, 0x75), Color.FromRgb(0x3B, 0xA7, 0xD9), Color.FromRgb(0xF0, 0x8A, 0x3C),
        Color.FromRgb(0x9B, 0x6B, 0xDF), Color.FromRgb(0x8C, 0xC0, 0x3F),
    ];

    private IReadOnlyList<MonitorInfo> _monitors = [];
    private IReadOnlyList<PreviewRect> _rects = [];
    private readonly List<(Rect Screen, Guid Id)> _hit = [];

    public IReadOnlyList<MonitorInfo> Monitors
    {
        get => _monitors;
        set { _monitors = value; InvalidateVisual(); }
    }

    public IReadOnlyList<PreviewRect> Rects
    {
        get => _rects;
        set { _rects = value; InvalidateVisual(); }
    }

    public Guid? Selected
    {
        get;
        set { field = value; InvalidateVisual(); }
    }

    public event Action<Guid>? RectClicked;

    /// <summary>Well-known apps keep their own colours (Discord blurple, KakaoTalk yellow).</summary>
    public static Color? BrandColor(string label)
    {
        var l = label.ToLowerInvariant();
        if (l.Contains("discord")) return Color.FromRgb(0x58, 0x65, 0xF2);
        if (l.Contains("kakao") || l.Contains("카카오") || l.Contains("카톡")) return Color.FromRgb(0xFE, 0xE5, 0x00);
        if (l.Contains("youtube")) return Color.FromRgb(0xFF, 0x33, 0x33);
        if (l.Contains("chrome")) return Color.FromRgb(0x42, 0x85, 0xF4);
        if (l.Contains("spotify")) return Color.FromRgb(0x1D, 0xB9, 0x54);
        return null;
    }

    public static Color ColorFor(Guid id) => Palette[(id.GetHashCode() & 0x7FFFFFFF) % Palette.Length];

    protected override void OnRender(DrawingContext dc)
    {
        _hit.Clear();
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_monitors.Count == 0 || ActualWidth < 10 || ActualHeight < 10) return;

        int minX = _monitors.Min(m => m.Bounds.X), minY = _monitors.Min(m => m.Bounds.Y);
        int maxX = _monitors.Max(m => m.Bounds.Right), maxY = _monitors.Max(m => m.Bounds.Bottom);
        double previewHeight = Math.Max(0, ActualHeight - MonitorLabelTopInset - PreviewBottomInset);
        double scale = Math.Min((ActualWidth - 16) / (maxX - minX), previewHeight / (maxY - minY));
        double ox = (ActualWidth - (maxX - minX) * scale) / 2;
        double oy = MonitorLabelTopInset + (previewHeight - (maxY - minY) * scale) / 2;
        Rect Map(PixelRect r) => new(ox + (r.X - minX) * scale, oy + (r.Y - minY) * scale, r.Width * scale, r.Height * scale);

        var monitorFill = TryFindResource("Monitor.Fill") as Brush ?? Brushes.DimGray;
        var monitorPen = new Pen(TryFindResource("Monitor.Stroke") as Brush ?? Brushes.Gray, 1.5);
        var labelBrush = TryFindResource("Monitor.Label") as Brush ?? Brushes.Gray;
        var accent = (TryFindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(0x4C, 0x6E, 0xF5);
        _monitorRects.Clear();
        int index = 1;
        foreach (var m in _monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y))
        {
            var r = Map(m.Bounds);
            _monitorRects.Add((r, m.DevicePath));
            double k = Lift(m.DevicePath);
            dc.PushTransform(LiftTransform(r, k));
            if (k > 0.01)
            {
                var glow = new SolidColorBrush(Color.FromArgb((byte)(70 * k), 0, 0, 0));
                dc.DrawRoundedRectangle(glow, null, new Rect(r.X + 2, r.Y + 6 * k, r.Width - 4, r.Height), 8, 8);
            }
            dc.DrawRoundedRectangle(monitorFill, monitorPen, r, 6, 6);
            if (k > 0.01)
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * k), accent.R, accent.G, accent.B)), 2), r, 6, 6);
            DrawText(dc, $"모니터 {index++}{(m.IsPrimary ? " (메인)" : "")} · {m.Bounds.Width}×{m.Bounds.Height}",
                new Point(r.X + 8, r.Y - 18), 11, labelBrush, r.Width - 16);
            dc.Pop();
        }

        foreach (var pr in _rects)
        {
            var monitor = _monitors
                .OrderByDescending(m => IntersectionArea(m.Bounds, pr.Abs))
                .First();
            var monitorBounds = Map(monitor.Bounds);
            dc.PushTransform(LiftTransform(monitorBounds, Lift(monitor.DevicePath)));
            var contentBounds = new Rect(
                monitorBounds.X + MonitorContentInset,
                monitorBounds.Y + MonitorContentInset,
                Math.Max(0, monitorBounds.Width - MonitorContentInset * 2),
                Math.Max(0, monitorBounds.Height - MonitorContentInset * 2));
            var bounds = Map(pr.Abs);
            bounds.Intersect(contentBounds);
            if (bounds.IsEmpty || bounds.Width <= WindowInset * 2 || bounds.Height <= WindowInset * 2) { dc.Pop(); continue; }

            // Keep a visible gap when saved windows touch at their edges.
            var r = new Rect(bounds.X + WindowInset, bounds.Y + WindowInset,
                bounds.Width - WindowInset * 2, bounds.Height - WindowInset * 2);
            double radius = Math.Min(WindowCornerRadius, Math.Min(r.Width, r.Height) / 2);
            var c = BrandColor(pr.Label) ?? ColorFor(pr.Id);
            bool lightFill = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6;
            bool selected = Selected == pr.Id;
            byte fillAlpha = pr.Emphasized ? (byte)0xF2 : (byte)0x38;
            var fill = new SolidColorBrush(Color.FromArgb(fillAlpha, c.R, c.G, c.B));
            var titleColor = Darken(c, 0.18);
            var titleFill = new SolidColorBrush(Color.FromArgb(fillAlpha, titleColor.R, titleColor.G, titleColor.B));

            var shadow = new SolidColorBrush(Color.FromArgb(ShadowAlpha, 0, 0, 0));
            var shadowBounds = new Rect(r.X, r.Y + ShadowOffsetY, r.Width, r.Height);
            dc.DrawRoundedRectangle(shadow, null, shadowBounds, radius, radius);
            dc.DrawRoundedRectangle(fill, null, r, radius, radius);

            double titleHeight = Math.Min(TitleBarHeight, r.Height);
            dc.PushClip(new RectangleGeometry(r, radius, radius));
            dc.DrawRoundedRectangle(titleFill, null,
                new Rect(r.X, r.Y, r.Width, titleHeight), radius, radius);
            if (titleHeight > radius)
                dc.DrawRectangle(titleFill, null,
                    new Rect(r.X, r.Y + radius, r.Width, titleHeight - radius));
            dc.Pop();

            var windowLabelBrush = pr.Emphasized
                ? (lightFill ? new SolidColorBrush(Color.FromRgb(0x1D, 0x21, 0x29)) : Brushes.White)
                : TryFindResource("Fg.Primary") as Brush ?? Brushes.White;
            var labelBounds = new Rect(r.X + 6, r.Y + titleHeight, r.Width - 12, r.Height - titleHeight);
            DrawCenteredText(dc, pr.Label, labelBounds, 12, windowLabelBrush);

            double borderWidth = selected ? 2.5 : 1.5;
            var borderColor = selected
                ? Colors.White
                : Darken(c, 0.14);
            if (!selected) borderColor.A = lightFill ? (byte)0x70 : (byte)0xB8;
            var border = new Pen(new SolidColorBrush(borderColor), borderWidth);
            var borderBounds = new Rect(r.X + borderWidth / 2, r.Y + borderWidth / 2,
                Math.Max(0, r.Width - borderWidth), Math.Max(0, r.Height - borderWidth));
            if (r.Width > borderWidth && r.Height > borderWidth)
                dc.DrawRoundedRectangle(null, border, borderBounds,
                    Math.Max(0, radius - borderWidth / 2), Math.Max(0, radius - borderWidth / 2));
            _hit.Add((r, pr.Id));
            dc.Pop();
        }
    }

    // ---------- hover lift: the monitor under the cursor rises a little and gets an accent outline ----------

    private readonly List<(Rect Rect, string Path)> _monitorRects = [];
    private readonly Dictionary<string, double> _lift = [];
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private string? _hoverPath;
    private bool _animating;

    private double Lift(string path) => _lift.TryGetValue(path, out var v) ? v : 0;

    private static Transform LiftTransform(Rect r, double k)
    {
        if (k <= 0.001) return Transform.Identity;
        double s = 1 + 0.018 * k;
        var g = new TransformGroup();
        g.Children.Add(new ScaleTransform(s, s, r.X + r.Width / 2, r.Y + r.Height / 2));
        g.Children.Add(new TranslateTransform(0, -4 * k));
        return g;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        var path = _monitorRects.FirstOrDefault(m => m.Rect.Contains(p)).Path;
        if (path != _hoverPath) { _hoverPath = path; StartAnimation(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hoverPath = null;
        StartAnimation();
        base.OnMouseLeave(e);
    }

    private void StartAnimation()
    {
        if (_animating) return;
        _animating = true;
        _clock.Restart();
        CompositionTarget.Rendering += Tick;
    }

    private void Tick(object? sender, EventArgs e)
    {
        double dt = Math.Min(0.05, _clock.Elapsed.TotalSeconds);
        _clock.Restart();
        bool moving = false;
        foreach (var (_, path) in _monitorRects)
        {
            double target = path == _hoverPath ? 1 : 0, v = Lift(path);
            double next = v + (target - v) * Math.Min(1, dt * 14);
            if (Math.Abs(target - next) < 0.01) next = target; else moving = true;
            _lift[path] = next;
        }
        InvalidateVisual();
        if (moving) return;
        CompositionTarget.Rendering -= Tick;
        _animating = false;
    }

    /// <summary>Left click on a monitor frame (the grey stage around the monitors does nothing).</summary>
    public event Action? Clicked;

    /// <summary>Right click on a window tile.</summary>
    public event Action<Guid>? RectRightClicked;

    /// <summary>When true a left click reports the tile (save dialog: toggle a window); otherwise it raises <see cref="Clicked"/>.</summary>
    public bool ClickSelectsRects { get; set; } = true;

    private Guid HitTest(Point p) =>
        // prefer the smallest rect when several overlap
        _hit.Where(h => h.Screen.Contains(p)).OrderBy(h => h.Screen.Width * h.Screen.Height).FirstOrDefault().Id;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var id = HitTest(e.GetPosition(this));
        if (ClickSelectsRects) { if (id != Guid.Empty) RectClicked?.Invoke(id); }
        else if (_monitorRects.Any(m => m.Rect.Contains(e.GetPosition(this)))) Clicked?.Invoke();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var id = HitTest(e.GetPosition(this));
        if (id != Guid.Empty) { RectRightClicked?.Invoke(id); e.Handled = true; }
        base.OnMouseRightButtonUp(e);
    }

    private void DrawText(DrawingContext dc, string text, Point at, double size, Brush brush, double maxWidth)
    {
        if (maxWidth < 8) return;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(ft, at);
    }

    private void DrawCenteredText(DrawingContext dc, string text, Rect bounds, double size, Brush brush)
    {
        if (bounds.Width < 8 || bounds.Height < 8) return;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = bounds.Width,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(ft, new Point(
            bounds.X + Math.Max(0, (bounds.Width - ft.Width) / 2),
            bounds.Y + Math.Max(0, (bounds.Height - ft.Height) / 2)));
    }

    private static Color Darken(Color color, double amount) => Color.FromRgb(
        (byte)Math.Round(color.R * (1 - amount)),
        (byte)Math.Round(color.G * (1 - amount)),
        (byte)Math.Round(color.B * (1 - amount)));

    private static long IntersectionArea(PixelRect a, PixelRect b)
    {
        int width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
        int height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
        return (long)width * height;
    }
}
