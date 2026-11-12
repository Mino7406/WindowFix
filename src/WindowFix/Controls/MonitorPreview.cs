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
        double scale = Math.Min((ActualWidth - 16) / (maxX - minX), (ActualHeight - 16) / (maxY - minY));
        double ox = (ActualWidth - (maxX - minX) * scale) / 2, oy = (ActualHeight - (maxY - minY) * scale) / 2;
        Rect Map(PixelRect r) => new(ox + (r.X - minX) * scale, oy + (r.Y - minY) * scale, r.Width * scale, r.Height * scale);

        var monitorFill = TryFindResource("Monitor.Fill") as Brush ?? Brushes.DimGray;
        var monitorPen = new Pen(TryFindResource("Monitor.Stroke") as Brush ?? Brushes.Gray, 1.5);
        var labelBrush = TryFindResource("Monitor.Label") as Brush ?? Brushes.Gray;
        int index = 1;
        foreach (var m in _monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y))
        {
            var r = Map(m.Bounds);
            dc.DrawRoundedRectangle(monitorFill, monitorPen, r, 6, 6);
            DrawText(dc, $"모니터 {index++}{(m.IsPrimary ? " (메인)" : "")} · {m.Bounds.Width}×{m.Bounds.Height}",
                new Point(r.X + 8, r.Y + 6), 11, labelBrush, r.Width - 16);
        }

        foreach (var pr in _rects)
        {
            var r = Map(pr.Abs);
            var c = BrandColor(pr.Label) ?? ColorFor(pr.Id);
            bool lightFill = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6;
            var fill = new SolidColorBrush(Color.FromArgb(pr.Emphasized ? (byte)0xF2 : (byte)0x38, c.R, c.G, c.B));
            var pen = new Pen(Selected == pr.Id ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)),
                Selected == pr.Id ? 2.5 : 1);
            dc.DrawRoundedRectangle(fill, pen, r, 2, 2);
            DrawText(dc, pr.Label, new Point(r.X + 6, r.Y + r.Height / 2 - 8), 12,
                pr.Emphasized ? (lightFill ? new SolidColorBrush(Color.FromRgb(0x1D, 0x21, 0x29)) : Brushes.White) : Brushes.LightGray, r.Width - 12);
            _hit.Add((r, pr.Id));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        // Topmost (last drawn) first; prefer the smallest rect when several overlap.
        var hit = _hit.Where(h => h.Screen.Contains(p)).OrderBy(h => h.Screen.Width * h.Screen.Height).FirstOrDefault();
        if (hit.Id != Guid.Empty) RectClicked?.Invoke(hit.Id);
        base.OnMouseLeftButtonDown(e);
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
}
