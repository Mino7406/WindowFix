namespace WindowFix.Core.Startup;

/// <summary>When the process may end. Spec: background only while the lock is on; closing the window with the lock off exits.</summary>
public static class AppLifetime
{
    public static bool ExitOnMainWindowClose(bool lockEnabled, bool startupRunning) => !lockEnabled && !startupRunning;

    /// <summary>For moments when nothing is left to do (lock switched off from the tray, boot restore finished).</summary>
    public static bool ExitWhenIdle(bool lockEnabled, bool startupRunning, bool mainVisible) =>
        !lockEnabled && !startupRunning && !mainVisible;
}
