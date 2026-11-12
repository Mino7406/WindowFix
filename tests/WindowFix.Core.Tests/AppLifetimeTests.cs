using WindowFix.Core.Startup;

namespace WindowFix.Core.Tests;

public sealed class AppLifetimeTests
{
    [Theory]
    [InlineData(false, false, true)]  // lock off, nothing running → closing the window ends the app (spec)
    [InlineData(true, false, false)]  // lock on → keep guarding in the background
    [InlineData(false, true, false)]  // boot restore still running → finish it first
    [InlineData(true, true, false)]
    public void ExitOnMainWindowClose(bool lockEnabled, bool startupRunning, bool expected)
    {
        Assert.Equal(expected, AppLifetime.ExitOnMainWindowClose(lockEnabled, startupRunning));
    }

    [Theory]
    [InlineData(false, false, false, true)]  // lock turned off from the tray while the window is hidden → exit
    [InlineData(false, false, true, false)]  // user has the window open → never pull it away
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void ExitWhenIdle(bool lockEnabled, bool startupRunning, bool mainVisible, bool expected)
    {
        Assert.Equal(expected, AppLifetime.ExitWhenIdle(lockEnabled, startupRunning, mainVisible));
    }
}
