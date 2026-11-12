using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowFix.Controls;
using WindowFix.Core.Apply;
using WindowFix.Core.Layout;
using WindowFix.Core.Lock;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;
using ThemeMode = WindowFix.Core.Model.ThemeMode;

namespace WindowFix;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private bool _refreshing;
    private bool _selectCurrentOnRefresh;
    private Guid? _selectedEntry;

    public MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        ThemeManager.Changed += () => { Preview.InvalidateVisual(); RefreshToggles(); };
        Preview.RectClicked += SelectEntry;
        _host.ConfigChanged += () => Dispatcher.Invoke(RefreshAll);
        _host.Lock.StateChanged += _ => Dispatcher.Invoke(RefreshToggles);
        _host.Applied += (p, r) => Dispatcher.Invoke(() => ShowReport(p, r));
        RefreshAll();
    }

    private Preset? Selected => PresetList.SelectedItem as Preset;

    // ---------- refresh ----------

    private void RefreshAll()
    {
        _refreshing = true;
        // Keep showing the layout the user picked; fall back to the current one (e.g. after a delete or a new save).
        var keep = Selected is { } sel && _host.Config.Presets.Contains(sel) && !_selectCurrentOnRefresh ? sel : _host.CurrentPreset;
        _selectCurrentOnRefresh = false;
        PresetList.ItemsSource = null;
        PresetList.ItemsSource = _host.Config.Presets;
        PresetList.SelectedItem = keep;
        _refreshing = false;
        RefreshPreview();
        RefreshToggles();
    }

    private void RefreshToggles()
    {
        var s = _host.Config.Settings;
        bool has = Selected is not null;
        LockCheck.IsChecked = s.LockEnabled;
        LockCheck.IsEnabled = _host.Config.Presets.Count > 0;
        PresetTitle.Text = Selected?.Name ?? "배치 없음";
        bool locksThis = has && _host.CurrentPreset?.Id == Selected!.Id;
        var (label, hint, bg, border, chip, chipBg, chipFg) = _host.Lock.State switch
        {
            LockState.Active => ("🔒 창 고정 중", $"'{_host.CurrentPreset?.Name}' 배치를 지키고 있어요.", "Lock.Active.Bg", "Lock.Active.Border",
                locksThis ? "● 고정 중" : "", "Chip.Active.Bg", "Chip.Active.Fg"),
            LockState.Paused => ("🔓 잠깐 풀어 둠", "고정 탭을 다시 누르면 원래 자리로 돌아가요.", "Lock.Paused.Bg", "Lock.Paused.Border",
                locksThis ? "잠깐 풀어 둠" : "", "Chip.Paused.Bg", "Chip.Paused.Fg"),
            _ => ("🔒 창 고정", _host.Config.Presets.Count == 0 ? "먼저 배치를 저장하세요." : "켜면 창이 저장한 자리에서 움직이지 않아요.",
                "Lock.Off.Bg", "Lock.Off.Border", has ? "고정 안 함" : "", "Chip.Off.Bg", "Chip.Off.Fg"),
        };
        LockLabel.Text = label;
        LockHint.Text = hint;
        LockCard.SetResourceReference(Border.BackgroundProperty, bg);
        LockCard.SetResourceReference(Border.BorderBrushProperty, border);
        StatusChipText.Text = chip;
        StatusChip.Visibility = chip.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusChip.SetResourceReference(Border.BackgroundProperty, chipBg);
        StatusChipText.SetResourceReference(TextBlock.ForegroundProperty, chipFg);

        TrayCheck.IsChecked = s.ShowTrayIcon;
        ButtonCheck.IsChecked = s.ShowLockButton;
        var current = _host.CurrentPreset;
        StartupCheck.IsChecked = current is not null && s.StartupPresetId == current.Id;
        StartupCheck.IsEnabled = current is not null;
        StartupLabel.Text = current is null ? "컴퓨터 켤 때 배치 맞추기" : $"컴퓨터 켤 때 '{current.Name}' 배치로 맞추기";
        ThemeSystem.IsChecked = s.Theme == Core.Model.ThemeMode.System;
        ThemeLight.IsChecked = s.Theme == Core.Model.ThemeMode.Light;
        ThemeDark.IsChecked = s.Theme == Core.Model.ThemeMode.Dark;
        ApplyButton.IsEnabled = RenameButton.IsEnabled = DeleteButton.IsEnabled = has;
    }

    private void ShowSettings(bool on)
    {
        SettingsPage.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        LayoutPage.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        SettingsNav.IsChecked = on;
        if (on) PresetList.SelectedItem = null;
    }

    private void OnSettingsNav(object sender, RoutedEventArgs e) => ShowSettings(true);

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        _host.Config.Settings.Theme = sender == ThemeLight ? Core.Model.ThemeMode.Light : sender == ThemeDark ? Core.Model.ThemeMode.Dark : Core.Model.ThemeMode.System;
        _host.SaveConfig();
        ThemeManager.Reapply();
    }

    private void RefreshPreview()
    {
        var monitors = _host.Ws.GetMonitors();
        Preview.Monitors = monitors;
        var preset = Selected;
        bool empty = _host.Config.Presets.Count == 0;
        Guide.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        PreviewHint.Visibility = preset is { Windows.Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        if (empty && string.IsNullOrEmpty(StatusText.Text)) StatusText.Text = "환영해요! 가운데 안내를 따라 첫 배치를 저장해 보세요.";
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
            ? $"✔ '{p.Name}' 배치로 맞췄어요 ({report.Results.Count}개 창)."
            : $"'{p.Name}' 배치로 맞췄어요. 단,\n  · " + string.Join("\n  · ", problems);
    }

    private static string Describe(ApplyOutcome o) => o switch
    {
        ApplyOutcome.NotFound => "창이 안 보여서 건너뛰었어요 (프로그램이 켜져 있는지 확인하세요)",
        ApplyOutcome.MonitorMissing => "그 모니터가 연결돼 있지 않아 건너뛰었어요",
        ApplyOutcome.Failed => "옮길 수 없었어요 (관리자 권한으로 실행된 창일 수 있어요)",
        ApplyOutcome.LaunchedWaiting => "프로그램을 켜는 중이에요. 창이 뜨면 [이 배치로 맞추기]를 한 번 더 눌러 주세요",
        ApplyOutcome.LaunchFailed => "프로그램을 켤 수 없었어요 (창 설정 → 고급 설정의 실행할 파일을 확인하세요)",
        _ => "완료",
    };

    // ---------- preset bar ----------

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        if (Selected is null) return; // cleared while the settings page is shown
        ShowSettings(false);
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
        var preset = SaveDialog.Show(this, _host.Ws, _host.Config.Presets.Count == 0 ? "평소 배치" : $"배치 {_host.Config.Presets.Count + 1}");
        if (preset is not null) AddPreset(preset);
    }

    private void AddPreset(Preset preset)
    {
        _host.Config.Presets.Add(preset);
        _host.Config.Settings.CurrentPresetId = preset.Id;
        _selectCurrentOnRefresh = true;
        _host.SaveConfig();
        _host.RefreshLock(); // a running lock now guards the layout just saved (windows are already in place)
        StatusText.Text = _host.Config.Settings.LockEnabled
            ? $"✔ '{preset.Name}' 저장했어요. 이제 이 배치를 고정하고 있어요."
            : $"✔ '{preset.Name}' 저장했어요! 이제 아래 [🔒 창 고정]을 켜면 창이 이 자리에 고정돼요.";
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        var name = Microsoft.VisualBasic.Interaction.InputBox("새 이름을 입력하세요.", "배치 이름 바꾸기", p.Name).Trim();
        if (name.Length == 0) return;
        p.Name = name;
        _host.SaveConfig();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        if (MessageBox.Show(this, $"'{p.Name}' 배치를 삭제할까요?\n(창이나 프로그램은 그대로 있어요. 저장한 자리 정보만 지워져요.)", "배치 삭제",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _host.Config.Presets.Remove(p);
        var s = _host.Config.Settings;
        if (s.StartupPresetId == p.Id) s.StartupPresetId = null;
        bool wasLocked = s.CurrentPresetId == p.Id && s.LockEnabled;
        if (s.CurrentPresetId == p.Id) s.CurrentPresetId = _host.Config.Presets.FirstOrDefault()?.Id;
        _selectCurrentOnRefresh = true;
        if (wasLocked)
        {
            // Never silently start guarding a different layout (windows would jump).
            _host.SetLockEnabled(false);
            StatusText.Text = $"고정하던 '{p.Name}' 배치를 지워서 창 고정을 껐어요.";
        }
        else
        {
            _host.SaveConfig();
            StatusText.Text = $"'{p.Name}' 배치를 지웠어요.";
        }
    }

    // ---------- toggles ----------

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        bool on = LockCheck.IsChecked == true;
        if (on && Selected is { } p && _host.Config.Settings.CurrentPresetId != p.Id)
            _host.Config.Settings.CurrentPresetId = p.Id; // lock the preset the user is looking at
        _host.SetLockEnabled(on);
        StatusText.Text = on
            ? $"🔒 '{_host.CurrentPreset?.Name}' 배치를 고정했어요. 이 창을 닫아도 계속 지켜요. 컴퓨터를 다시 켜도 자동으로 켜져요."
            : "창 고정을 껐어요. 이제 창을 자유롭게 옮길 수 있어요.";
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
        if (_host.CurrentPreset is not { } p) return;
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

        EditTitle.Text = $"{entry.DisplayName} 설정";
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

    private void OnEditClose(object sender, RoutedEventArgs e) => SelectEntry(null);

    private void OnEntrySave(object sender, RoutedEventArgs e)
    {
        var entry = _selectedEntry is { } id ? Selected?.Windows.FirstOrDefault(w => w.Id == id) : null;
        if (entry is null) return;
        if (!int.TryParse(XBox.Text, out int x) || !int.TryParse(YBox.Text, out int y) ||
            !int.TryParse(WBox.Text, out int w) || !int.TryParse(HBox.Text, out int h) || w < 1 || h < 1)
        {
            StatusText.Text = "위치와 크기에는 숫자만 넣어 주세요 (너비·높이는 1 이상).";
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
        StatusText.Text = $"✔ '{entry.DisplayName}' 설정을 저장했어요. [이 배치로 맞추기]를 누르면 바로 반영돼요.";
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
