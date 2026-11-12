using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WindowFix.Core.Layout;
using WindowFix.Core.Lock;
using static WindowFix.Core.Platform.NativeMethods;

namespace WindowFix;

/// <summary>
/// The lock button: a small tab glued to a monitor edge, shown while the lock is on.
/// Click = pause / re-lock (windows go back to their saved places). Drag = move it; on release it sticks to the
/// nearest edge and the spot is remembered. Never takes focus; not in the taskbar or Alt+Tab.
/// </summary>
public partial class FloatingLockButton : Window
{
    private const double TabThicknessDip = 22, TabLengthDip = 56;
    private static readonly TimeSpan ToggleGuard = TimeSpan.FromMilliseconds(400);

    private readonly AppHost _host;
    private Point? _downAt;
    private bool _placed;
    private DateTime _lastToggle = DateTime.MinValue;

    public FloatingLockButton(AppHost host)
    {
        _host = host;
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            long ex = GetWindowLongPtr(Hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(Hwnd, GWL_EXSTYLE, (nint)(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        };
        MouseEnter += (_, _) => Opacity = 1.0;
        MouseLeave += (_, _) => Opacity = RestingOpacity;
        Update(host.Lock.State);
    }

    private nint Hwnd => new WindowInteropHelper(this).Handle;
    private double RestingOpacity => _host.Lock.State == LockState.Paused ? 0.9 : 0.45;

    public void Update(LockState state)
    {
        bool paused = state == LockState.Paused;
        Glyph.Text = paused ? "🔓" : "🔒";
        Tab.Background = new SolidColorBrush(paused ? Color.FromRgb(0xF0, 0x8A, 0x3C) : Color.FromRgb(0x3B, 0x5B, 0xDB));
        ToolTip = paused
            ? "잠깐 풀어 둔 상태예요. 누르면 창들이 원래 자리로 돌아가고 다시 고정돼요."
            : "창 고정 중이에요. 누르면 잠깐 풀려서 창을 옮길 수 있어요. (끌어서 다른 가장자리로 옮길 수 있어요)";
        if (!IsMouseOver) Opacity = RestingOpacity;
    }

    /// <summary>Show glued to the saved edge spot, or the default spot on the secondary monitor's right edge.</summary>
    public void ShowAtSavedSpot()
    {
        Show();
        if (_placed) return;
        _placed = true;
        var s = _host.Config.Settings;
        var (len, thick) = TabSize();
        Place(LockButtonPlacement.Resolve(s.LockButtonX, s.LockButtonY, _host.Ws.GetMonitors(), len, thick));
    }

    private (int Length, int Thickness) TabSize()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return ((int)Math.Round(TabLengthDip * scale), (int)Math.Round(TabThicknessDip * scale));
    }

    private void Place(TabRect t)
    {
        const double r = 9;
        Tab.CornerRadius = t.Edge switch
        {
            ScreenEdge.Right => new CornerRadius(r, 0, 0, r),
            ScreenEdge.Left => new CornerRadius(0, r, r, 0),
            ScreenEdge.Top => new CornerRadius(0, 0, r, r),
            _ => new CornerRadius(r, r, 0, 0),
        };
        SetWindowPos(Hwnd, 0, t.X, t.Y, t.Width, t.Height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _downAt = PointToScreen(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_downAt is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var now = PointToScreen(e.GetPosition(this));
        if (LockButtonPlacement.IsClick(now.X - start.X, now.Y - start.Y)) return;

        _downAt = null;
        ReleaseMouseCapture();
        DragMove(); // returns when the mouse button is released
        SnapAndSave();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_downAt is null) return;
        _downAt = null;
        e.Handled = true;

        // One press = one toggle, even if a double-click or a bounced button sends two.
        if (DateTime.UtcNow - _lastToggle < ToggleGuard) return;
        _lastToggle = DateTime.UtcNow;
        _host.Lock.TogglePause();
    }

    private void SnapAndSave()
    {
        GetWindowRect(Hwnd, out var r);
        var (len, thick) = TabSize();
        var t = LockButtonPlacement.Snap((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2, _host.Ws.GetMonitors(), len, thick);
        Place(t);
        _host.Config.Settings.LockButtonX = t.CenterX;
        _host.Config.Settings.LockButtonY = t.CenterY;
        _host.SaveConfig();
    }
}
