using WindowFix.Core.Apply;
using WindowFix.Core.Lock;
using WindowFix.Core.Model;
using WindowFix.Core.Tests.Fakes;
using static WindowFix.Core.Tests.Fakes.FakeWindowSystem;

namespace WindowFix.Core.Tests;

public sealed class LockTrackerTests
{
    private static readonly PixelRect DiscordTarget = new(1914, 0, 1601, 958);
    private readonly FakeClock _clock = new();
    private readonly WindowEntry _discord;
    private readonly WindowEntry _chromeMax;
    private readonly LockTracker _t;

    public LockTrackerTests()
    {
        _discord = new WindowEntry
        {
            Match = new WindowMatch { ExeName = "Discord.exe" },
            Monitor = new MonitorRef { DevicePath = "dev-secondary", Width = 2560, Height = 1440 },
            Rect = new RelRect(-6, 0, 1601, 958),
        };
        _chromeMax = new WindowEntry
        {
            Match = new WindowMatch { ExeName = "chrome.exe" },
            Monitor = new MonitorRef { DevicePath = "dev-secondary", Width = 2560, Height = 1440 },
            Rect = new RelRect(0, 0, 2560, 1400),
            Maximized = true,
        };
        var preset = new Preset { Name = "p", Windows = [_discord, _chromeMax] };
        _t = new LockTracker(preset, () => [Primary, Secondary], new SnapBackPolicy(_clock));
        _t.BindInitial([new EntryResult(_discord.Id, ApplyOutcome.Applied, 5), new EntryResult(_chromeMax.Id, ApplyOutcome.Applied, 7)]);
    }

    private static WindowInfo Discord(PixelRect r, bool min = false, bool max = false) =>
        Win(5, "Discord.exe", r) with { IsMinimized = min, IsMaximized = max };

    [Fact]
    public void Tracker_MovedBoundWindow_Reverts()
    {
        var d = _t.OnLocationChanged(Discord(new PixelRect(2000, 50, 1200, 700)));

        Assert.Equal(LockAction.Revert, d.Action);
        Assert.Equal(DiscordTarget, d.Target);
        Assert.False(d.Maximize);
    }

    [Fact]
    public void Tracker_Minimized_None()
    {
        Assert.Equal(LockAction.None, _t.OnLocationChanged(Discord(new PixelRect(-32000, -32000, 160, 28), min: true)).Action);
    }

    [Fact]
    public void Tracker_MaximizedButSavedNormal_Reverts()
    {
        var d = _t.OnLocationChanged(Discord(new PixelRect(1912, -8, 2576, 1416), max: true));

        Assert.Equal(LockAction.Revert, d.Action);
        Assert.Equal(DiscordTarget, d.Target);
    }

    [Fact]
    public void Tracker_SavedMaximized_StaysMaximized_None()
    {
        var w = Win(7, "chrome.exe", new PixelRect(1912, -8, 2576, 1416), maximized: true);

        Assert.Equal(LockAction.None, _t.OnLocationChanged(w).Action);
    }

    [Fact]
    public void Tracker_SavedMaximized_Unmaximized_RevertsToMaximized()
    {
        var d = _t.OnLocationChanged(Win(7, "chrome.exe", new PixelRect(2000, 100, 800, 600)));

        Assert.Equal(LockAction.Revert, d.Action);
        Assert.True(d.Maximize);
    }

    [Fact]
    public void Tracker_UnboundWindow_None()
    {
        Assert.Equal(LockAction.None, _t.OnLocationChanged(Win(99, "Discord.exe", new PixelRect(0, 0, 10, 10))).Action);
    }

    [Fact]
    public void Tracker_SameRect_None()
    {
        Assert.Equal(LockAction.None, _t.OnLocationChanged(Discord(DiscordTarget)).Action);
    }

    [Fact]
    public void Tracker_RevertsStopAfterPolicyLimit()
    {
        var moved = Discord(new PixelRect(0, 0, 10, 10));
        for (int i = 0; i < 5; i++) Assert.Equal(LockAction.Revert, _t.OnLocationChanged(moved).Action);

        Assert.Equal(LockAction.None, _t.OnLocationChanged(moved).Action);
    }

    [Fact]
    public void Tracker_NewMatchingWindowShown_PlaceNew_AndBinds()
    {
        var preset = new Preset { Name = "p", Windows = [_discord] };
        var t = new LockTracker(preset, () => [Primary, Secondary], new SnapBackPolicy(_clock));

        var d = t.OnShown(Win(42, "Discord.exe", new PixelRect(0, 0, 800, 600)));

        Assert.Equal(LockAction.PlaceNew, d.Action);
        Assert.Equal(DiscordTarget, d.Target);
        Assert.Equal(_discord.Id, t.Bound[42]);
    }

    [Fact]
    public void Tracker_ShownNonCandidate_IsIgnored()
    {
        var preset = new Preset { Name = "p", Windows = [_discord] };
        var t = new LockTracker(preset, () => [Primary, Secondary], new SnapBackPolicy(_clock));

        var d = t.OnShown(Win(42, "Discord.exe", new PixelRect(0, 0, 800, 600), owned: true));

        Assert.Equal(LockAction.None, d.Action);
        Assert.Empty(t.Bound);
    }

    [Fact]
    public void Tracker_ShownWhileEntryAlreadyBound_IsIgnored()
    {
        // e.g. a KakaoTalk chat window opening while the main window is held
        Assert.Equal(LockAction.None, _t.OnShown(Win(43, "Discord.exe", new PixelRect(0, 0, 800, 600))).Action);
        Assert.False(_t.Bound.ContainsKey(43));
    }

    [Fact]
    public void Tracker_BoundWindowReshownElsewhere_Reverts()
    {
        Assert.Equal(LockAction.Revert, _t.OnShown(Discord(new PixelRect(0, 0, 800, 600))).Action);
    }

    [Fact]
    public void Tracker_DestroyedThenNewShown_Rebinds()
    {
        _t.OnDestroyed(5);

        var d = _t.OnShown(Win(50, "Discord.exe", new PixelRect(0, 0, 800, 600)));

        Assert.Equal(LockAction.PlaceNew, d.Action);
        Assert.Equal(_discord.Id, _t.Bound[50]);
        Assert.False(_t.Bound.ContainsKey(5));
    }

    [Fact]
    public void Tracker_GivesUp_ReportsRetryAfterCooldown()
    {
        var moved = Discord(new PixelRect(0, 0, 10, 10));
        for (int i = 0; i < 5; i++) _t.OnLocationChanged(moved);

        var d = _t.OnLocationChanged(moved);

        Assert.Equal(LockAction.None, d.Action);
        Assert.Equal(TimeSpan.FromSeconds(60), d.RetryAfter);
    }

    [Fact]
    public void Tracker_ShownKakaoChat_WhileMainHidden_IsIgnored()
    {
        var kakao = new WindowEntry
        {
            Match = new WindowMatch { ExeName = "KakaoTalk.exe", ClassName = "EVA_Window_Dblclk", SavedTitle = "카카오톡" },
            Monitor = new MonitorRef { DevicePath = "dev-secondary", Width = 2560, Height = 1440 },
            Rect = new RelRect(1588, 0, 460, 951),
        };
        var chat = Win(20, "KakaoTalk.exe", new PixelRect(0, 0, 400, 600), title: "채팅방", cls: "EVA_Window_Dblclk");
        var hiddenMain = Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), title: "카카오톡", cls: "EVA_Window_Dblclk", visible: false);
        var t = new LockTracker(new Preset { Windows = [kakao] }, () => [Primary, Secondary], new SnapBackPolicy(_clock),
            _ => [chat, hiddenMain]);

        Assert.Equal(LockAction.None, t.OnShown(chat).Action);
        Assert.Equal(LockAction.PlaceNew, t.OnShown(hiddenMain with { IsVisible = true }).Action);
        Assert.Equal(_clock.UtcNow, _clock.UtcNow);
    }

    [Fact]
    public void Tracker_MonitorGone_None()
    {
        var preset = new Preset { Name = "p", Windows = [_discord] };
        var t = new LockTracker(preset, () => [Primary], new SnapBackPolicy(_clock));
        t.BindInitial([new EntryResult(_discord.Id, ApplyOutcome.Applied, 5)]);

        Assert.Equal(LockAction.None, t.OnLocationChanged(Discord(new PixelRect(0, 0, 10, 10))).Action);
    }
}
