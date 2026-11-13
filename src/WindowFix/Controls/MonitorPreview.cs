using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowFix.Core.Model;

namespace WindowFix.Controls;

public sealed record PreviewRect(Guid Id, string Label, PixelRect Abs, bool Emphasized);

/// <summary>
/// Draws the real monitor arrangement, scaled to fit, with window rectangles on top.
/// Retained-mode: each monitor (frame + its windows) is drawn once into its own visual; the hover lift is a
/// GPU-composited transform/opacity animation, so nothing is re-drawn per frame.
/// </summary>
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
    private static readonly Duration HoverTime = new(TimeSpan.FromMilliseconds(170));
    private static readonly IEasingFunction HoverEase = new CubicEase { EasingMode = EasingMode.EaseOut };

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x58, 0x65, 0xF2), Color.FromRgb(0xF5, 0xC5, 0x18), Color.FromRgb(0x2E, 0xB6, 0x7D),
        Color.FromRgb(0xE8, 0x5D, 0x75), Color.FromRgb(0x3B, 0xA7, 0xD9), Color.FromRgb(0xF0, 0x8A, 0x3C),
        Color.FromRgb(0x9B, 0x6B, 0xDF), Color.FromRgb(0x8C, 0xC0, 0x3F),
    ];

    private IReadOnlyList<MonitorInfo> _monitors = [];
    private IReadOnlyList<PreviewRect> _rects = [];
    private readonly List<(Rect Screen, Guid Id)> _hit = [];
    private readonly VisualCollection _children;
    private readonly List<MonitorVisual> _groups = [];
    private string? _hoverPath;

    public MonitorPreview()
    {
        _children = new VisualCollection(this);
    }

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

    /// <summary>Left click on a monitor frame, with that monitor's device path (the grey stage does nothing).</summary>
    public event Action<string>? MonitorClicked;

    /// <summary>Right click on a window tile.</summary>
    public event Action<Guid>? RectRightClicked;

    /// <summary>When true a left click reports the tile (save dialog: toggle a window); otherwise it raises <see cref="MonitorClicked"/>.</summary>
    public bool ClickSelectsRects { get; set; } = true;

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

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    /// <summary>Only paints the hit-test background; the monitors live in child visuals rebuilt here on data changes.</summary>
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        Rebuild();
    }

    private sealed class MonitorVisual
    {
        public required string Path { get; init; }
        public required Rect Rect { get; init; }
        public required ScaleTransform Scale { get; init; }
        public required TranslateTransform Lift { get; init; }
        public required SolidColorBrush Outline { get; init; }
        public required SolidColorBrush Shadow { get; init; }
    }

    private void Rebuild()
    {
        _children.Clear();
        _groups.Clear();
        _hit.Clear();
        if (_monitors.Count == 0 || ActualWidth < 10 || ActualHeight < 10) return;

        int minX = _monitors.Min(m => m.Bounds.X), minY = _monitors.Min(m => m.Bounds.Y);
        int maxX = _monitors.Max(m => m.Bounds.Right), maxY = _monitors.Max(m => m.Bounds.Bottom);
        double previewHeight = Math.Max(0, ActualHeight - MonitorLabelTopInset - PreviewBottomInset);
        double scale = Math.Min((ActualWidth - 16) / (maxX - minX), previewHeight / (maxY - minY));
        double ox = (ActualWidth - (maxX - minX) * scale) / 2;
        double oy = MonitorLabelTopInset + (previewHeight - (maxY - minY) * scale) / 2;
        Rect Map(PixelRect r) => new(ox + (r.X - minX) * scale, oy + (r.Y - minY) * scale, r.Width * scale, r.Height * scale);

        var monitorFill = TryFindResource("Monitor.Fill") as Brush ?? Brushes.DimGray;
        var monitorPen = Frozen(new Pen(TryFindResource("Monitor.Stroke") as Brush ?? Brushes.Gray, 1.5));
        var labelBrush = TryFindResource("Monitor.Label") as Brush ?? Brushes.Gray;
        var accent = (TryFindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(0x4C, 0x6E, 0xF5);
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        int index = 1;
        foreach (var m in _monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y))
        {
            var r = Map(m.Bounds);
            bool hovered = m.DevicePath == _hoverPath;
            var sc = new ScaleTransform(hovered ? 1.018 : 1, hovered ? 1.018 : 1, r.X + r.Width / 2, r.Y + r.Height / 2);
            var lift = new TranslateTransform(0, hovered ? -4 : 0);
            var outline = new SolidColorBrush(accent) { Opacity = hovered ? 1 : 0 };
            var shadow = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)) { Opacity = hovered ? 1 : 0 };

            var group = new ContainerVisual();
            var tg = new TransformGroup();
            tg.Children.Add(sc);
            tg.Children.Add(lift);
            group.Transform = tg;

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRoundedRectangle(shadow, null, new Rect(r.X + 2, r.Y + 6, Math.Max(0, r.Width - 4), r.Height), 8, 8);
                dc.DrawRoundedRectangle(monitorFill, monitorPen, r, 6, 6);
                DrawText(dc, $"모니터 {index++}{(m.IsPrimary ? " (메인)" : "")} · {m.Bounds.Width}×{m.Bounds.Height}",
                    new Point(r.X + 8, r.Y - 18), 11, labelBrush, r.Width - 16, ppd);
                DrawTiles(dc, m, Map, ppd);
                dc.DrawRoundedRectangle(null, new Pen(outline, 2), r, 6, 6);
            }
            group.Children.Add(dv);
            _children.Add(group);
            _groups.Add(new MonitorVisual { Path = m.DevicePath, Rect = r, Scale = sc, Lift = lift, Outline = outline, Shadow = shadow });
        }
    }

    private void DrawTiles(DrawingContext dc, MonitorInfo monitor, Func<PixelRect, Rect> map, double ppd)
    {
        var monitorBounds = map(monitor.Bounds);
        var contentBounds = new Rect(
            monitorBounds.X + MonitorContentInset,
            monitorBounds.Y + MonitorContentInset,
            Math.Max(0, monitorBounds.Width - MonitorContentInset * 2),
            Math.Max(0, monitorBounds.Height - MonitorContentInset * 2));
        var shadow = Frozen(new SolidColorBrush(Color.FromArgb(ShadowAlpha, 0, 0, 0)));

        foreach (var pr in _rects)
        {
            var owner = _monitors.OrderByDescending(m => IntersectionArea(m.Bounds, pr.Abs)).First();
            if (owner.DevicePath != monitor.DevicePath) continue;

            var bounds = map(pr.Abs);
            bounds.Intersect(contentBounds);
            if (bounds.IsEmpty || bounds.Width <= WindowInset * 2 || bounds.Height <= WindowInset * 2) continue;

            // Keep a visible gap when saved windows touch at their edges.
            var r = new Rect(bounds.X + WindowInset, bounds.Y + WindowInset, bounds.Width - WindowInset * 2, bounds.Height - WindowInset * 2);
            double radius = Math.Min(WindowCornerRadius, Math.Min(r.Width, r.Height) / 2);
            var c = BrandColor(pr.Label) ?? ColorFor(pr.Id);
            bool lightFill = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6;
            bool selected = Selected == pr.Id;
            byte fillAlpha = pr.Emphasized ? (byte)0xF2 : (byte)0x38;
            var fill = Frozen(new SolidColorBrush(Color.FromArgb(fillAlpha, c.R, c.G, c.B)));
            var titleColor = Darken(c, 0.18);
            var titleFill = Frozen(new SolidColorBrush(Color.FromArgb(fillAlpha, titleColor.R, titleColor.G, titleColor.B)));

            dc.DrawRoundedRectangle(shadow, null, new Rect(r.X, r.Y + ShadowOffsetY, r.Width, r.Height), radius, radius);
            dc.DrawRoundedRectangle(fill, null, r, radius, radius);

            double titleHeight = Math.Min(TitleBarHeight, r.Height);
            dc.PushClip(new RectangleGeometry(r, radius, radius));
            dc.DrawRectangle(titleFill, null, new Rect(r.X, r.Y, r.Width, titleHeight));
            dc.Pop();

            var windowLabelBrush = pr.Emphasized
                ? (lightFill ? Frozen(new SolidColorBrush(Color.FromRgb(0x1D, 0x21, 0x29))) : Brushes.White)
                : TryFindResource("Fg.Primary") as Brush ?? Brushes.White;
            DrawCenteredText(dc, pr.Label, new Rect(r.X + 6, r.Y + titleHeight, r.Width - 12, r.Height - titleHeight), 12, windowLabelBrush, ppd);

            double borderWidth = selected ? 2.5 : 1.5;
            var borderColor = selected ? Colors.White : Darken(c, 0.14);
            if (!selected) borderColor.A = lightFill ? (byte)0x70 : (byte)0xB8;
            if (r.Width > borderWidth && r.Height > borderWidth)
            {
                var border = Frozen(new Pen(Frozen(new SolidColorBrush(borderColor)), borderWidth));
                var borderBounds = new Rect(r.X + borderWidth / 2, r.Y + borderWidth / 2, r.Width - borderWidth, r.Height - borderWidth);
                double br = Math.Max(0, radius - borderWidth / 2);
                dc.DrawRoundedRectangle(null, border, borderBounds, br, br);
            }
            _hit.Add((r, pr.Id));
        }
    }

    // ---------- hover: GPU-composited lift, no re-draw ----------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        var path = _groups.FirstOrDefault(g => g.Rect.Contains(p))?.Path;
        if (path != _hoverPath) SetHover(path);
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        SetHover(null);
        base.OnMouseLeave(e);
    }

    private void SetHover(string? path)
    {
        _hoverPath = path;
        foreach (var g in _groups)
        {
            bool on = g.Path == path;
            Animate(g.Scale, ScaleTransform.ScaleXProperty, on ? 1.018 : 1);
            Animate(g.Scale, ScaleTransform.ScaleYProperty, on ? 1.018 : 1);
            Animate(g.Lift, TranslateTransform.YProperty, on ? -4 : 0);
            Animate(g.Outline, Brush.OpacityProperty, on ? 1 : 0);
            Animate(g.Shadow, Brush.OpacityProperty, on ? 1 : 0);
        }
    }

    private static void Animate(Animatable target, DependencyProperty prop, double to) =>
        target.BeginAnimation(prop, new DoubleAnimation(to, HoverTime) { EasingFunction = HoverEase }, HandoffBehavior.SnapshotAndReplace);

    // ---------- clicks ----------

    private Guid HitTest(Point p) =>
        // prefer the smallest rect when several overlap
        _hit.Where(h => h.Screen.Contains(p)).OrderBy(h => h.Screen.Width * h.Screen.Height).FirstOrDefault().Id;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (ClickSelectsRects)
        {
            var id = HitTest(p);
            if (id != Guid.Empty) RectClicked?.Invoke(id);
        }
        else if (_groups.FirstOrDefault(g => g.Rect.Contains(p)) is { } g)
        {
            MonitorClicked?.Invoke(g.Path);
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var id = HitTest(e.GetPosition(this));
        if (id != Guid.Empty) { RectRightClicked?.Invoke(id); e.Handled = true; }
        base.OnMouseRightButtonUp(e);
    }

    // ---------- drawing helpers ----------

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    private static void DrawText(DrawingContext dc, string text, Point at, double size, Brush brush, double maxWidth, double ppd)
    {
        if (maxWidth < 8) return;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, ppd)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(ft, at);
    }

    private static void DrawCenteredText(DrawingContext dc, string text, Rect bounds, double size, Brush brush, double ppd)
    {
        if (bounds.Width < 8 || bounds.Height < 8) return;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, ppd)
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
