using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowFix.Core.Layout;
using WindowFix.Core.Lock;
using static WindowFix.Core.Platform.NativeMethods;

namespace WindowFix;

/// <summary>
/// The lock button: a small tab glued to a monitor edge, shown while the lock is on.
/// Click = pause / re-lock (windows go back to their saved places). Drag = move it; on release it sticks to the
/// nearest edge and the spot is remembered. Never takes focus; not in the taskbar or Alt+Tab.
/// While dragged it becomes a symmetric rounded rectangle; on release it slides to the edge and flattens again.
/// </summary>
public partial class FloatingLockButton : Window
{
    private const double TabThicknessDip = 22, TabLengthDip = 56, Radius = 9, SliverDip = 8;
    private static readonly Duration Quick = new(TimeSpan.FromMilliseconds(140));
    private static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(360);

    /// <summary>0 = half tab glued to the edge (flat side), 1 = symmetric rounded rectangle (while dragging).</summary>
    public static readonly DependencyProperty RoundnessProperty = DependencyProperty.Register(
        nameof(Roundness), typeof(double), typeof(FloatingLockButton),
        new PropertyMetadata(0.0, (d, _) => ((FloatingLockButton)d).ApplyCorners()));

    public double Roundness
    {
        get => (double)GetValue(RoundnessProperty);
        set => SetValue(RoundnessProperty, value);
    }

    private ScreenEdge _edge = ScreenEdge.Right;
    private volatile int _slideVersion; // bumping it cancels a glide in progress
    private bool _sliding;
    private static readonly TimeSpan ToggleGuard = TimeSpan.FromMilliseconds(400);

    private readonly AppHost _host;
    private Point? _downAt;
    private bool _placed;
    private bool _dragging;
    private DateTime _lastToggle = DateTime.MinValue;

    public FloatingLockButton(AppHost host)
    {
        _host = host;
        InitializeComponent();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        Closed += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        };
        SourceInitialized += (_, _) =>
        {
            long ex = GetWindowLongPtr(Hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(Hwnd, GWL_EXSTYLE, (nint)(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        };
        MouseEnter += (_, _) => { if (!_sliding && !_dragging) SetExpanded(true); };
        MouseLeave += (_, _) => { if (!_sliding && !_dragging) SetExpanded(ShouldStayOpen); };
        Update(host.Lock.State);
        ApplyCorners();
    }

    private nint Hwnd => new WindowInteropHelper(this).Handle;
    /// <summary>Paused stays open so it's obvious the windows are free to move.</summary>
    private bool ShouldStayOpen => _host.Lock.State == LockState.Paused;

    public void Update(LockState state)
    {
        bool paused = state == LockState.Paused;
        var color = paused ? Color.FromRgb(0xF0, 0x8A, 0x3C) : Color.FromRgb(0x3B, 0x5B, 0xDB);
        TabFill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, Quick));
        Keyhole.Fill = new SolidColorBrush(color);
        // open padlock: the shackle lifts and swings out a little
        ShackleShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(paused ? -2.2 : 0, Quick));
        ShackleShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(paused ? 2.2 : 0, Quick));
        ToolTip = paused
            ? "잠깐 풀어 둔 상태예요. 누르면 창들이 원래 자리로 돌아가고 다시 고정돼요."
            : "창 고정 중이에요. 누르면 잠깐 풀려서 창을 옮길 수 있어요. (끌어서 다른 가장자리로 옮길 수 있어요)";
        if (!IsMouseOver && !_dragging) SetExpanded(ShouldStayOpen);
    }

    private bool VerticalEdge => _edge is ScreenEdge.Left or ScreenEdge.Right;

    /// <summary>Sliver (6 DIP) ⇄ full tab, growing out of the edge it's glued to; the padlock fades in with it.</summary>
    private void SetExpanded(bool on, bool animate = true)
    {
        if (_dragging || _sliding) return; // the tab is free-floating; Place() sizes it after the drop
        double to = on ? TabThicknessDip : SliverDip;
        var prop = VerticalEdge ? WidthProperty : HeightProperty;
        var other = VerticalEdge ? HeightProperty : WidthProperty;
        Tab.BeginAnimation(other, null);
        Tab.SetValue(other, double.NaN);
        if (animate)
        {
            // Always give a start value: while the size is Auto (NaN) an animation without one throws on every frame.
            double cur = VerticalEdge ? Tab.ActualWidth : Tab.ActualHeight;
            if (double.IsNaN(cur) || cur <= 0) cur = SliverDip;
            Tab.BeginAnimation(prop, new DoubleAnimation(cur, to, Quick) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Icon.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 1 : 0, Quick));
        }
        else
        {
            Tab.BeginAnimation(prop, null);
            Tab.SetValue(prop, to);
            Icon.BeginAnimation(OpacityProperty, null);
            Icon.Opacity = on ? 1 : 0;
        }
    }

    /// <summary>Show glued to the saved edge spot, or the default spot on the secondary monitor's right edge.</summary>
    /// <summary>
    /// Re-glue to the saved spot with a size for the monitor's current DPI: after a DPI change (mixed-scale monitors),
    /// a display change (monitor unplugged, laptop undocked) or a work-area change (taskbar moved/resized).
    /// </summary>
    private void Reposition()
    {
        if (!_placed || _dragging || !IsVisible) return;
        var s = _host.Config.Settings;
        var (len, thick) = TabSize();
        Place(LockButtonPlacement.Resolve(s.LockButtonX, s.LockButtonY, _host.Ws.GetMonitors(), len, thick));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Background);

    private void OnPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is Microsoft.Win32.UserPreferenceCategory.Desktop or Microsoft.Win32.UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void ShowAtSavedSpot()
    {
        Show();
        if (_placed) return;
        _placed = true;
        var s = _host.Config.Settings;
        var (len, thick) = TabSize();
        Place(LockButtonPlacement.Resolve(s.LockButtonX, s.LockButtonY, _host.Ws.GetMonitors(), len, thick));
        Peek(TimeSpan.FromSeconds(2.5));
    }

    /// <summary>Stay open for a moment, then fold smoothly back into the edge (first show: so the user sees where it
    /// lives; after a drop: so it visibly settles instead of snapping to a sliver).</summary>
    private void Peek(TimeSpan hold)
    {
        SetExpanded(true);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = hold };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!IsMouseOver && !_dragging && !_sliding) SetExpanded(ShouldStayOpen);
        };
        timer.Start();
    }

    private (int Length, int Thickness) TabSize()
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return ((int)Math.Round(TabLengthDip * scale), (int)Math.Round(TabThicknessDip * scale));
    }

    private void Place(TabRect t)
    {
        AlignToEdge(t.Edge);
        SetExpanded(IsMouseOver || ShouldStayOpen, animate: false);
        ApplyCorners();
        SetWindowPos(Hwnd, 0, t.X, t.Y, t.Width, t.Height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>The tab hugs <paramref name="edge"/>: it grows out of / folds into that side.</summary>
    private void AlignToEdge(ScreenEdge edge)
    {
        _edge = edge;
        Tab.HorizontalAlignment = edge switch { ScreenEdge.Left => HorizontalAlignment.Left, ScreenEdge.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Stretch };
        Tab.VerticalAlignment = edge switch { ScreenEdge.Top => VerticalAlignment.Top, ScreenEdge.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Stretch };
    }

    /// <summary>
    /// Last part of the glide: flatten against the edge and fold into the sliver while still moving, so the tab slides
    /// in one continuous motion instead of arriving, stopping, then folding.
    /// </summary>
    private void FoldIn(ScreenEdge edge, TimeSpan time)
    {
        AlignToEdge(edge);
        bool open = IsMouseOver || ShouldStayOpen;
        var prop = VerticalEdge ? WidthProperty : HeightProperty;
        var other = VerticalEdge ? HeightProperty : WidthProperty;
        double cur = VerticalEdge ? Tab.ActualWidth : Tab.ActualHeight;
        if (double.IsNaN(cur) || cur <= 0) cur = TabThicknessDip;
        Tab.BeginAnimation(other, null);
        Tab.SetValue(other, double.NaN);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        Tab.BeginAnimation(prop, new DoubleAnimation(cur, open ? TabThicknessDip : SliverDip, time) { EasingFunction = ease });
        Icon.BeginAnimation(OpacityProperty, new DoubleAnimation(open ? 1 : 0, time));
        BeginAnimation(RoundnessProperty, new DoubleAnimation(0, time) { EasingFunction = ease });
    }

    private void ApplyCorners()
    {
        if (Tab is null) return;
        var (tl, tr, br, bl) = LockButtonPlacement.Corners(_edge, Roundness, Radius);
        Tab.CornerRadius = new CornerRadius(tl, tr, br, bl);
    }

    private void AnimateRoundness(double to) =>
        BeginAnimation(RoundnessProperty, new DoubleAnimation(to, Quick) { EasingFunction = new QuadraticEase() });


    /// <summary>Glide from where the tab was dropped to its snapped spot, then flatten the side touching the edge.</summary>
    /// <summary>The fold-in follows the glide right away (never overlapping it: both at once made the moves arrive in clumps).</summary>
    private static readonly TimeSpan FoldTime = TimeSpan.FromMilliseconds(170);

    /// <summary>
    /// Glide to the snapped spot. Runs on its own thread paced by the compositor (DwmFlush = once per screen refresh),
    /// not by WPF's Rendering event, which fires in bursts and gaps and made the glide stutter. Only moves the window
    /// (posted, no repaint), so it costs next to nothing; the fold-in and the save run back on the UI thread.
    /// </summary>
    private void SlideTo(TabRect target, Action done)
    {
        GetWindowRect(Hwnd, out var r);
        var from = new TabRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, target.Edge);
        _edge = target.Edge; // still fully round (Roundness = 1) while gliding
        var hwnd = Hwnd;
        bool sameSize = from.Width == target.Width && from.Height == target.Height;
        uint flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS | (sameSize ? SWP_NOSIZE : 0);
        int version = ++_slideVersion;
        _sliding = true;
        var thread = new Thread(() =>
        {
            var clock = Stopwatch.StartNew();
            while (version == _slideVersion)
            {
                if (DwmFlush() != 0) Thread.Sleep(8); // no compositor sync available: ~120 Hz fallback
                double t = clock.Elapsed.TotalMilliseconds / SlideTime.TotalMilliseconds;
                var step = LockButtonPlacement.Interpolate(from, target, t);
                SetWindowPos(hwnd, 0, step.X, step.Y, step.Width, step.Height, flags);
                if (t >= 1) break;
            }
            Dispatcher.BeginInvoke(() =>
            {
                if (version != _slideVersion) return;
                _sliding = false;
                SetWindowPos(hwnd, 0, target.X, target.Y, target.Width, target.Height, SWP_NOZORDER | SWP_NOACTIVATE);
                FoldIn(target.Edge, FoldTime);
                done();
            });
        }) { IsBackground = true, Name = "LockTabGlide" };
        thread.Start();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

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
        _slideVersion++; // a new drag cancels a glide still running
        _sliding = false;
        _dragging = true;
        Tab.HorizontalAlignment = HorizontalAlignment.Stretch;
        Tab.VerticalAlignment = VerticalAlignment.Stretch;
        Tab.BeginAnimation(WidthProperty, null); Tab.Width = double.NaN;
        Tab.BeginAnimation(HeightProperty, null); Tab.Height = double.NaN;
        Icon.Opacity = 1;
        AnimateRoundness(1); // lift off the edge: symmetric rounded rectangle while dragging
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
        SlideTo(t, () =>
        {
            _dragging = false; // already flattened and folding in (FoldIn), so no size reset here
            _host.Config.Settings.LockButtonX = t.CenterX;
            _host.Config.Settings.LockButtonY = t.CenterY;
            // save (disk flush + main window refresh) only after the fold-in has played, so it can't cause a hitch
            var saveLater = new System.Windows.Threading.DispatcherTimer { Interval = FoldTime + TimeSpan.FromMilliseconds(80) };
            saveLater.Tick += (_, _) => { saveLater.Stop(); _host.SaveConfig(); };
            saveLater.Start();
        });
    }
}
