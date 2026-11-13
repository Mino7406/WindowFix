namespace WindowFix.Core.Model;

/// <summary>Absolute rectangle in physical desktop pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>Rectangle relative to a monitor's top-left corner, in physical pixels.</summary>
public readonly record struct RelRect(int X, int Y, int Width, int Height);

public sealed record MonitorInfo(string DevicePath, string GdiName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary);

public sealed record WindowInfo(
    nint Handle,
    string ExeName,
    string? ExePath,
    string Title,
    string ClassName,
    PixelRect Rect,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized,
    bool IsOwned,
    bool IsToolWindow,
    bool IsCloaked,
    int ProcessId,
    bool IsElevated = false);
