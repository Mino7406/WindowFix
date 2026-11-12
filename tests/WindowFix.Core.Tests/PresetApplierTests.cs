using WindowFix.Core.Apply;
using WindowFix.Core.Model;
using WindowFix.Core.Tests.Fakes;
using static WindowFix.Core.Tests.Fakes.FakeWindowSystem;

namespace WindowFix.Core.Tests;

public sealed class PresetApplierTests
{
    private static WindowEntry Entry(string exe, RelRect rect, bool launch = false, bool maximized = false,
        string monitorPath = "dev-secondary", string? cls = null) => new()
    {
        Match = new WindowMatch { ExeName = exe, ClassName = cls },
        Monitor = new MonitorRef { DevicePath = monitorPath, IsPrimary = false, Width = 2560, Height = 1440 },
        Rect = rect,
        Maximized = maximized,
        LaunchIfMissing = launch,
        LaunchPath = @"C:\Apps\" + exe,
        LaunchArgs = "--arg",
        DisplayName = exe,
    };

    private static Preset PresetOf(params WindowEntry[] entries) => new() { Name = "p", Windows = [.. entries] };

    [Fact]
    public void Apply_MovesMatchingWindowToAbsoluteRect()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "Discord.exe", new PixelRect(100, 100, 300, 300)));
        var e = Entry("Discord.exe", new RelRect(-6, 0, 1601, 958));

        var report = new PresetApplier(ws).Apply(PresetOf(e), ApplyOptions.Full);

        Assert.Equal((5, new PixelRect(1914, 0, 1601, 958), false), Assert.Single(ws.Placements));
        var r = Assert.Single(report.Results);
        Assert.Equal(ApplyOutcome.Applied, r.Outcome);
        Assert.Equal(e.Id, r.EntryId);
        Assert.Equal(5, r.Handle);
        Assert.True(report.AllDone);
    }

    [Fact]
    public void Apply_ResolutionChanged_ClampsInsideMonitor()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "Discord.exe", new PixelRect(100, 100, 300, 300)));
        var e = Entry("Discord.exe", new RelRect(-6, 0, 1601, 958));
        e.Monitor.Width = 1920; // saved on a 1920x1080 panel, now 2560x1440
        e.Monitor.Height = 1080;

        new PresetApplier(ws).Apply(PresetOf(e), ApplyOptions.Full);

        Assert.Equal(new PixelRect(1920, 0, 1601, 958), ws.Placements[0].Rect);
    }

    [Fact]
    public void Apply_MonitorMissing_SkipsAndReports()
    {
        var ws = new FakeWindowSystem();
        ws.Monitors.Add(Primary);
        ws.Add(Win(5, "Discord.exe", new PixelRect(100, 100, 300, 300)));

        var report = new PresetApplier(ws).Apply(PresetOf(Entry("Discord.exe", new RelRect(0, 0, 100, 100))), ApplyOptions.Full);

        Assert.Equal(ApplyOutcome.MonitorMissing, report.Results[0].Outcome);
        Assert.Empty(ws.Placements);
        Assert.True(report.AllDone);
    }

    [Fact]
    public void Apply_SameWindowNotUsedTwice()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "Discord.exe", new PixelRect(100, 100, 300, 300)));
        var a = Entry("Discord.exe", new RelRect(0, 0, 100, 100));
        var b = Entry("Discord.exe", new RelRect(200, 0, 100, 100));

        var report = new PresetApplier(ws).Apply(PresetOf(a, b), ApplyOptions.Full);

        Assert.Equal(ApplyOutcome.Applied, report.Results[0].Outcome);
        Assert.Equal(ApplyOutcome.NotFound, report.Results[1].Outcome);
        Assert.False(report.AllDone);
    }

    [Fact]
    public void Apply_HiddenWindow_WithLaunchOption_ShowsThenPlaces()
    {
        var ws = TwoMonitors();
        ws.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), title: "카카오톡", cls: "EVA_Window_Dblclk", visible: false));
        ws.Add(Win(10, "KakaoTalk.exe", new PixelRect(0, 0, 900, 900), cls: "EVA_Window_Dblclk", visible: false, owned: true));

        var report = new PresetApplier(ws).Apply(
            PresetOf(Entry("KakaoTalk.exe", new RelRect(1588, 0, 460, 951), launch: true, cls: "EVA_Window_Dblclk")), ApplyOptions.Full);

        Assert.Equal([9], ws.Shown);
        Assert.Equal(9, Assert.Single(ws.Placements).Hwnd);
        Assert.Equal(ApplyOutcome.Applied, report.Results[0].Outcome);
        Assert.Empty(ws.Launches);
    }

    [Fact]
    public void Apply_HiddenWindow_WithoutLaunchOption_NotFound()
    {
        var ws = TwoMonitors();
        ws.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), visible: false));

        var report = new PresetApplier(ws).Apply(PresetOf(Entry("KakaoTalk.exe", new RelRect(0, 0, 460, 951))), ApplyOptions.Full);

        Assert.Equal(ApplyOutcome.NotFound, report.Results[0].Outcome);
        Assert.Empty(ws.Shown);
    }

    [Fact]
    public void Apply_NotRunning_WithLaunchOption_LaunchesAndReportsWaiting()
    {
        var ws = TwoMonitors();

        var report = new PresetApplier(ws).Apply(PresetOf(Entry("Discord.exe", new RelRect(0, 0, 100, 100), launch: true)), ApplyOptions.Full);

        Assert.Equal((@"C:\Apps\Discord.exe", "--arg"), Assert.Single(ws.Launches));
        Assert.Equal(ApplyOutcome.LaunchedWaiting, report.Results[0].Outcome);
        Assert.False(report.AllDone);
    }

    [Fact]
    public void Apply_LaunchThrows_ReportsLaunchFailedAndContinues()
    {
        var ws = TwoMonitors();
        ws.FailLaunch = true;
        ws.Add(Win(6, "b.exe", new PixelRect(0, 0, 100, 100)));

        var report = new PresetApplier(ws).Apply(
            PresetOf(Entry("Gone.exe", new RelRect(0, 0, 100, 100), launch: true), Entry("b.exe", new RelRect(0, 0, 100, 100))),
            ApplyOptions.Full);

        Assert.Equal(ApplyOutcome.LaunchFailed, report.Results[0].Outcome);
        Assert.Equal(ApplyOutcome.Applied, report.Results[1].Outcome);
        Assert.True(report.AllDone);
    }

    [Fact]
    public void Apply_KakaoChatVisible_MainHidden_UnhidesMainNotChat()
    {
        var ws = TwoMonitors();
        ws.Add(Win(20, "KakaoTalk.exe", new PixelRect(100, 100, 400, 600), title: "친구랑 채팅방", cls: "EVA_Window_Dblclk"));
        ws.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), title: "카카오톡", cls: "EVA_Window_Dblclk", visible: false));
        var e = Entry("KakaoTalk.exe", new RelRect(1588, 0, 460, 951), launch: true, cls: "EVA_Window_Dblclk");
        e.Match.SavedTitle = "카카오톡";

        var report = new PresetApplier(ws).Apply(PresetOf(e), ApplyOptions.Full);

        Assert.Equal([9], ws.Shown);
        Assert.Equal(9, Assert.Single(ws.Placements).Hwnd);
        Assert.Equal(9, report.Results[0].Handle);
    }

    [Fact]
    public void Apply_KakaoChatVisible_MainHidden_NoLaunchOption_LeavesChatAlone()
    {
        var ws = TwoMonitors();
        ws.Add(Win(20, "KakaoTalk.exe", new PixelRect(100, 100, 400, 600), title: "친구랑 채팅방", cls: "EVA_Window_Dblclk"));
        ws.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), title: "카카오톡", cls: "EVA_Window_Dblclk", visible: false));
        var e = Entry("KakaoTalk.exe", new RelRect(1588, 0, 460, 951), cls: "EVA_Window_Dblclk");
        e.Match.SavedTitle = "카카오톡";

        var report = new PresetApplier(ws).Apply(PresetOf(e), ApplyOptions.Full);

        Assert.Empty(ws.Placements);
        Assert.Equal(ApplyOutcome.NotFound, report.Results[0].Outcome);
    }

    [Fact]
    public void Apply_DiscordTitleChanged_NoSibling_StillApplied()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "Discord.exe", new PixelRect(0, 0, 100, 100), title: "#일반 | 서버 - Discord"));
        var e = Entry("Discord.exe", new RelRect(0, 0, 100, 100));
        e.Match.SavedTitle = "🍃 | 감비아_숲속 | 감비아 - Discord";

        Assert.Equal(ApplyOutcome.Applied, new PresetApplier(ws).Apply(PresetOf(e), ApplyOptions.Full).Results[0].Outcome);
    }

    [Fact]
    public void Apply_RunningButNoWindow_WithLaunchOption_NotFoundAndNoLaunch()
    {
        var ws = TwoMonitors();
        ws.Running.Add("Discord.exe");

        var report = new PresetApplier(ws).Apply(PresetOf(Entry("Discord.exe", new RelRect(0, 0, 100, 100), launch: true)), ApplyOptions.Full);

        Assert.Empty(ws.Launches);
        Assert.Equal(ApplyOutcome.NotFound, report.Results[0].Outcome);
    }

    [Fact]
    public void Apply_LaunchNotAllowed_DoesNotLaunch()
    {
        var ws = TwoMonitors();

        var report = new PresetApplier(ws).Apply(
            PresetOf(Entry("Discord.exe", new RelRect(0, 0, 100, 100), launch: true)), new ApplyOptions(Unhide: true, Launch: false));

        Assert.Empty(ws.Launches);
        Assert.Equal(ApplyOutcome.NotFound, report.Results[0].Outcome);
    }

    [Fact]
    public void Apply_SetPlacementFails_ReportsAccessDenied()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "Elevated.exe", new PixelRect(0, 0, 100, 100)));
        ws.Add(Win(6, "Discord.exe", new PixelRect(0, 0, 100, 100)));
        ws.FailPlacement.Add(5);

        var report = new PresetApplier(ws).Apply(
            PresetOf(Entry("Elevated.exe", new RelRect(0, 0, 100, 100)), Entry("Discord.exe", new RelRect(200, 0, 100, 100))),
            ApplyOptions.Full);

        Assert.Equal(ApplyOutcome.Failed, report.Results[0].Outcome);
        Assert.Equal(ApplyOutcome.Applied, report.Results[1].Outcome);
        Assert.True(report.AllDone);
    }

    [Fact]
    public void Apply_MaximizedEntry_PassesMaximizeTrue()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "chrome.exe", new PixelRect(0, 0, 100, 100)));

        new PresetApplier(ws).Apply(PresetOf(Entry("chrome.exe", new RelRect(0, 0, 2560, 1400), maximized: true)), ApplyOptions.Full);

        Assert.True(Assert.Single(ws.Placements).Maximize);
    }

    [Fact]
    public void Apply_OnlyEntries_TouchesOnlyThose()
    {
        var ws = TwoMonitors();
        ws.Add(Win(5, "a.exe", new PixelRect(0, 0, 100, 100)));
        ws.Add(Win(6, "b.exe", new PixelRect(0, 0, 100, 100)));
        var a = Entry("a.exe", new RelRect(0, 0, 100, 100));
        var b = Entry("b.exe", new RelRect(200, 0, 100, 100));

        var report = new PresetApplier(ws).Apply(PresetOf(a, b), ApplyOptions.Full, new HashSet<Guid> { b.Id });

        Assert.Equal(6, Assert.Single(ws.Placements).Hwnd);
        Assert.Equal(b.Id, Assert.Single(report.Results).EntryId);
    }
}
