using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
/// Two uses: "새 배치 만들기" (lists the windows on screen, pre-checks those on non-primary monitors) and
/// "배치 수정" (pre-checks the windows already in the layout and saves their current positions back into it,
/// keeping each window's own settings).
/// </summary>
public partial class SaveDialog : Window
{
    private readonly IWindowSystem _ws;
    private readonly Preset? _editing;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private List<Candidate> _items = [];

    private SaveDialog(IWindowSystem ws, string defaultName, Preset? editing)
    {
        _ws = ws;
        _editing = editing;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        if (editing is not null)
        {
            Title = $"'{editing.Name}' 배치 수정";
            HeaderText.Text = "이 배치에 넣을 창을 고르세요";
            SubText.Text = "체크한 창들의 지금 위치와 크기로 배치가 바뀌어요. 수정하는 동안은 창 고정이 잠깐 풀려 있으니, 창을 원하는 자리로 옮긴 뒤 [↻ 지금 위치 다시 읽기]를 누르세요.";
            SaveButton.Content = "변경 저장";
        }
        else
        {
            RescanButton.Visibility = Visibility.Collapsed;
        }
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

    /// <summary>Edit <paramref name="preset"/> in place from the windows on screen; false when cancelled.</summary>
    public static bool Edit(Window owner, IWindowSystem ws, Preset preset)
    {
        var dlg = new SaveDialog(ws, preset.Name, preset) { Owner = owner };
        return dlg.ShowDialog() == true;
    }

    private void LoadWindows(ISet<nint>? keepChecks)
    {
        _monitors = _ws.GetMonitors();
        var windows = _ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && w.ProcessId != Environment.ProcessId && !w.IsMinimized)
            .ToList();

        ISet<nint> preselected;
        if (keepChecks is not null) preselected = keepChecks;
        else if (_editing is not null)
        {
            // the windows the layout's entries resolve to right now
            var taken = new HashSet<nint>();
            foreach (var e in _editing.Windows)
                if (WindowMatcher.PickBest(e.Match, windows, taken) is { } w) taken.Add(w.Handle);
            preselected = taken;
        }
        else
        {
            // admin apps stay unchecked only when WindowFix itself can't move them
            preselected = PresetCapture.DefaultSelection(windows, _monitors)
                .Where(w => !(w.IsElevated && !_ws.SelfElevated)).Select(w => w.Handle).ToHashSet();
        }

        var ordered = _monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();
        _items = windows.Select(w =>
        {
            var m = Placement.MonitorOf(w.Rect, _monitors);
            var warn = w.IsElevated && !_ws.SelfElevated ? "⚠ 관리자 권한 앱 (게임 등) · " : "";
            var c = new Candidate(w, IconFor(w.ExePath), $"{warn}모니터 {ordered.IndexOf(m) + 1}{(m.IsPrimary ? " (메인)" : "")} · {w.Rect.Width}×{w.Rect.Height}")
            {
                IsChecked = preselected.Contains(w.Handle),
            };
            c.PropertyChanged += (_, _) => Refresh();
            return c;
        }).OrderByDescending(c => c.IsChecked).ToList();

        WindowList.ItemsSource = _items;
        Preview.Monitors = _monitors;
        Refresh();
    }

    private void OnRescan(object sender, RoutedEventArgs e) =>
        LoadWindows(_items.Where(i => i.IsChecked).Select(i => i.Window.Handle).ToHashSet());

    private void Refresh()
    {
        // Checked windows drawn last so they sit on top of unchecked ones.
        Preview.Rects = _items
            .OrderBy(c => c.IsChecked)
            .Select(c => new PreviewRect(c.Id, c.Program, c.Window.Rect, c.IsChecked))
            .ToList();
        int n = _items.Count(c => c.IsChecked);
        CountText.Text = $"{n}개 창 선택됨";
        SaveButton.IsEnabled = n > 0 && NameBox.Text.Trim().Length > 0;
    }

    private void OnNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_items is not null) Refresh();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(c => c.IsChecked).Select(c => c.Window).ToList();
        if (selected.Count == 0 || NameBox.Text.Trim().Length == 0) return;
        var fresh = PresetCapture.Capture(NameBox.Text.Trim(), selected, _monitors);
        if (_editing is null)
        {
            Result = fresh;
            DialogResult = true;
            return;
        }

        // Keep each window's own settings (auto-launch, name, title rule...) when it was already in the layout.
        var windows = _ws.GetWindows(includeHidden: false);
        var taken = new HashSet<nint>();
        var oldByHandle = new Dictionary<nint, WindowEntry>();
        foreach (var old in _editing.Windows)
            if (WindowMatcher.PickBest(old.Match, windows, taken) is { } w) { taken.Add(w.Handle); oldByHandle[w.Handle] = old; }

        var merged = new List<WindowEntry>();
        for (int i = 0; i < selected.Count; i++)
        {
            var neu = fresh.Windows[i];
            if (oldByHandle.TryGetValue(selected[i].Handle, out var old))
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

    public sealed class Candidate(WindowInfo window, ImageSource? icon, string detail) : INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();
        public WindowInfo Window { get; } = window;
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
