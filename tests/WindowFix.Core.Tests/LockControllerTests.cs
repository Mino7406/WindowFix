using WindowFix.Core.Lock;

namespace WindowFix.Core.Tests;

public sealed class LockControllerTests
{
    [Fact]
    public void Controller_StartsOff()
    {
        Assert.Equal(LockState.Off, new LockController().State);
    }

    [Fact]
    public void Controller_TogglePause_ActiveToPausedAndBack_RequestsReapply()
    {
        var c = new LockController();
        var states = new List<LockState>();
        int reapplies = 0;
        c.StateChanged += states.Add;
        c.RequestReapply += () => reapplies++;

        c.SetEnabled(true);
        c.TogglePause();
        Assert.Equal(LockState.Paused, c.State);
        Assert.Equal(0, reapplies);

        c.TogglePause();
        Assert.Equal(LockState.Active, c.State);
        Assert.Equal(1, reapplies);
        Assert.Equal([LockState.Active, LockState.Paused, LockState.Active], states);
    }

    [Fact]
    public void Controller_TogglePause_WhenOff_NoChange()
    {
        var c = new LockController();
        int events = 0;
        c.StateChanged += _ => events++;

        c.TogglePause();

        Assert.Equal(LockState.Off, c.State);
        Assert.Equal(0, events);
    }

    [Fact]
    public void Controller_Disable_FromPaused_GoesOff()
    {
        var c = new LockController();
        c.SetEnabled(true);
        c.TogglePause();

        c.SetEnabled(false);

        Assert.Equal(LockState.Off, c.State);
    }

    [Fact]
    public void Controller_EnableWhenAlreadyActive_NoEvent()
    {
        var c = new LockController();
        c.SetEnabled(true);
        int events = 0;
        c.StateChanged += _ => events++;

        c.SetEnabled(true);

        Assert.Equal(0, events);
    }
}
