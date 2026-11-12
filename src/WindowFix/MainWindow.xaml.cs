using System.Windows;
using System.Windows.Controls;
using WindowFix.Controls;
using WindowFix.Core.Apply;
using WindowFix.Core.Layout;
using WindowFix.Core.Lock;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;

namespace WindowFix;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private bool _refreshing;
    private Guid? _selectedEntry;

    public MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        Preview.RectClicked += SelectEntry;
        _host.ConfigChanged += () => Dispatcher.Invoke(RefreshAll);
        _host.Lock.StateChanged += _ => Dispatcher.Invoke(RefreshToggles);
        _host.Applied += (p, r) => Dispatcher.Invoke(() => ShowReport(p, r));
        RefreshAll();
    }

    private Preset? Selected => PresetCombo.SelectedItem as Preset;

    // ---------- refresh ----------

    private void RefreshAll()
    {
        _refreshing = true;
        var current = _host.CurrentPreset;
        PresetCombo.ItemsSource = null;
        PresetCombo.ItemsSource = _host.Config.Presets;
        PresetCombo.SelectedItem = current;
        _refreshing = false;
        RefreshPreview();
        RefreshToggles();
    }

    private void RefreshToggles()
    {
        var s = _host.Config.Settings;
        LockCheck.IsChecked = s.LockEnabled;
        LockCheck.Content = _host.Lock.State == LockState.Paused ? "🔓 잠금 켜짐 · 일시 해제 중" : "🔒 잠금";
        TrayCheck.IsChecked = s.ShowTrayIcon;
        ButtonCheck.IsChecked = s.ShowLockButton;
        StartupCheck.IsChecked = Selected is { } p && s.StartupPresetId == p.Id;
        bool has = Selected is not null;
        ApplyButton.IsEnabled = RenameButton.IsEnabled = DeleteButton.IsEnabled = StartupCheck.IsEnabled = has;
    }

    private void RefreshPreview()
    {
        var monitors = _host.Ws.GetMonitors();
        Preview.Monitors = monitors;
        var preset = Selected;
        EmptyHint.Visibility = preset is null || preset.Windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (preset is null)
        {
            Preview.Rects = [];
            SelectEntry(null);
            return;
        }

        var rects = new List<PreviewRect>();
        foreach (var e in preset.Windows)
        {
            var m = MonitorResolver.Resolve(e.Monitor, monitors);
            if (m is null) continue;
            var abs = e.Maximized ? m.WorkArea : PresetApplier.TargetRect(e, m);
            rects.Add(new PreviewRect(e.Id, e.DisplayName, abs, true));
        }
        Preview.Rects = rects;
        if (_selectedEntry is { } id && preset.Windows.All(w => w.Id != id)) SelectEntry(null);
    }

    private void ShowReport(Preset p, ApplyReport report)
    {
        var problems = report.Results
            .Where(r => r.Outcome != ApplyOutcome.Applied)
            .Select(r => $"{p.Windows.FirstOrDefault(w => w.Id == r.EntryId)?.DisplayName ?? "?"}: {Describe(r.Outcome)}")
            .ToList();
        StatusText.Text = problems.Count == 0
            ? $"'{p.Name}' 적용 완료 ({report.Results.Count}개 창)"
            : $"'{p.Name}' 적용 · " + string.Join(" · ", problems);
    }

    private static string Describe(ApplyOutcome o) => o switch
    {
        ApplyOutcome.NotFound => "창을 찾지 못함",
        ApplyOutcome.MonitorMissing => "모니터 없음 → 건너뜀",
        ApplyOutcome.Failed => "옮길 수 없음(관리자 권한 창?)",
        ApplyOutcome.LaunchedWaiting => "실행함 · 창이 뜨면 다시 적용하세요",
        _ => "완료",
    };

    // ---------- preset bar ----------

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        SelectEntry(null);
        RefreshPreview();
        RefreshToggles();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) _host.ApplyPreset(p.Id);
    }

    private void OnSaveCurrent(object sender, RoutedEventArgs e)
    {
        var monitors = _host.Ws.GetMonitors();
        var candidates = _host.Ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && w.ProcessId != Environment.ProcessId)
            .ToList();
        var selected = PresetCapture.DefaultSelection(candidates, monitors);
        if (selected.Count == 0)
        {
            StatusText.Text = "서브모니터에 저장할 창이 없어요.";
            return;
        }
        AddPreset(PresetCapture.Capture($"프리셋 {_host.Config.Presets.Count + 1}", selected, monitors));
    }

    private void AddPreset(Preset preset)
    {
        _host.Config.Presets.Add(preset);
        _host.Config.Settings.CurrentPresetId = preset.Id;
        _host.SaveConfig();
        StatusText.Text = $"'{preset.Name}' 저장됨 ({preset.Windows.Count}개 창)";
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("새 이름", "프리셋 이름 변경", p.Name).Trim();
        if (name.Length == 0) return;
        p.Name = name;
        _host.SaveConfig();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        if (MessageBox.Show(this, $"'{p.Name}' 프리셋을 삭제할까요?", "프리셋 삭제",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _host.Config.Presets.Remove(p);
        var s = _host.Config.Settings;
        if (s.StartupPresetId == p.Id) s.StartupPresetId = null;
        if (s.CurrentPresetId == p.Id) s.CurrentPresetId = _host.Config.Presets.FirstOrDefault()?.Id;
        _host.SaveConfig();
        _host.RefreshLock();
    }

    // ---------- toggles ----------

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        bool on = LockCheck.IsChecked == true;
        if (on && Selected is { } p && _host.Config.Settings.CurrentPresetId != p.Id)
            _host.Config.Settings.CurrentPresetId = p.Id; // lock the preset the user is looking at
        _host.SetLockEnabled(on);
        StatusText.Text = on ? $"잠금 켜짐: '{_host.CurrentPreset?.Name}'" : "잠금 꺼짐";
    }

    private void OnTrayClick(object sender, RoutedEventArgs e)
    {
        _host.Config.Settings.ShowTrayIcon = TrayCheck.IsChecked == true;
        _host.SaveConfig();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        _host.Config.Settings.ShowLockButton = ButtonCheck.IsChecked == true;
        _host.SaveConfig();
    }

    private void OnStartupClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        _host.Config.Settings.StartupPresetId = StartupCheck.IsChecked == true ? p.Id : null;
        _host.SaveConfig();
    }

    // ---------- edit panel ----------

    private void SelectEntry(Guid? id)
    {
        _selectedEntry = id;
        Preview.Selected = id;
        var entry = id is null ? null : Selected?.Windows.FirstOrDefault(w => w.Id == id);
        EditPanel.Visibility = entry is null ? Visibility.Collapsed : Visibility.Visible;
        if (entry is null) return;

        NameBox.Text = entry.DisplayName;
        XBox.Text = entry.Rect.X.ToString();
        YBox.Text = entry.Rect.Y.ToString();
        WBox.Text = entry.Rect.Width.ToString();
        HBox.Text = entry.Rect.Height.ToString();
        MaxCheck.IsChecked = entry.Maximized;
        TitleBox.Text = entry.Match.TitleContains ?? "";
        LaunchCheck.IsChecked = entry.LaunchIfMissing;
        PathBox.Text = entry.LaunchPath ?? "";
        ArgsBox.Text = entry.LaunchArgs ?? "";
        MatchInfo.Text = $"프로그램: {entry.Match.ExeName}" + (entry.Match.ClassName is { } c ? $" · 창 종류: {c}" : "");
    }

    private void SelectEntry(Guid id) => SelectEntry((Guid?)id);

    private void OnEntrySave(object sender, RoutedEventArgs e)
    {
        var entry = _selectedEntry is { } id ? Selected?.Windows.FirstOrDefault(w => w.Id == id) : null;
        if (entry is null) return;
        if (!int.TryParse(XBox.Text, out int x) || !int.TryParse(YBox.Text, out int y) ||
            !int.TryParse(WBox.Text, out int w) || !int.TryParse(HBox.Text, out int h) || w < 1 || h < 1)
        {
            StatusText.Text = "위치와 크기는 숫자로 입력하세요 (너비·높이는 1 이상).";
            return;
        }

        entry.DisplayName = NameBox.Text.Trim().Length > 0 ? NameBox.Text.Trim() : entry.DisplayName;
        entry.Rect = new RelRect(x, y, w, h);
        entry.Maximized = MaxCheck.IsChecked == true;
        entry.Match.TitleContains = string.IsNullOrWhiteSpace(TitleBox.Text) ? null : TitleBox.Text.Trim();
        entry.LaunchIfMissing = LaunchCheck.IsChecked == true;
        entry.LaunchPath = string.IsNullOrWhiteSpace(PathBox.Text) ? null : PathBox.Text.Trim();
        entry.LaunchArgs = string.IsNullOrWhiteSpace(ArgsBox.Text) ? null : ArgsBox.Text.Trim();
        _host.SaveConfig();
        _host.RefreshLock();
        SelectEntry(entry.Id);
        StatusText.Text = $"'{entry.DisplayName}' 설정 저장됨";
    }

    private void OnEntryRemove(object sender, RoutedEventArgs e)
    {
        var preset = Selected;
        var entry = _selectedEntry is { } id ? preset?.Windows.FirstOrDefault(w => w.Id == id) : null;
        if (preset is null || entry is null) return;
        preset.Windows.Remove(entry);
        SelectEntry(null);
        _host.SaveConfig();
        _host.RefreshLock();
    }
}
