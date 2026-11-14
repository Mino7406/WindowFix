using System.ComponentModel;
using System.Windows;
using WindowFix.Core.Apply;
using WindowFix.Core.Lock;
using WindowFix.Core.Model;
using WindowFix.Core.Platform;
using WindowFix.Core.Startup;
using WindowFix.Core.Storage;

namespace WindowFix;

/// <summary>
/// App lifetime: single instance, --startup mode, config, lock on/off/pause, and when the process may exit.
/// Rule from the spec: when the lock is off, closing the main window ends the process.
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly App _app;
    private readonly ConfigStore _store = new(ConfigStore.DefaultPath);
    private readonly WinEventLockService _lockService;
    private SingleInstance? _instance;
    private MainWindow? _main;
    private FloatingLockButton? _button;
    private bool _startupRunning;
    private Task _syncChain = Task.CompletedTask;
    private Tray.TrayIcon? _tray;

    public AppHost(App app)
    {
        _app = app;
        Ws = new Win32WindowSystem();
        Applier = new PresetApplier(Ws);
        _lockService = new WinEventLockService(Ws);
        Lock.StateChanged += OnLockStateChanged;
    }

    public IWindowSystem Ws { get; }
    public PresetApplier Applier { get; }
    public LockController Lock { get; } = new();
    public AppConfig Config { get; private set; } = new();

    /// <summary>Raised after the config was saved.</summary>
    public event Action? ConfigChanged;

    /// <summary>Raised after a preset was applied, for status display.</summary>
    public event Action<Preset, ApplyReport>? Applied;

    public Preset? CurrentPreset =>
        Config.Presets.FirstOrDefault(p => p.Id == Config.Settings.CurrentPresetId) ?? Config.Presets.FirstOrDefault();

    public void Run(string[] args)
    {
        // An elevated restart may start while the previous instance is still shutting down: give it a moment.
        bool restarted = args.Contains(RestartedArg);
        _instance = SingleInstance.TryAcquire(() => _app.Dispatcher.BeginInvoke(ShowMainWindow),
            restarted ? TimeSpan.FromSeconds(8) : TimeSpan.Zero);
        if (_instance is null)
        {
            _app.Shutdown();
            return;
        }

        try
        {
            Config = _store.Load();
        }
        catch (ConfigUnavailableException)
        {
            Dialogs.AppDialog.Info(null, "설정 파일을 읽을 수 없어요",
                "저장해 둔 배치를 지키기 위해 WindowFix를 닫을게요. 잠시 뒤 다시 실행해 주세요.");
            Shutdown();
            return;
        }
        if (Config.Settings.AlwaysAdmin && !Ws.SelfElevated && !args.Contains(RestartedArg) && TryElevate(args))
            return;
        bool boot = args.Contains(StartupRegistration.StartupArg, StringComparer.OrdinalIgnoreCase);
        App.Trace($"start boot={boot} elevated={Ws.SelfElevated}");
        if (boot)
        {
            _ = BootAsync();
            return;
        }

        FinishStartup(boot: false);
        ShowMainWindow();
        if (Config.Settings.LockEnabled) Lock.SetEnabled(true);
    }

    /// <summary>Started by Windows at logon: wait for the taskbar first (see <see cref="ShellReady"/>), then go.</summary>
    private async Task BootAsync()
    {
        try
        {
            bool ready = await ShellReady.WaitAsync(TimeSpan.FromSeconds(90));
            App.Trace($"shell ready={ready}");
            FinishStartup(boot: true);
            App.Trace("ui ready");
            await RunStartupAsync();
            App.Trace("boot restore done");
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            App.Trace("boot failed: " + ex.GetType().Name);
        }
    }

    /// <summary>Everything that touches the shell (tray icon, edge widget, theme hooks) — after the taskbar is ready at boot.</summary>
    private void FinishStartup(bool boot)
    {
        SyncRunKey(); // the exe may have been moved since the last save
        Updater.CleanupOld();
        ThemeManager.Init(() => Config.Settings.Theme);
        _tray = new Tray.TrayIcon(this);
        Lock.StateChanged += _ => UpdateOverlays();
        ConfigChanged += UpdateOverlays;
        UpdateOverlays();
        StartUpdateChecks(boot);
    }

    private async Task RunStartupAsync()
    {
        var s = Config.Settings;
        var preset = Config.Presets.FirstOrDefault(p => p.Id == (s.StartupPresetId ?? s.CurrentPresetId));
        // The startup layout becomes the current one *before* the lock is armed, so the lock and the restore
        // agree on where windows go (otherwise they fight until the restore ends).
        if (preset is not null && s.CurrentPresetId != preset.Id)
        {
            s.CurrentPresetId = preset.Id;
            SaveConfig();
        }
        // Arm the lock first: windows that show up while the boot restore is still waiting are placed the moment
        // they appear (event-driven), instead of only after every app in the layout has opened.
        try
        {
            if (s.LockEnabled && Lock.State == LockState.Off) Lock.SetEnabled(true);
        }
        catch (Exception ex)
        {
            App.LogError(ex); // never let a failure here skip the restore itself
        }
        if (preset is not null)
        {
            _startupRunning = true;
            try
            {
                var report = await new StartupSequence(Applier, SystemClock.Instance, d => Task.Delay(d)).RunAsync(preset, CancellationToken.None);
                if (s.CurrentPresetId == preset.Id) // the user may have applied another layout meanwhile: keep theirs
                {
                    Applied?.Invoke(preset, report);
                    if (s.BootNotify) NotifyBootResult(preset, report);
                    RefreshLock(); // re-bind with the final positions (apps often resize themselves while loading)
                }
            }
            catch (Exception ex)
            {
                App.LogError(ex); // a failed restore must not stop the lock from arming
            }
            finally
            {
                _startupRunning = false;
            }
        }

        if (s.LockEnabled && Lock.State == LockState.Off) Lock.SetEnabled(true);
        ExitIfIdle();
    }

    private bool MainVisible => _main?.IsVisible == true;

    /// <summary>Ends the process when there is nothing left to guard and the user isn't looking at the window.</summary>
    private void ExitIfIdle()
    {
        if (AppLifetime.ExitWhenIdle(Config.Settings.LockEnabled, _startupRunning, MainVisible)) Shutdown();
    }

    /// <summary>Apply with launch/unhide (the [적용] button), make it the current preset, re-arm the lock.</summary>
    public ApplyReport ApplyPreset(Guid id)
    {
        var preset = Config.Presets.First(p => p.Id == id);
        var report = Applier.Apply(preset, ApplyOptions.Full);
        Config.Settings.CurrentPresetId = id;
        SaveConfig();
        if (Lock.State == LockState.Active) StartLock(preset, report);
        Applied?.Invoke(preset, report);
        return report;
    }

    public void SetLockEnabled(bool on)
    {
        Config.Settings.LockEnabled = on;
        SaveConfig();
        Lock.SetEnabled(on);
        if (!on) ExitIfIdle(); // e.g. turned off from the tray while the window is hidden
    }

    /// <summary>Call after editing the current preset so a running lock uses the new targets.</summary>
    public void RefreshLock()
    {
        if (Lock.State == LockState.Active) OnLockStateChanged(LockState.Active);
    }

    public void SaveConfig()
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                _store.Save(Config);
                break;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                if (attempt < 3) { Thread.Sleep(150); continue; } // antivirus / OneDrive briefly holding the file
                App.LogError(ex);
                break; // keep running with the settings in memory; the next save tries again
            }
        }
        SyncRunKey();
        ConfigChanged?.Invoke();
    }

    private (bool Admin, bool Boot)? _syncedStartup;
    private readonly object _syncLock = new();

    /// <summary>
    /// Keeps "start with Windows" registered. Only when what it depends on changed (always-admin, boot needed) — every
    /// config save (a tab drag, a layout click) used to launch schtasks.exe twice and freeze the UI while it ran — and
    /// off the UI thread, since registry and schtasks calls can take a moment.
    /// </summary>
    private void SyncRunKey()
    {
        var s = Config.Settings;
        var state = (s.AlwaysAdmin, s.LockEnabled || s.StartupPresetId is not null);
        if (_syncedStartup == state) return;
        var snapshot = new AppSettings
        {
            AlwaysAdmin = s.AlwaysAdmin,
            LockEnabled = s.LockEnabled,
            StartupPresetId = s.StartupPresetId,
        };
        bool elevated = Ws.SelfElevated;
        var exe = Environment.ProcessPath!;
        _syncedStartup = state; // optimistic; reset below if the sync fails so the next save retries
        lock (_syncLock)
        {
            // chained, so syncs run strictly in order and the last one always wins
            _syncChain = _syncChain.ContinueWith(_ =>
            {
                try
                {
                    StartupRegistration.Sync(snapshot, exe, new RegistryRunKey(), new SchtasksElevatedTasks(), elevated);
                }
                catch (Exception ex)
                {
                    App.LogError(ex); // the app still works; it just may not start with Windows
                    _app.Dispatcher.BeginInvoke(() => _syncedStartup = null);
                }
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Floating lock button: only while the lock is on and the setting allows it. Tray icon: per setting.</summary>
    private void UpdateOverlays()
    {
        var s = Config.Settings;
        if (_tray is not null)
        {
            _tray.Visible = s.ShowTrayIcon;
            _tray.Update(Lock.State);
        }

        if (s.ShowLockButton && Lock.State != LockState.Off)
        {
            _button ??= new FloatingLockButton(this);
            _button.Update(Lock.State);
            _button.ShowAtSavedSpot();
        }
        else
        {
            _button?.Hide();
        }
    }

    public void ShowMainWindow()
    {
        if (_main is null)
        {
            _main = new MainWindow(this);
            _main.Closing += OnMainWindowClosing;
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    /// <summary>Open the main window straight on the settings page (tray menu).</summary>
    public void ShowSettings()
    {
        ShowMainWindow();
        _main!.OpenSettings();
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!AppLifetime.ExitOnMainWindowClose(Config.Settings.LockEnabled, _startupRunning))
        {
            e.Cancel = true;
            _main!.Hide();
            return;
        }
        Shutdown();
    }

    /// <summary>Entering Active (lock turned on, or a pause ended) puts windows back and binds them; leaving it stops the hooks.</summary>
    private void OnLockStateChanged(LockState state)
    {
        if (state != LockState.Active)
        {
            _lockService.Stop();
            return;
        }
        var preset = CurrentPreset;
        if (preset is null)
        {
            _lockService.Stop();
            return;
        }
        StartLock(preset, Applier.Apply(preset, ApplyOptions.PlaceOnly));
    }

    private void StartLock(Preset preset, ApplyReport report)
    {
        var tracker = new LockTracker(preset, Ws.GetMonitors, new SnapBackPolicy(SystemClock.Instance),
            pid => Ws.GetWindows(includeHidden: true).Where(w => w.ProcessId == pid).ToList());
        tracker.BindInitial(report.Results);
        _lockService.Start(tracker);
        _tracker = tracker;
        EnsureRelaunchWatch();
    }

    // ---------- "고정 중에 꺼지면 다시 켜기" ----------

    private LockTracker? _tracker;
    private System.Windows.Threading.DispatcherTimer? _relaunchTimer;
    private readonly RelaunchPolicy _relaunch = new(SystemClock.Instance);

    /// <summary>Every 5s while locked: an entry marked "relaunch" whose app is gone gets started again (rate-limited).</summary>
    private void EnsureRelaunchWatch()
    {
        if (_relaunchTimer is not null) return;
        _relaunchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _relaunchTimer.Tick += (_, _) =>
        {
            if (Lock.State != LockState.Active || _tracker is not { } t || IsEditing) return;
            foreach (var e in t.Preset.Windows)
            {
                if (!e.RelaunchIfClosed || string.IsNullOrEmpty(e.LaunchPath) || t.IsBound(e.Id)) continue;
                if (Ws.IsRunning(e.Match.ExeName) || !_relaunch.MayRelaunch(e.Id)) continue;
                try { Ws.Launch(e.LaunchPath, e.LaunchArgs); } // the lock places the new window when it shows
                catch (Exception ex) { App.LogError(ex); }
            }
        };
        _relaunchTimer.Start();
    }

    public const string RestartedArg = "--restarted";

    /// <summary>A layout editor is open (lock paused on purpose): the tray must not re-arm or apply meanwhile.</summary>
    public bool IsEditing { get; set; }

    // ---------- updates (GitHub releases) ----------

    public event Action<Core.Update.ReleaseInfo>? UpdateAvailable;
    public Core.Update.ReleaseInfo? PendingUpdate { get; private set; }
    private System.Windows.Threading.DispatcherTimer? _updateTimer;

    /// <summary>First check shortly after start (a minute later at boot, to stay out of the way), then every 6h; the 24h rule gates real requests.</summary>
    private void StartUpdateChecks(bool boot)
    {
        var first = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(boot ? 60 : 3) };
        first.Tick += async (_, _) => { first.Stop(); await CheckForUpdateAsync(manual: false); };
        first.Start();
        _updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdateAsync(manual: false);
        _updateTimer.Start();
    }

    /// <summary>Manual checks ignore the daily limit and a skipped version.</summary>
    public async Task<Core.Update.ReleaseInfo?> CheckForUpdateAsync(bool manual)
    {
        var s = Config.Settings;
        if (!manual && (!s.AutoUpdateCheck || !Core.Update.UpdateChecker.ShouldCheck(s.LastUpdateCheckUtc, DateTime.UtcNow)))
            return PendingUpdate;
        var r = await Updater.CheckAsync(manual ? null : s.SkippedUpdateTag);
        s.LastUpdateCheckUtc = DateTime.UtcNow;
        SaveConfig();
        if (r is not null)
        {
            PendingUpdate = r;
            UpdateAvailable?.Invoke(r);
            _tray?.ShowBalloon($"WindowFix {r.Tag}이(가) 나왔어요", "눌러서 업데이트할 수 있어요.");
        }
        return r;
    }

    /// <summary>Download and hand over to the swap step; the app exits and comes back as the new version.</summary>
    public async Task<bool> InstallUpdateAsync(Core.Update.ReleaseInfo r)
    {
        if (!await Updater.InstallAsync(r)) return false;
        Shutdown();
        return true;
    }

    private void NotifyBootResult(Preset preset, ApplyReport report)
    {
        var missing = report.Results.Where(r => r.Outcome != ApplyOutcome.Applied)
            .Select(r => preset.Windows.FirstOrDefault(w => w.Id == r.EntryId)?.DisplayName).Where(n => n is not null).ToList();
        _tray?.ShowBalloon($"'{preset.Name}' 배치로 맞췄어요",
            missing.Count == 0 ? $"{report.Results.Count}개 창을 제자리에 놓았어요." : $"아직 못 찾은 창: {string.Join(", ", missing)} (뜨면 바로 놓아 드려요)");
    }

    public void SkipUpdate(Core.Update.ReleaseInfo r)
    {
        Config.Settings.SkippedUpdateTag = r.Tag;
        PendingUpdate = null;
        SaveConfig();
    }

    public bool IsElevated => Ws.SelfElevated;

    /// <summary>
    /// "Always admin" on but we started as a normal app: hand over to the elevated scheduled task (no UAC), or fall back
    /// to a UAC prompt. Returns true when an elevated instance was started and this one is exiting.
    /// </summary>
    private bool TryElevate(string[] args)
    {
        bool startup = args.Contains(StartupRegistration.StartupArg, StringComparer.OrdinalIgnoreCase);
        _instance?.Dispose(); // let the elevated instance take over the single-instance lock
        _instance = null;
        var tasks = new SchtasksElevatedTasks();
        if (tasks.Run(startup ? StartupRegistration.StartupTask : StartupRegistration.OpenTask) || StartElevated(startup))
        {
            _app.Shutdown();
            return true;
        }
        _instance = SingleInstance.TryAcquire(() => _app.Dispatcher.BeginInvoke(ShowMainWindow), TimeSpan.FromSeconds(2));
        return false; // UAC declined: keep running as a normal app
    }

    private static bool StartElevated(bool startup)
    {
        try
        {
            using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!,
                startup ? $"{RestartedArg} {StartupRegistration.StartupArg}" : RestartedArg)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // the user said no to UAC
        }
    }

    /// <summary>Restart this app elevated (UAC prompt). False when the user declined.</summary>
    public bool RestartElevated()
    {
        _lockService.Stop();
        _instance?.Dispose();
        _instance = null;
        if (StartElevated(startup: false))
        {
            _app.Shutdown();
            return true;
        }
        _instance = SingleInstance.TryAcquire(() => _app.Dispatcher.BeginInvoke(ShowMainWindow), TimeSpan.FromSeconds(2));
        if (Lock.State == LockState.Active) OnLockStateChanged(LockState.Active);
        return false;
    }

    public void Shutdown()
    {
        // let a pending startup-registration change finish, or the app could silently stop (or keep) starting with Windows
        try { _syncChain.Wait(TimeSpan.FromSeconds(15)); } catch (AggregateException) { }
        _lockService.Stop();
        _app.Shutdown();
    }

    public void Dispose()
    {
        _tray?.Dispose();
        _tray = null;
        _lockService.Dispose();
        _instance?.Dispose();
        _instance = null;
    }
}
