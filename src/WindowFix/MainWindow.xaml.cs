using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool _selectCurrentOnRefresh;
    private Guid? _selectedEntry;
    private Preset? _beforeSettings;
    private bool _settingsJustOpened;

    public MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        ThemeManager.Changed += () => { Preview.InvalidateVisual(); RefreshPreview(); RefreshToggles(); };
        Preview.ClickSelectsRects = false;
        Preview.MonitorClicked += EditLayout;
        _host.ConfigChanged += () => Dispatcher.Invoke(RefreshAll);
        _host.Lock.StateChanged += _ => Dispatcher.Invoke(RefreshToggles);
        _host.Applied += (p, r) => Dispatcher.Invoke(() => ShowReport(p, r));
        _host.UpdateAvailable += r => Dispatcher.Invoke(() => ShowUpdate(r));
        if (_host.PendingUpdate is { } pending) ShowUpdate(pending);
        System.ComponentModel.DependencyPropertyDescriptor
            .FromProperty(TextBlock.TextProperty, typeof(TextBlock))
            .AddValueChanged(StatusText, (_, _) => OnStatusChanged());
        RefreshAll();
    }

    /// <summary>Show the status as a callout (orange when something needs attention) and nudge it in so it gets noticed.</summary>
    private void OnStatusChanged()
    {
        var text = StatusText.Text;
        if (string.IsNullOrWhiteSpace(text)) { StatusBox.Visibility = Visibility.Collapsed; return; }        bool warn = text.Contains("단,") || text.Contains("못") || text.Contains("취소") || text.Contains("없어요") || text.Contains("⚠");
        StatusBox.SetResourceReference(Border.BackgroundProperty, warn ? "Lock.Paused.Bg" : "Bg.Selected");
        StatusBox.SetResourceReference(Border.BorderBrushProperty, warn ? "Lock.Paused.Fg" : "Accent");
        StatusBar.SetResourceReference(Border.BackgroundProperty, warn ? "Lock.Paused.Fg" : "Accent");
        StatusBox.Visibility = Visibility.Visible;
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var t = TimeSpan.FromMilliseconds(220);
        StatusShift.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(6, 0, t) { EasingFunction = ease });
        StatusBox.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, t));
    }

    private Preset? Selected => PresetList.SelectedItem as Preset;
    private bool SettingsShown => SettingsPage.Visibility == Visibility.Visible;

    // ---------- refresh ----------

    private void RefreshAll()
    {
        _refreshing = true;
        // Keep showing the layout the user picked; fall back to the current one (e.g. after a delete or a new save).
        // While the settings page is open nothing stays selected, so any click on a layout opens it.
        var keep = SettingsShown ? null
            : Selected is { } sel && _host.Config.Presets.Contains(sel) && !_selectCurrentOnRefresh ? sel : _host.CurrentPreset;
        _selectCurrentOnRefresh = false;
        PresetList.ItemsSource = null;
        PresetList.ItemsSource = _host.Config.Presets;
        PresetList.SelectedItem = keep;
        StartupPresetList.ItemsSource = null;
        StartupPresetList.ItemsSource = _host.Config.Presets;
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
            LockState.Paused => ("🔓 잠깐 풀어 둠", "위젯을 다시 누르면 원래 자리로 돌아가요.", "Lock.Paused.Bg", "Lock.Paused.Border",
                locksThis ? "잠깐 풀어 둠" : "", "Chip.Paused.Bg", "Chip.Paused.Fg"),
            _ => ("🔒 창 고정", _host.Config.Presets.Count == 0 ? "먼저 배치를 만드세요." : "켜면 창이 저장한 자리에서 움직이지 않아요.",
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
        LayoutActions.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        // The lock already holds this layout in place, so "apply" would do nothing new: hide it.
        bool lockOn = _host.Lock.State != LockState.Off;
        ApplyButton.Visibility = lockOn && locksThis ? Visibility.Collapsed : Visibility.Visible;
        RenameButton.Margin = ApplyButton.Visibility == Visibility.Visible ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        ApplyText.Text = lockOn ? "↺  이 배치로 바꾸기" : "↺  이 배치로 맞추기";
        ApplyButton.ToolTip = lockOn
            ? "창들을 이 배치대로 옮기고, 창 고정도 이 배치로 바꿔요."
            : "창들을 이 배치대로 지금 바로 옮겨요. 꺼진 프로그램은 ('자동으로 켜기'를 켜 뒀다면) 켜 줘요.";

        // settings page
        TrayCheck.IsChecked = s.ShowTrayIcon;
        ButtonCheck.IsChecked = s.ShowLockButton;
        bool startupOn = s.StartupPresetId is { } sid && _host.Config.Presets.Any(p => p.Id == sid);
        StartupCheck.IsChecked = startupOn;
        StartupCheck.IsEnabled = _host.Config.Presets.Count > 0;
        StartupPickLabel.Visibility = StartupPickBox.Visibility = startupOn ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(() => MarkStartupChoice(s.StartupPresetId), System.Windows.Threading.DispatcherPriority.Loaded);
        ThemeSystem.IsChecked = s.Theme == Core.Model.ThemeMode.System;
        ThemeLight.IsChecked = s.Theme == Core.Model.ThemeMode.Light;
        ThemeDark.IsChecked = s.Theme == Core.Model.ThemeMode.Dark;
        AdminCheck.IsChecked = s.AlwaysAdmin;
        UpdateCheck.IsChecked = s.AutoUpdateCheck;
        if (string.IsNullOrEmpty(VersionText.Text)) VersionText.Text = $"현재 버전 {Updater.CurrentText}";
        AdminState.Text = _host.IsElevated ? "● 지금 관리자 권한으로 실행 중이에요." : "지금은 일반 권한으로 실행 중이에요.";
        if (_host.IsElevated) AdminBanner.Visibility = Visibility.Collapsed;
    }

    private void MarkStartupChoice(Guid? id)
    {
        foreach (var item in StartupPresetList.Items)
            if (StartupPresetList.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter cp &&
                VisualTreeHelper.GetChildrenCount(cp) > 0 && VisualTreeHelper.GetChild(cp, 0) is RadioButton rb)
                rb.IsChecked = rb.Tag is Guid g && g == id;
    }

    private void ShowSettings(bool on)
    {
        SettingsPage.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        LayoutPage.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        SettingsNav.IsChecked = on;
        SettingsNavText.Text = on ? "↩  돌아가기" : "⚙  설정";
        SettingsNav.ToolTip = on ? "설정을 닫고 보던 배치로 돌아가요." : "테마, 시작 설정 등을 바꿔요.";
        if (on)
        {
            if (Selected is { } before) _beforeSettings = before;
            _refreshing = true;
            PresetList.SelectedItem = null;
            _refreshing = false;
            RefreshToggles();
        }
    }

    /// <summary>From outside (tray menu): show the settings page; "돌아가기" then returns to the layout seen before.</summary>
    public void OpenSettings()
    {
        if (!SettingsShown) SettingsNav.IsChecked = true; // raises Checked → OnSettingsNav
    }

    private void OnSettingsNav(object sender, RoutedEventArgs e)
    {
        if (SettingsShown) return;
        _settingsJustOpened = true; // the same click also raises Click right after this
        // opened by keyboard / UI Automation there is no Click: don't let the flag eat the next real click
        Dispatcher.BeginInvoke(() => _settingsJustOpened = false, System.Windows.Threading.DispatcherPriority.Input);
        ShowSettings(true);
    }

    /// <summary>On the settings page the nav item reads "돌아가기": a second click returns to the layout seen before.</summary>
    private void OnSettingsNavClick(object sender, RoutedEventArgs e)
    {
        if (_settingsJustOpened) { _settingsJustOpened = false; return; }
        if (SettingsShown) GoBackFromSettings();
    }

    private void GoBackFromSettings()
    {
        var back = _beforeSettings is { } b && _host.Config.Presets.Contains(b) ? b : _host.CurrentPreset;
        ShowSettings(false);
        _refreshing = true;
        PresetList.SelectedItem = back;
        _refreshing = false;
        SelectEntry(null);
        RefreshPreview();
        RefreshToggles();
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        _host.Config.Settings.Theme = sender == ThemeLight ? Core.Model.ThemeMode.Light : sender == ThemeDark ? Core.Model.ThemeMode.Dark : Core.Model.ThemeMode.System;
        _host.SaveConfig();
        ThemeManager.Reapply();
    }

    private sealed record AppChip(Guid Id, string Name, Brush Brush);

    private void RefreshPreview()
    {
        var monitors = _host.Ws.GetMonitors();
        Preview.Monitors = monitors;
        var preset = Selected;
        bool empty = _host.Config.Presets.Count == 0;
        Guide.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        PreviewHint.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (empty && string.IsNullOrEmpty(StatusText.Text)) StatusText.Text = "환영해요! 가운데 안내를 따라 첫 배치를 만들어 보세요.";
        if (preset is null)
        {
            Preview.Rects = [];
            AppList.ItemsSource = null;
            AppsLabel.Visibility = Visibility.Collapsed;
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
        AppList.ItemsSource = preset.Windows
            .Select(w => new AppChip(w.Id, w.DisplayName, new SolidColorBrush(MonitorPreview.BrandColor(w.DisplayName) ?? MonitorPreview.ColorFor(w.Id))))
            .ToList();
        AppsLabel.Visibility = preset.Windows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_selectedEntry is { } id && preset.Windows.All(w => w.Id != id)) SelectEntry(null);
    }

    private void ShowReport(Preset p, ApplyReport report)
    {
        bool locked = _host.Lock.State == LockState.Active && _host.CurrentPreset?.Id == p.Id;
        var problems = report.Results
            .Where(r => r.Outcome != ApplyOutcome.Applied)
            .Select(r => $"{p.Windows.FirstOrDefault(w => w.Id == r.EntryId)?.DisplayName ?? "?"}: {Describe(r.Outcome, locked)}")
            .ToList();
        StatusText.Text = problems.Count == 0
            ? $"✔ '{p.Name}' 배치로 맞췄어요 ({report.Results.Count}개 창)."
            : $"'{p.Name}' 배치로 맞췄어요. 단,\n  · " + string.Join("\n  · ", problems);
        AdminBanner.Visibility = report.Results.Any(r => r.Outcome == ApplyOutcome.NeedsAdmin) && !_host.IsElevated
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Describe(ApplyOutcome o, bool locked) => o switch
    {
        ApplyOutcome.NotFound => "창이 안 보여서 건너뛰었어요 (프로그램이 켜져 있는지 확인하세요)",
        ApplyOutcome.MonitorMissing => "그 모니터가 연결돼 있지 않아 건너뛰었어요",
        ApplyOutcome.Failed => "옮길 수 없었어요",
        ApplyOutcome.NeedsAdmin => "관리자 권한으로 켜진 창이라 옮길 수 없었어요 (위의 '관리자 권한으로 다시 실행'을 누르세요)",
        ApplyOutcome.LaunchedWaiting => locked
            ? "프로그램을 켜는 중이에요. 창이 뜨면 창 고정이 알아서 제자리에 놓아요"
            : "프로그램을 켜는 중이에요. 창이 뜨면 [이 배치로 맞추기]를 한 번 더 눌러 주세요",
        ApplyOutcome.LaunchFailed => "프로그램을 켤 수 없었어요 (창 설정 → 고급 설정의 실행할 파일을 확인하세요)",
        _ => "완료",
    };

    // ---------- sidebar ----------

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        if (Selected is null) return;
        ShowSettings(false);
        SelectEntry(null);
        RefreshPreview();
        RefreshToggles();
    }

    /// <summary>A click on a layout always opens it, even if it is already the selected item.</summary>
    private void OnLayoutItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: Preset p }) return;
        if (SettingsShown || !ReferenceEquals(Selected, p))
        {
            ShowSettings(false);
            PresetList.SelectedItem = p;
            SelectEntry(null);
            RefreshPreview();
            RefreshToggles();
        }
    }

    private static Preset? MenuPreset(object sender) => (sender as FrameworkElement)?.DataContext as Preset;

    private void OnMenuApply(object sender, RoutedEventArgs e)
    {
        if (MenuPreset(sender) is { } p) _host.ApplyPreset(p.Id);
    }

    private void OnMenuRename(object sender, RoutedEventArgs e)
    {
        if (MenuPreset(sender) is { } p) Rename(p);
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) Rename(p);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) Delete(p);
    }

    private void Rename(Preset p)
    {
        var name = Dialogs.AppDialog.Prompt(this, "배치 이름 바꾸기", "새 이름을 입력하세요.", p.Name);
        if (name is null) return;
        p.Name = name;
        _host.SaveConfig();
        StatusText.Text = $"✔ 배치 이름을 '{name}'(으)로 바꿨어요.";
    }

    private void OnMenuDelete(object sender, RoutedEventArgs e)
    {
        if (MenuPreset(sender) is { } p) Delete(p);
    }

    private void Delete(Preset p)
    {
        if (!Dialogs.AppDialog.Confirm(this, $"'{p.Name}' 배치를 삭제할까요?",
                "창이나 프로그램은 그대로 있어요. 저장한 자리 정보만 지워져요.", ok: "삭제", danger: true)) return;
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

    // ---------- layout page ----------

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) _host.ApplyPreset(p.Id);
    }

    private void OnSaveCurrent(object sender, RoutedEventArgs e)
    {
        var preset = SaveDialog.Show(this, _host.Ws, NextLayoutName());
        if (preset is null) return;
        ShowSettings(false);
        _host.Config.Presets.Add(preset);
        _host.Config.Settings.CurrentPresetId = preset.Id;
        _selectCurrentOnRefresh = true;
        _host.SaveConfig();
        _host.RefreshLock(); // a running lock now guards the layout just made (windows are already in place)
        StatusText.Text = _host.Config.Settings.LockEnabled
            ? $"✔ '{preset.Name}' 배치를 만들었어요. 이제 이 배치를 고정하고 있어요."
            : $"✔ '{preset.Name}' 배치를 만들었어요! 이제 [🔒 창 고정]을 켜면 창이 이 자리에 고정돼요.";
    }

    /// <summary>"배치 1", "배치 2", … — the first number not already used.</summary>
    private string NextLayoutName()
    {
        var used = _host.Config.Presets.Select(p => p.Name).ToHashSet();
        int n = 1;
        while (used.Contains($"배치 {n}")) n++;
        return $"배치 {n}";
    }

    /// <summary>
    /// Click on a monitor: edit the layout shown. While the editor is open the lock is paused so windows can be moved;
    /// afterwards it re-locks to the saved (or unchanged, if cancelled) positions.
    /// </summary>
    private void EditLayout(string monitorPath)
    {
        if (Selected is not { } p) return;
        bool pausedHere = _host.Lock.State == LockState.Active; // any lock would fight the windows being moved
        if (pausedHere) _host.Lock.TogglePause();
        bool saved = SaveDialog.Edit(this, _host.Ws, p, monitorPath);
        if (saved) _host.SaveConfig();
        if (pausedHere && _host.Lock.State == LockState.Paused) _host.Lock.TogglePause();
        else if (saved) _host.RefreshLock();
        SelectEntry(null);
        RefreshPreview();
        StatusText.Text = saved
            ? $"✔ '{p.Name}' 배치를 고쳤어요" + (pausedHere ? ". 다시 고정했어요." : ".")
            : pausedHere ? "수정을 취소했어요. 창들을 원래 자리로 되돌리고 다시 고정했어요." : "수정을 취소했어요.";
    }

    // ---------- updates ----------

    private Core.Update.ReleaseInfo? _shownUpdate;

    private void ShowUpdate(Core.Update.ReleaseInfo r)
    {
        _shownUpdate = r;
        UpdateTitle.Text = $"새 버전 {r.Tag}이(가) 나왔어요 (지금 {Updater.CurrentText})";
        UpdateNowButton.Content = r.AssetUrl is null ? "다운로드 페이지 열기" : "업데이트";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private async void OnUpdateNow(object sender, RoutedEventArgs e)
    {
        if (_shownUpdate is not { } r) return;
        if (r.AssetUrl is null) { Updater.OpenPage(r.PageUrl); return; }
        UpdateNowButton.IsEnabled = UpdateSkipButton.IsEnabled = false;
        UpdateNowButton.Content = "내려받는 중…";
        StatusText.Text = $"{r.Tag}을(를) 내려받고 있어요. 끝나면 WindowFix가 잠깐 꺼졌다가 새 버전으로 다시 켜져요.";
        if (await _host.InstallUpdateAsync(r)) return;
        UpdateNowButton.IsEnabled = UpdateSkipButton.IsEnabled = true;
        UpdateNowButton.Content = "업데이트";
        StatusText.Text = "업데이트를 내려받지 못했어요. 인터넷 연결을 확인하거나, 다운로드 페이지에서 직접 받아 주세요.";
        Updater.OpenPage(r.PageUrl);
    }

    private void OnUpdateLater(object sender, RoutedEventArgs e) => UpdateBanner.Visibility = Visibility.Collapsed;

    private void OnUpdateSkip(object sender, RoutedEventArgs e)
    {
        if (_shownUpdate is { } r) _host.SkipUpdate(r);
        UpdateBanner.Visibility = Visibility.Collapsed;
        StatusText.Text = "이 버전은 건너뛸게요. 다음 새 버전이 나오면 다시 알려 드릴게요.";
    }

    private void OnUpdateCheckClick(object sender, RoutedEventArgs e)
    {
        _host.Config.Settings.AutoUpdateCheck = UpdateCheck.IsChecked == true;
        _host.SaveConfig();
    }

    private async void OnCheckUpdateNow(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        VersionText.Text = "확인하는 중…";
        var r = await _host.CheckForUpdateAsync(manual: true);
        CheckUpdateButton.IsEnabled = true;
        VersionText.Text = r is null
            ? $"현재 버전 {Updater.CurrentText} · 최신 버전이에요"
            : $"현재 버전 {Updater.CurrentText} · 새 버전 {r.Tag}이(가) 있어요";
    }

    private void OnRestartElevated(object sender, RoutedEventArgs e)
    {
        if (!_host.RestartElevated())
            StatusText.Text = "관리자 권한 확인을 취소했어요. 관리자 권한 창은 옮기지 못해요.";
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        bool on = LockCheck.IsChecked == true;
        if (on && Selected is { } p && _host.Config.Settings.CurrentPresetId != p.Id)
            _host.Config.Settings.CurrentPresetId = p.Id; // lock the layout the user is looking at
        _host.SetLockEnabled(on);
        StatusText.Text = on
            ? $"🔒 '{_host.CurrentPreset?.Name}' 배치를 고정했어요. 이 창을 닫아도 계속 지켜요. 컴퓨터를 다시 켜도 자동으로 켜져요."
            : "창 고정을 껐어요. 이제 창을 자유롭게 옮길 수 있어요.";
    }

    // ---------- settings page ----------

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
        var s = _host.Config.Settings;
        s.StartupPresetId = StartupCheck.IsChecked == true ? (s.CurrentPresetId ?? _host.Config.Presets.FirstOrDefault()?.Id) : null;
        _host.SaveConfig();
    }

    private void OnStartupPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: Guid id })
        {
            _host.Config.Settings.StartupPresetId = id;
            _host.SaveConfig();
        }
    }

    private void OnAdminClick(object sender, RoutedEventArgs e)
    {
        bool on = AdminCheck.IsChecked == true;
        _host.Config.Settings.AlwaysAdmin = on;
        _host.SaveConfig();
        if (on && !_host.IsElevated)
        {
            if (!_host.RestartElevated())
            {
                _host.Config.Settings.AlwaysAdmin = false; // UAC declined: keep things as they were
                _host.SaveConfig();
                StatusText.Text = "관리자 권한 확인을 취소해서 옵션을 껐어요.";
            }
            return;
        }
        StatusText.Text = on
            ? "✔ 이제 항상 관리자 권한으로 실행돼요. 컴퓨터를 켤 때도 확인 창 없이 관리자 권한으로 켜져요."
            : "다음 실행부터 일반 권한으로 켜져요.";
    }

    // ---------- window settings panel ----------

    private void OnAppChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) SelectEntry(id);
    }

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

    private WindowEntry? SelectedEntry => _selectedEntry is { } id ? Selected?.Windows.FirstOrDefault(w => w.Id == id) : null;

    private void OnEditClose(object sender, RoutedEventArgs e) => SelectEntry(null);

    private void OnPickTitle(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry is not { } entry) return;
        var live = _host.Ws.GetWindows(includeHidden: false)
            .Where(w => WindowMatcher.IsCandidate(w) && string.Equals(w.ExeName, entry.Match.ExeName, StringComparison.OrdinalIgnoreCase))
            .Select(w => w.Title).Distinct().ToList();
        if (live.Count == 0)
        {
            StatusText.Text = $"지금 떠 있는 {entry.DisplayName} 창이 없어요. 창을 띄운 뒤 다시 눌러 주세요.";
            return;
        }
        TitleBox.Text = live[0];
        StatusText.Text = live.Count > 1
            ? $"창이 {live.Count}개라 첫 번째 제목을 넣었어요. 구분되는 단어만 남기고 지우세요."
            : "제목을 넣었어요. 매번 바뀌는 부분은 지우고 구분되는 단어만 남기세요.";
    }

    private void OnBrowseExe(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "자동으로 켤 프로그램 고르기",
            Filter = "프로그램 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
        };
        if (!string.IsNullOrWhiteSpace(PathBox.Text) && System.IO.File.Exists(PathBox.Text))
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(PathBox.Text);
        if (dlg.ShowDialog(this) == true) PathBox.Text = dlg.FileName;
    }

    private void OnEntrySave(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry is not { } entry) return;
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
        StatusText.Text = _host.Lock.State == LockState.Active && _host.CurrentPreset?.Id == Selected?.Id
            ? $"✔ '{entry.DisplayName}' 설정을 저장하고 바로 반영했어요."
            : $"✔ '{entry.DisplayName}' 설정을 저장했어요. [이 배치로 맞추기]를 누르면 바로 반영돼요.";
    }

    private void OnEntryRemove(object sender, RoutedEventArgs e)
    {
        var preset = Selected;
        if (preset is null || SelectedEntry is not { } entry) return;
        preset.Windows.Remove(entry);
        SelectEntry(null);
        _host.SaveConfig();
        _host.RefreshLock();
    }
}
