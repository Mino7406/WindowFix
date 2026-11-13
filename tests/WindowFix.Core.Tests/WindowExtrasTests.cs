using WindowFix.Core.Apply;
using WindowFix.Core.Lock;
using WindowFix.Core.Model;
using WindowFix.Core.Tests.Fakes;
using static WindowFix.Core.Tests.Fakes.FakeWindowSystem;

namespace WindowFix.Core.Tests;

public sealed class WindowExtrasTests
{
    private readonly FakeClock _clock = new();

    private static WindowEntry Entry(bool noMin = false, bool top = false) => new()
    {
        Match = new WindowMatch { ExeName = "KakaoTalk.exe" },
        Monitor = new MonitorRef { DevicePath = "dev-secondary", Width = 2560, Height = 1440 },
        Rect = new RelRect(1588, 0, 460, 951),
        NoMinimize = noMin,
        AlwaysOnTop = top,
    };

    private LockTracker Tracker(WindowEntry e)
    {
        var t = new LockTracker(new Preset { Windows = [e] }, () => [Primary, Secondary], new SnapBackPolicy(_clock));
        t.BindInitial([new EntryResult(e.Id, ApplyOutcome.Applied, 9)]);
        return t;
    }

    [Fact]
    public void NewWindowOptions_DefaultOff()
    {
        var e = new WindowEntry();
        Assert.False(e.NoMinimize);
        Assert.False(e.AlwaysOnTop);
        Assert.False(e.RelaunchIfClosed);
        Assert.True(new AppSettings().BootNotify);
    }

    [Fact]
    public void Minimized_WithNoMinimize_IsRestored()
    {
        var w = Win(9, "KakaoTalk.exe", new PixelRect(-32000, -32000, 160, 28)) with { IsMinimized = true };

        Assert.Equal(LockAction.Revert, Tracker(Entry(noMin: true)).OnLocationChanged(w).Action);
        Assert.Equal(LockAction.None, Tracker(Entry()).OnLocationChanged(w).Action);
    }

    [Fact]
    public void Decision_CarriesAlwaysOnTop()
    {
        var w = Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 10, 10));

        Assert.True(Tracker(Entry(top: true)).OnLocationChanged(w).TopMost);
        Assert.False(Tracker(Entry()).OnLocationChanged(w).TopMost);
    }

    [Fact]
    public void Apply_AlwaysOnTopEntry_SetsTopMost()
    {
        var ws = TwoMonitors();
        ws.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 10, 10)));

        new PresetApplier(ws).Apply(new Preset { Windows = [Entry(top: true)] }, ApplyOptions.Full);

        Assert.Equal([9], ws.TopMost);
    }

    [Fact]
    public void RelaunchPolicy_LimitsTo3In10Minutes()
    {
        var p = new RelaunchPolicy(_clock);
        var id = Guid.NewGuid();

        for (int i = 0; i < 3; i++) { Assert.True(p.MayRelaunch(id)); _clock.Advance(TimeSpan.FromMinutes(1)); }
        Assert.False(p.MayRelaunch(id));

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.True(p.MayRelaunch(id));
    }
}
