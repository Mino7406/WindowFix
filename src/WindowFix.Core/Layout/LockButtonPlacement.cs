using WindowFix.Core.Model;

namespace WindowFix.Core.Layout;

public enum ScreenEdge { Left, Top, Right, Bottom }

/// <summary>Physical-pixel rectangle of the edge tab and the work-area edge it is glued to.</summary>
public readonly record struct TabRect(int X, int Y, int Width, int Height, ScreenEdge Edge)
{
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;
}

/// <summary>
/// The lock button is a tab glued to a monitor's work-area edge (half a rounded rectangle peeking out of the edge).
/// Saved position = the tab's centre; it is always re-snapped to the nearest edge. Physical pixels throughout.
/// </summary>
public static class LockButtonPlacement
{
    public const int DragThreshold = 4;

    public static TabRect Resolve(int? savedCx, int? savedCy, IReadOnlyList<MonitorInfo> monitors, int length, int thickness)
    {
        if (savedCx is int x && savedCy is int y && monitors.Any(m => Contains(m.WorkArea, x, y)))
            return Snap(x, y, monitors, length, thickness);

        var target = monitors.FirstOrDefault(m => !m.IsPrimary) ?? monitors.First(m => m.IsPrimary);
        var wa = target.WorkArea;
        return Snap(wa.Right - 1, wa.Bottom - wa.Height / 5, monitors, length, thickness);
    }

    /// <summary>Glue a tab centred near (cx, cy) to the closest work-area edge of the monitor under (or nearest to) that point.</summary>
    public static TabRect Snap(int cx, int cy, IReadOnlyList<MonitorInfo> monitors, int length, int thickness)
    {
        var wa = monitors
            .Select(m => m.WorkArea)
            .OrderBy(r => Contains(r, cx, cy) ? 0 : 1)
            .ThenBy(r => DistanceSq(r, cx, cy))
            .First();
        int px = Math.Clamp(cx, wa.X, wa.Right - 1);
        int py = Math.Clamp(cy, wa.Y, wa.Bottom - 1);

        var edge = new[]
        {
            (ScreenEdge.Right, wa.Right - 1 - px),
            (ScreenEdge.Left, px - wa.X),
            (ScreenEdge.Top, py - wa.Y),
            (ScreenEdge.Bottom, wa.Bottom - 1 - py),
        }.MinBy(e => e.Item2).Item1;

        int along(int c, int start, int size) => Math.Clamp(c - length / 2, start, start + size - length);
        return edge switch
        {
            ScreenEdge.Right => new TabRect(wa.Right - thickness, along(py, wa.Y, wa.Height), thickness, length, edge),
            ScreenEdge.Left => new TabRect(wa.X, along(py, wa.Y, wa.Height), thickness, length, edge),
            ScreenEdge.Top => new TabRect(along(px, wa.X, wa.Width), wa.Y, length, thickness, edge),
            _ => new TabRect(along(px, wa.X, wa.Width), wa.Bottom - thickness, length, thickness, edge),
        };
    }

    /// <summary>
    /// Corner radii (top-left, top-right, bottom-right, bottom-left) of a tab glued to <paramref name="edge"/>.
    /// The side touching the screen edge is flat at roundness 0; at 1 (while dragging) all four corners are round.
    /// </summary>
    public static (double TopLeft, double TopRight, double BottomRight, double BottomLeft) Corners(ScreenEdge edge, double roundness, double radius)
    {
        double flat = radius * Math.Clamp(roundness, 0, 1);
        return edge switch
        {
            ScreenEdge.Right => (radius, flat, flat, radius),
            ScreenEdge.Left => (flat, radius, radius, flat),
            ScreenEdge.Top => (flat, flat, radius, radius),
            _ => (radius, radius, flat, flat),
        };
    }

    /// <summary>Ease-out-cubic step from <paramref name="a"/> to <paramref name="b"/> for the snap slide; t is clamped to [0, 1].</summary>
    public static TabRect Interpolate(TabRect a, TabRect b, double t)
    {
        double e = 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);
        int lerp(int x, int y) => (int)Math.Round(x + (y - x) * e, MidpointRounding.AwayFromZero);
        return new TabRect(lerp(a.X, b.X), lerp(a.Y, b.Y), lerp(a.Width, b.Width), lerp(a.Height, b.Height), b.Edge);
    }

    public static bool IsClick(double dx, double dy) => Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold;

    private static bool Contains(PixelRect r, int x, int y) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;

    private static long DistanceSq(PixelRect r, int x, int y)
    {
        long dx = x < r.X ? r.X - x : x >= r.Right ? x - r.Right + 1 : 0;
        long dy = y < r.Y ? r.Y - y : y >= r.Bottom ? y - r.Bottom + 1 : 0;
        return dx * dx + dy * dy;
    }
}
