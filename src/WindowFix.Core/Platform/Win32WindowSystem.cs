using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowFix.Core.Model;
using static WindowFix.Core.Platform.NativeMethods;

namespace WindowFix.Core.Platform;

public sealed class Win32WindowSystem : IWindowSystem
{
    public IReadOnlyList<WindowInfo> GetWindows(bool includeHidden)
    {
        var handles = new List<nint>();
        EnumWindows((h, _) => { handles.Add(h); return true; }, 0);

        var exeCache = new Dictionary<uint, string?>();
        var result = new List<WindowInfo>(handles.Count);
        foreach (var h in handles)
        {
            if (!includeHidden && !IsWindowVisible(h)) continue;
            var info = Describe(h, exeCache);
            if (info is not null) result.Add(info);
        }
        return result;
    }

    public WindowInfo? GetWindow(nint hwnd) =>
        IsWindow(hwnd) ? Describe(hwnd, new Dictionary<uint, string?>()) : null;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var devicePaths = QueryDevicePathsByGdiName();
        var result = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (hMon, _, ref _, _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                var gdi = mi.szDevice;
                result.Add(new MonitorInfo(
                    devicePaths.TryGetValue(gdi, out var path) && !string.IsNullOrEmpty(path) ? path : gdi,
                    gdi,
                    ToPixelRect(mi.rcMonitor),
                    ToPixelRect(mi.rcWork),
                    (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, 0);
        return result;
    }

    public bool SetPlacement(nint hwnd, PixelRect rect, bool maximize)
    {
        if (!IsWindow(hwnd)) return false;
        if (IsIconic(hwnd) || IsZoomed(hwnd)) ShowCmd(hwnd, SW_RESTORE);

        bool ok = SetWindowPos(hwnd, 0, rect.X, rect.Y, rect.Width, rect.Height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        if (!ok) return false;
        // Moving onto a monitor with a different scale makes per-monitor-aware apps (Discord) rescale themselves on
        // WM_DPICHANGED; a second identical request lands the size exactly (same approach as PowerToys FancyZones).
        SetWindowPos(hwnd, 0, rect.X, rect.Y, rect.Width, rect.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

        if (maximize) ShowCmd(hwnd, SW_MAXIMIZE);
        return true;
    }

    public bool Show(nint hwnd)
    {
        if (!IsWindow(hwnd)) return false;
        ShowCmd(hwnd, SW_SHOW);
        if (IsIconic(hwnd)) ShowCmd(hwnd, SW_RESTORE);
        return true;
    }

    /// <summary>
    /// Other threads' windows get async calls so a hung app can't block us; their posted requests run in order.
    /// Our own thread's windows must be handled synchronously, or a posted restore would run after the move.
    /// </summary>
    private static void ShowCmd(nint hwnd, int cmd)
    {
        if (GetWindowThreadProcessId(hwnd, out _) == GetCurrentThreadId())
            ShowWindow(hwnd, cmd);
        else
            ShowWindowAsync(hwnd, cmd);
    }

    public bool IsRunning(string exeName)
    {
        var name = Path.GetFileNameWithoutExtension(exeName);
        var procs = Process.GetProcessesByName(name);
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    public void Launch(string path, string? args)
    {
        using var _ = Process.Start(new ProcessStartInfo(path, args ?? "")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
        });
    }

    // ---- helpers ----

    private static WindowInfo? Describe(nint h, Dictionary<uint, string?> exeCache)
    {
        if (!GetWindowRect(h, out var r)) return null;
        GetWindowThreadProcessId(h, out var pid);
        if (!exeCache.TryGetValue(pid, out var exePath))
        {
            exePath = QueryExePath(pid);
            exeCache[pid] = exePath;
        }

        long exStyle = GetWindowLongPtr(h, GWL_EXSTYLE);
        DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int));

        return new WindowInfo(
            Handle: h,
            ExeName: exePath is null ? "" : Path.GetFileName(exePath),
            ExePath: exePath,
            Title: GetText(h),
            ClassName: GetClass(h),
            Rect: ToPixelRect(r),
            IsVisible: IsWindowVisible(h),
            IsMinimized: IsIconic(h),
            IsMaximized: IsZoomed(h),
            IsOwned: NativeMethods.GetWindow(h, GW_OWNER) != 0,
            IsToolWindow: (exStyle & WS_EX_TOOLWINDOW) != 0,
            IsCloaked: cloaked != 0,
            ProcessId: (int)pid);
    }

    private static string GetText(nint h)
    {
        int len = GetWindowTextLength(h);
        if (len <= 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClass(nint h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string? QueryExePath(uint pid)
    {
        var hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == 0) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(hProc, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(hProc);
        }
    }

    private static PixelRect ToPixelRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    /// <summary>Maps GDI names (\\.\DISPLAYn) to stable monitor device paths via the DisplayConfig API.</summary>
    private static Dictionary<string, string> QueryDevicePathsByGdiName()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var numPaths, out var numModes) != 0) return map;
            var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPaths, paths, ref numModes, modes, 0) != 0) return map;

            for (int i = 0; i < numPaths; i++)
            {
                var p = paths[i];
                var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                        adapterId = p.sourceInfo.adapterId,
                        id = p.sourceInfo.id,
                    },
                };
                var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = p.targetInfo.adapterId,
                        id = p.targetInfo.id,
                    },
                };
                if (DisplayConfigGetDeviceInfo(ref src) == 0 && DisplayConfigGetDeviceInfo(ref tgt) == 0 &&
                    !string.IsNullOrEmpty(tgt.monitorDevicePath))
                {
                    map[src.viewGdiDeviceName] = tgt.monitorDevicePath;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            // Fall back to GDI names.
        }
        return map;
    }
}
