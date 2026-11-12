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
        _instance = SingleInstance.TryAcquire(() => _app.Dispatcher.BeginInvoke(ShowMainWindow));
        if (_instance is null)
        {
            _app.Shutdown();
            return;
        }

        Config = _store.Load();
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
        if (preset is null)
        {
            if (!s.LockEnabled) Shutdown();
            else Lock.SetEnabled(true);
            return;
        }

        var report = await new StartupSequence(Applier, SystemClock.Instance, d => Task.Delay(d)).RunAsync(preset, CancellationToken.None);
        if (s.CurrentPresetId != preset.Id)
        {
            s.CurrentPresetId = preset.Id;
            SaveConfig();
        }
        Applied?.Invoke(preset, report);

        if (!s.LockEnabled)
        {
            Shutdown();
            return;
        }
        Lock.SetEnabled(true);
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
    }

    /// <summary>Call after editing the current preset so a running lock uses the new targets.</summary>
    public void RefreshLock()
    {
        if (Lock.State == LockState.Active) OnLockStateChanged(LockState.Active);
    }

    public void SaveConfig()
    {
        _store.Save(Config);
        try
        {
            StartupRegistration.Sync(Config.Settings, Environment.ProcessPath!, new RegistryRunKey());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            // The app still works; it just won't start with Windows.
        }
        ConfigChanged?.Invoke();
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

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (Lock.State != LockState.Off)
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
        var tracker = new LockTracker(preset, Ws.GetMonitors, new SnapBackPolicy(SystemClock.Instance));
        tracker.BindInitial(report.Results);
        _lockService.Start(tracker);
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
