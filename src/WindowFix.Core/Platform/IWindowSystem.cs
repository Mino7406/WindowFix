using WindowFix.Core.Model;

namespace WindowFix.Core.Platform;

/// <summary>Everything WindowFix needs from the OS about windows, monitors and processes.</summary>
public interface IWindowSystem
{
    IReadOnlyList<WindowInfo> GetWindows(bool includeHidden);
    IReadOnlyList<MonitorInfo> GetMonitors();
    WindowInfo? GetWindow(nint hwnd);

    /// <summary>Restores the window if minimized/maximized, moves it to <paramref name="rect"/>, then maximizes when asked. False when the OS refused (e.g. elevated target).</summary>
    bool SetPlacement(nint hwnd, PixelRect rect, bool maximize);

    bool Show(nint hwnd);

    /// <summary>Whether WindowFix itself runs elevated (admin).</summary>
    bool SelfElevated { get; }
    bool IsRunning(string exeName);
    void Launch(string path, string? args);
}
