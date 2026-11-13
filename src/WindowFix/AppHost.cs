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
        SyncRunKey(); // the exe may have been moved since the last save
        ThemeManager.Init(() => Config.Settings.Theme);
        _tray = new Tray.TrayIcon(this);
        Lock.StateChanged += _ => UpdateOverlays();
        ConfigChanged += UpdateOverlays;
        UpdateOverlays();

        if (args.Contains(StartupRegistration.StartupArg, StringComparer.OrdinalIgnoreCase))
        {
            _ = RunStartupAsync();
            return;
        }

        ShowMainWindow();
        if (Config.Settings.LockEnabled) Lock.SetEnabled(true);
    }

    private async Task RunStartupAsync()
    {
        var s = Config.Settings;
        var preset = Config.Presets.FirstOrDefault(p => p.Id == (s.StartupPresetId ?? s.CurrentPresetId));
        if (preset is not null)
        {
            _startupRunning = true;
            try
            {
                var report = await new StartupSequence(Applier, SystemClock.Instance, d => Task.Delay(d)).RunAsync(preset, CancellationToken.None);
                if (s.CurrentPresetId != preset.Id)
                {
                    s.CurrentPresetId = preset.Id;
                    SaveConfig();
                }
                Applied?.Invoke(preset, report);
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
        _store.Save(Config);
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
        _syncedStartup = state;
        var snapshot = new AppSettings
        {
            AlwaysAdmin = s.AlwaysAdmin,
            LockEnabled = s.LockEnabled,
            StartupPresetId = s.StartupPresetId,
        };
        bool elevated = Ws.SelfElevated;
        var exe = Environment.ProcessPath!;
        Task.Run(() =>
        {
            lock (_syncLock) // one at a time, in order
            {
                try
                {
                    StartupRegistration.Sync(snapshot, exe, new RegistryRunKey(), new SchtasksElevatedTasks(), elevated);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
                {
                    // The app still works; it just won't start with Windows.
                }
            }
        });
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
    }

    public const string RestartedArg = "--restarted";

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
