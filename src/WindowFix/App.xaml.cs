using System.Windows;
using System.Windows.Threading;

namespace WindowFix;

public partial class App : Application
{
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        LogError(e.Exception);
        e.Handled = true;
        Dialogs.AppDialog.Info(null, "예상하지 못한 문제가 생겼어요",
            @"WindowFix는 계속 동작해요. 같은 문제가 반복되면 %APPDATA%\WindowFix\error.log 파일을 보내 주세요.");
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
