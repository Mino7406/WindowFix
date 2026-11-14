using System.Windows;
using System.Windows.Threading;

namespace WindowFix;

public partial class App : Application
{
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length >= 3 && e.Args[0] == Core.Update.UpdateSwap.ApplyArg)
        {
            Updater.RunSwap(e.Args[1], e.Args[2]);
            Shutdown();
            return;
        }
        DispatcherUnhandledException += OnUnhandled;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };
        _host = new AppHost(this);
        _host.Run(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Keep running (the lock may be guarding windows) and tell the user in plain words.</summary>
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        // A broken animation or render handler fails on every frame: log and show it once, never a flood of dialogs.
        if (_showingError || DateTime.UtcNow - _lastErrorShown < TimeSpan.FromSeconds(30)) return;
        LogError(e.Exception);
        _showingError = true;
        try
        {
            Dialogs.AppDialog.Info(null, "예상하지 못한 문제가 생겼어요",
                @"WindowFix는 계속 동작해요. 같은 문제가 반복되면 %APPDATA%\WindowFix\error.log 파일을 보내 주세요.");
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
        finally
        {
            _showingError = false;
            _lastErrorShown = DateTime.UtcNow;
        }
    }

    private static bool _showingError;
    private static DateTime _lastErrorShown = DateTime.MinValue;

    /// <summary>✕ inside a "Clearable" TextBox: empty it and keep typing there.</summary>
    private void OnClearTextBox(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.TemplatedParent is System.Windows.Controls.TextBox box)
        {
            box.Clear();
            box.Focus();
        }
    }

    /// <summary>Startup milestones with timestamps (%APPDATA%\WindowFix\startup.log), so a stall shows where it stopped.</summary>
    public static void Trace(string message)
    {
        try
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowFix");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "startup.log");
            if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 256 * 1024) System.IO.File.Delete(path);
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] pid {Environment.ProcessId} {message}\n");
        }
        catch (Exception)
        {
            // never let diagnostics break startup
        }
    }

    public static void LogError(Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowFix");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch (Exception)
        {
            // Logging must never take the app down.
        }
    }
}
