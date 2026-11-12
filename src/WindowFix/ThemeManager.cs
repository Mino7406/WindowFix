using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using WindowFix.Core.Model;
using ThemeMode = WindowFix.Core.Model.ThemeMode;

namespace WindowFix;

/// <summary>
/// Swaps the Dark/Light brush dictionary, darkens window title bars, and follows Windows' app mode when the
/// setting is "System" (re-applied live when the user changes it in Windows settings).
/// </summary>
public static class ThemeManager
{
    private static Func<ThemeMode>? _mode;

    public static bool IsDark { get; private set; } = true;

    public static event Action? Changed;

    public static void Init(Func<ThemeMode> mode)
    {
        _mode = mode;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
                Application.Current?.Dispatcher.BeginInvoke(Reapply);
        };
        Reapply();
    }

    public static void Reapply()
    {
        if (_mode is null || Application.Current is null) return;
        bool dark = ThemeResolver.IsDark(_mode(), SystemIsDark());
        var dicts = Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml") };
        if (dicts.Count > 0) dicts[0] = next; else dicts.Add(next);
        IsDark = dark;
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
        Changed?.Invoke();
    }

    /// <summary>Windows "앱 모드": AppsUseLightTheme = 0 means dark. Missing value → light (Windows default).</summary>
    public static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Call from SourceInitialized (and on theme change) so the caption bar matches the theme.</summary>
    public static void ApplyTitleBar(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == 0) return;
        int on = IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
}
