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

/// <summary>"지금 배치 저장": lists the windows on screen, pre-checks those on non-primary monitors, saves the checked ones.</summary>
public partial class SaveDialog : Window
{
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly List<Candidate> _items;

    private SaveDialog(IWindowSystem ws, string defaultName)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        _monitors = ws.GetMonitors();
        var windows = ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && w.ProcessId != Environment.ProcessId && !w.IsMinimized)
            .ToList();
        var preselected = PresetCapture.DefaultSelection(windows, _monitors).Where(w => !w.IsElevated).Select(w => w.Handle).ToHashSet();
        var ordered = _monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();

        _items = windows.Select(w =>
        {
            var m = Placement.MonitorOf(w.Rect, _monitors);
            var c = new Candidate(w, IconFor(w.ExePath), (w.IsElevated && !ws.SelfElevated ? "⚠ 관리자 권한 앱 (게임 등) · " : "") + $"모니터 {ordered.IndexOf(m) + 1}{(m.IsPrimary ? " (메인)" : "")} · {w.Rect.Width}×{w.Rect.Height}")
            {
                IsChecked = preselected.Contains(w.Handle),
            };
            c.PropertyChanged += (_, _) => Refresh();
            return c;
        }).OrderByDescending(c => c.IsChecked).ToList();

        WindowList.ItemsSource = _items;
        Preview.Monitors = _monitors;
        Preview.RectClicked += id =>
        {
            var c = _items.FirstOrDefault(i => i.Id == id);
            if (c is not null) c.IsChecked = !c.IsChecked;
        };
        NameBox.Text = defaultName;
        Refresh();
    }

    public Preset? Result { get; private set; }

    public static Preset? Show(Window owner, IWindowSystem ws, string defaultName)
    {
        var dlg = new SaveDialog(ws, defaultName) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

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
        Result = PresetCapture.Capture(NameBox.Text.Trim(), selected, _monitors);
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
