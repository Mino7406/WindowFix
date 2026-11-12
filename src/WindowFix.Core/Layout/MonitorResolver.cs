using WindowFix.Core.Model;

namespace WindowFix.Core.Layout;

/// <summary>Finds the currently connected monitor that a saved <see cref="MonitorRef"/> refers to.</summary>
public static class MonitorResolver
{
    public static MonitorInfo? Resolve(MonitorRef saved, IReadOnlyList<MonitorInfo> current)
    {
        var byPath = current.FirstOrDefault(m =>
            string.Equals(m.DevicePath, saved.DevicePath, StringComparison.OrdinalIgnoreCase));
        if (byPath is not null) return byPath;

        return current.FirstOrDefault(m =>
            m.IsPrimary == saved.IsPrimary &&
            m.Bounds.Width == saved.Width &&
            m.Bounds.Height == saved.Height);
    }
}
