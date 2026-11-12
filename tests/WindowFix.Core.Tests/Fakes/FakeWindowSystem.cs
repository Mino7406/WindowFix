using WindowFix.Core.Model;
using WindowFix.Core.Platform;

namespace WindowFix.Core.Tests.Fakes;

internal sealed class FakeWindowSystem : IWindowSystem
{
    public List<WindowInfo> Windows { get; } = [];
    public List<MonitorInfo> Monitors { get; } = [];
    public HashSet<nint> FailPlacement { get; } = [];
    public HashSet<string> Running { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(nint Hwnd, PixelRect Rect, bool Maximize)> Placements { get; } = [];
    public List<nint> Shown { get; } = [];
    public List<(string Path, string? Args)> Launches { get; } = [];

    public static MonitorInfo Primary { get; } =
        new("dev-primary", @"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true);

    public static MonitorInfo Secondary { get; } =
        new("dev-secondary", @"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), false);

    public static FakeWindowSystem TwoMonitors()
    {
        var f = new FakeWindowSystem();
        f.Monitors.Add(Primary);
        f.Monitors.Add(Secondary);
        return f;
    }

    public static WindowInfo Win(nint h, string exe, PixelRect rect, string title = "t", string cls = "C",
        bool visible = true, bool maximized = false, bool owned = false, string? path = null) =>
        new(h, exe, path ?? @"C:\Apps\" + exe, title, cls, rect, visible, false, maximized, owned, false, false, 1);

    public WindowInfo Add(WindowInfo w)
    {
        Windows.Add(w);
        if (w.ExeName.Length > 0) Running.Add(w.ExeName);
        return w;
    }

    public IReadOnlyList<WindowInfo> GetWindows(bool includeHidden) =>
        Windows.Where(w => includeHidden || w.IsVisible).ToList();

    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors;

    public WindowInfo? GetWindow(nint hwnd) => Windows.FirstOrDefault(w => w.Handle == hwnd);

    public bool SetPlacement(nint hwnd, PixelRect rect, bool maximize)
    {
        Placements.Add((hwnd, rect, maximize));
        if (FailPlacement.Contains(hwnd)) return false;
        Replace(hwnd, w => w with { Rect = rect, IsMaximized = maximize, IsMinimized = false });
        return true;
    }

    public bool Show(nint hwnd)
    {
        Shown.Add(hwnd);
        Replace(hwnd, w => w with { IsVisible = true });
        return true;
    }

    public bool IsRunning(string exeName) => Running.Contains(exeName);

    public bool FailLaunch { get; set; }

    public void Launch(string path, string? args)
    {
        Launches.Add((path, args));
        if (FailLaunch) throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified.");
    }

    private void Replace(nint hwnd, Func<WindowInfo, WindowInfo> f)
    {
        int i = Windows.FindIndex(w => w.Handle == hwnd);
        if (i >= 0) Windows[i] = f(Windows[i]);
    }
}
