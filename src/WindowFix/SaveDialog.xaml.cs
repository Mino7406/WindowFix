using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowFix.Controls;
using WindowFix.Core.Apply;
using WindowFix.Core.Layout;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;
using WindowFix.Core.Platform;

namespace WindowFix;

/// <summary>
/// Two uses:
/// "새 배치 만들기" lists the windows on screen and pre-checks those on non-primary monitors.
/// "배치 수정" opens on the monitor that was clicked: that monitor is shown large, the others as small thumbnails
/// (click one to switch). Saving writes the checked windows' current positions back into the layout, keeps each
/// window's own settings, and keeps entries whose app isn't open right now untouched.
/// </summary>
public partial class SaveDialog : Window
{
    private readonly IWindowSystem _ws;
    private readonly Preset? _editing;
    private string? _monitorPath; // edit mode: the monitor shown large
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private List<Candidate> _items = [];
    private bool _loaded;

    private SaveDialog(IWindowSystem ws, string defaultName, Preset? editing, string? monitorPath = null)
    {
        _ws = ws;
        _editing = editing;
        _monitorPath = monitorPath;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        if (editing is not null) SaveButton.Content = "변경 저장";
        // Coming back from moving/opening a window elsewhere: read the screen again so the list is never stale.
        Activated += (_, _) => { if (_loaded) Rescan(); };
        ContentRendered += (_, _) => _loaded = true;
        Preview.RectClicked += id =>
        {
            var c = _items.FirstOrDefault(i => i.Id == id);
            if (c is not null) c.IsChecked = !c.IsChecked;
        };
        NameBox.Text = editing?.Name ?? defaultName;
        LoadWindows(keepChecks: null);
    }

    public Preset? Result { get; private set; }

    /// <summary>New layout from the windows on screen; null when cancelled.</summary>
    public static Preset? Show(Window owner, IWindowSystem ws, string defaultName)
    {
        var dlg = new SaveDialog(ws, defaultName, editing: null) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

    /// <summary>Edit <paramref name="preset"/> in place, starting on <paramref name="monitorPath"/>; false when cancelled.</summary>
    public static bool Edit(Window owner, IWindowSystem ws, Preset preset, string monitorPath)
    {
        var dlg = new SaveDialog(ws, preset.Name, preset, monitorPath) { Owner = owner };
        return dlg.ShowDialog() == true;
    }

    private bool Editing => _editing is not null;

    private void LoadWindows(ISet<nint>? keepChecks, ISet<nint>? seen = null)
    {
        _monitors = _ws.GetMonitors();
        if (Editing && _monitors.All(m => m.DevicePath != _monitorPath)) _monitorPath = _monitors.FirstOrDefault()?.DevicePath;
        var windows = _ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && w.ProcessId != Environment.ProcessId && !w.IsMinimized)
            .ToList();

        ISet<nint> preselected = Editing
            ? ResolveEntries(windows).Keys.ToHashSet()
            // admin apps stay unchecked only when WindowFix itself can't move them
            : PresetCapture.DefaultSelection(windows, _monitors)
                .Where(w => !(w.IsElevated && !_ws.SelfElevated)).Select(w => w.Handle).ToHashSet();
        if (keepChecks is not null)
        {
            // windows already listed keep the user's choice; windows that just appeared get the default
            var fresh = preselected.Where(h => seen is null || !seen.Contains(h));
            preselected = keepChecks.Concat(fresh).ToHashSet();
        }

        var ordered = Ordered(_monitors);
        _items = windows.Select(w =>
        {
            var m = Placement.MonitorOf(w.Rect, _monitors);
            var warn = w.IsElevated && !_ws.SelfElevated ? "⚠ 관리자 권한 앱 (게임 등) · " : "";
            var c = new Candidate(w, m.DevicePath, IconFor(w.ExePath),
                $"{warn}모니터 {ordered.IndexOf(m) + 1}{(m.IsPrimary ? " (메인)" : "")} · {w.Rect.Width}×{w.Rect.Height}")
            {
                IsChecked = preselected.Contains(w.Handle),
            };
            c.PropertyChanged += (_, _) => Refresh();
            return c;
        }).OrderByDescending(c => c.IsChecked).ToList();

        ShowMonitor();
    }

    /// <summary>Live window → existing layout entry, for the entries whose app is open right now.</summary>
    private Dictionary<nint, WindowEntry> ResolveEntries(IReadOnlyList<WindowInfo> windows)
    {
        var map = new Dictionary<nint, WindowEntry>();
        var taken = new HashSet<nint>();
        foreach (var e in _editing!.Windows)
            if (WindowMatcher.PickBest(e.Match, windows, taken) is { } w) { taken.Add(w.Handle); map[w.Handle] = e; }
        return map;
    }

    private static List<MonitorInfo> Ordered(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();

    private string Label(string? path)
    {
        var ordered = Ordered(_monitors);
        int i = ordered.FindIndex(m => m.DevicePath == path);
        return i < 0 ? "이 모니터" : $"모니터 {i + 1}{(ordered[i].IsPrimary ? " (메인)" : "")}";
    }

    /// <summary>Edit mode: the chosen monitor large, the list filtered to it, the other monitors as thumbnails.</summary>
    private void ShowMonitor()
    {
        if (Editing)
        {
            var label = Label(_monitorPath);
            Title = $"'{_editing!.Name}' 배치 수정 · {label}";
            HeaderText.Text = $"{label}에 둘 창을 고르세요";
            SubText.Text = "체크한 창들의 지금 위치와 크기로 배치가 바뀌어요. 수정하는 동안은 창 고정이 잠깐 풀려 있으니, " +
                           "창을 옮기고 이 창으로 돌아오면 알아서 다시 읽어요([↻ 새로고침]으로도 돼요). 아래 작은 모니터를 누르면 그 모니터로 바뀌어요.";
            WindowList.ItemsSource = _items.Where(i => i.MonitorPath == _monitorPath).ToList();
            Preview.Monitors = _monitors.Where(m => m.DevicePath == _monitorPath).ToList();
            BuildThumbnails();
        }
        else
        {
            WindowList.ItemsSource = _items;
            Preview.Monitors = _monitors;
            ThumbPanel.Visibility = Visibility.Collapsed;
        }
        Refresh();
    }

    private void BuildThumbnails()
    {
        ThumbList.Children.Clear();
        var others = Ordered(_monitors).Where(m => m.DevicePath != _monitorPath).ToList();
        ThumbPanel.Visibility = others.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var m in others)
        {
            var path = m.DevicePath;
            var mini = new MonitorPreview
            {
                Monitors = [m],
                Rects = _items.Where(i => i.MonitorPath == path && i.IsChecked)
                    .Select(i => new PreviewRect(i.Id, i.Program, i.Window.Rect, true)).ToList(),
                ClickSelectsRects = false,
                Height = 92,
                Width = 92.0 * m.Bounds.Width / Math.Max(1, m.Bounds.Height) + 20,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{Label(path)} 크게 보기",
            };
            mini.MonitorClicked += _ => { _monitorPath = path; ShowMonitor(); };
            var card = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            card.Children.Add(mini);
            ThumbList.Children.Add(card);
        }
    }

    private void OnRescan(object sender, RoutedEventArgs e) => Rescan();

    /// <summary>Re-read windows and positions; keeps the user's checks, new windows get the usual default.</summary>
    private void Rescan() =>
        LoadWindows(_items.Where(i => i.IsChecked).Select(i => i.Window.Handle).ToHashSet(),
                    _items.Select(i => i.Window.Handle).ToHashSet());

    private void Refresh()
    {
        var shown = Editing ? _items.Where(i => i.MonitorPath == _monitorPath) : _items;
        // Checked windows drawn last so they sit on top of unchecked ones.
        Preview.Rects = shown
            .OrderBy(c => c.IsChecked)
            .Select(c => new PreviewRect(c.Id, c.Program, c.Window.Rect, c.IsChecked))
            .ToList();
        int n = _items.Count(c => c.IsChecked);
        CountText.Text = Editing ? $"전체 {n}개 창 선택됨" : $"{n}개 창 선택됨";
        SaveButton.IsEnabled = (n > 0 || Editing) && NameBox.Text.Trim().Length > 0;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_items is not null && SaveButton is not null) Refresh();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(c => c.IsChecked).Select(c => c.Window).ToList();
        if ((selected.Count == 0 && !Editing) || NameBox.Text.Trim().Length == 0) return;
        var fresh = PresetCapture.Capture(NameBox.Text.Trim(), selected, _monitors);
        if (!Editing)
        {
            Result = fresh;
            DialogResult = true;
            return;
        }

        // Entries whose app is open map to a live window: kept (with their own settings) only if still checked.
        // Entries whose app isn't open right now can't be shown here, so they stay exactly as they were.
        // Same filter as the list: a minimized/hidden window was never shown, so its entry must not count as "unchecked".
        var live = ResolveEntries(_ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && w.ProcessId != Environment.ProcessId && !w.IsMinimized).ToList());
        var merged = _editing!.Windows.Where(en => !live.ContainsValue(en)).ToList();
        for (int i = 0; i < selected.Count; i++)
        {
            var neu = fresh.Windows[i];
            if (live.TryGetValue(selected[i].Handle, out var old))
            {
                old.Monitor = neu.Monitor;
                old.Rect = neu.Rect;
                old.Maximized = neu.Maximized;
                old.Match.SavedTitle = neu.Match.SavedTitle;
                merged.Add(old);
            }
            else merged.Add(neu);
        }
        _editing.Name = fresh.Name;
        _editing.Windows = merged;
        Result = _editing;
        DialogResult = true;
    }

    private static ImageSource? IconFor(string? exePath)
    {
        if (exePath is null) return null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon is null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public sealed class Candidate(WindowInfo window, string monitorPath, ImageSource? icon, string detail) : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        public WindowInfo Window { get; } = window;
        public string MonitorPath { get; } = monitorPath;
        public ImageSource? Icon { get; } = icon;
        public string Program { get; } = System.IO.Path.GetFileNameWithoutExtension(window.ExeName);
        public string Title { get; } = window.Title;
        public string Detail { get; } = detail;

        public bool IsChecked
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                OnChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
