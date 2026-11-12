using WindowFix.Core.Model;

namespace WindowFix.Core.Layout;

/// <summary>Converts window rectangles between absolute desktop pixels and monitor-relative pixels.</summary>
public static class Placement
{
    public static RelRect ToRelative(PixelRect abs, MonitorInfo m) =>
        new(abs.X - m.Bounds.X, abs.Y - m.Bounds.Y, abs.Width, abs.Height);

    /// <summary>Maps a monitor-relative rect onto <paramref name="m"/>, shrinking then shifting it to stay inside the monitor.</summary>
    public static PixelRect ToAbsolute(RelRect rel, MonitorInfo m)
    {
        var b = m.Bounds;
        int w = Math.Min(rel.Width, b.Width);
        int h = Math.Min(rel.Height, b.Height);
        int x = Math.Clamp(b.X + rel.X, b.X, b.Right - w);
        int y = Math.Clamp(b.Y + rel.Y, b.Y, b.Bottom - h);
        return new PixelRect(x, y, w, h);
    }

    /// <summary>The monitor that overlaps the rect the most; the primary monitor when nothing overlaps.</summary>
    public static MonitorInfo MonitorOf(PixelRect abs, IReadOnlyList<MonitorInfo> monitors)
    {
        MonitorInfo? best = null;
        long bestArea = 0;
        foreach (var m in monitors)
        {
            long area = OverlapArea(abs, m.Bounds);
            if (area > bestArea)
            {
                bestArea = area;
                best = m;
            }
        }
        return best ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public static MonitorRef ToRef(MonitorInfo m) => new()
    {
        DevicePath = m.DevicePath,
        IsPrimary = m.IsPrimary,
        Width = m.Bounds.Width,
        Height = m.Bounds.Height,
    };

    private static long OverlapArea(PixelRect a, PixelRect b)
    {
        long w = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        long h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}
