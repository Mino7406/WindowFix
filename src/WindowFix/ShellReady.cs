using System.Runtime.InteropServices;

namespace WindowFix;

/// <summary>
/// Right after logon the taskbar (Explorer) may not exist or may still be busy. Registering a tray icon then can
/// block the UI thread for a long time, which froze WindowFix at boot (process alive, no window, no lock).
/// This waits — off the UI thread — until the taskbar window exists and answers messages.
/// </summary>
public static class ShellReady
{
    public static Task<bool> WaitAsync(TimeSpan max) => Task.Run(async () =>
    {
        var until = DateTime.UtcNow + max;
        while (DateTime.UtcNow < until)
        {
            var tray = FindWindow("Shell_TrayWnd", null);
            if (tray != 0 && SendMessageTimeout(tray, 0 /*WM_NULL*/, 0, 0, 0x0002 /*SMTO_ABORTIFHUNG*/, 500, out _) != 0)
            {
                await Task.Delay(1500); // let Explorer finish creating the notification area
                return true;
            }
            await Task.Delay(500);
        }
        return false;
    });

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint result);
}
