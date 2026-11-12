using WindowFix.Core.Apply;
using WindowFix.Core.Model;
using WindowFix.Core.Startup;
using WindowFix.Core.Tests.Fakes;
using static WindowFix.Core.Tests.Fakes.FakeWindowSystem;

namespace WindowFix.Core.Tests;

public sealed class StartupSequenceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeWindowSystem _ws = TwoMonitors();
    private int _delays;
    private Action<int>? _onDelay;

    private StartupSequence Sequence() => new(new PresetApplier(_ws), _clock, ts =>
    {
        _clock.Advance(ts);
        _delays++;
        _onDelay?.Invoke(_delays);
        return Task.CompletedTask;
    });

    private static WindowEntry Entry(string exe, bool launch = false) => new()
    {
        Match = new WindowMatch { ExeName = exe },
        Monitor = new MonitorRef { DevicePath = "dev-secondary", Width = 2560, Height = 1440 },
        Rect = new RelRect(0, 0, 800, 600),
        LaunchIfMissing = launch,
        LaunchPath = @"C:\Apps\" + exe,
    };

    [Fact]
    public async Task Startup_AllPresentImmediately_AppliesThenReappliesAfter5s()
    {
        _ws.Add(Win(1, "Discord.exe", new PixelRect(0, 0, 10, 10)));
        _ws.Add(Win(2, "KakaoTalk.exe", new PixelRect(0, 0, 10, 10)));
        var start = _clock.UtcNow;

        var report = await Sequence().RunAsync(new Preset { Windows = [Entry("Discord.exe"), Entry("KakaoTalk.exe")] }, default);

        Assert.Equal(4, _ws.Placements.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), _clock.UtcNow - start);
        Assert.All(report.Results, r => Assert.Equal(ApplyOutcome.Applied, r.Outcome));
    }

    [Fact]
    public async Task Startup_WindowAppearsLater_RetriesEvery2sUntilFound()
    {
        var start = _clock.UtcNow;
        _onDelay = n => { if (n == 3) _ws.Add(Win(1, "Discord.exe", new PixelRect(0, 0, 10, 10))); };

        var report = await Sequence().RunAsync(new Preset { Windows = [Entry("Discord.exe")] }, default);

        Assert.Equal(ApplyOutcome.Applied, Assert.Single(report.Results).Outcome);
        Assert.Equal(TimeSpan.FromSeconds(3 * 2 + 5), _clock.UtcNow - start);
        Assert.Equal(2, _ws.Placements.Count); // found on retry, then the 5s re-apply
    }

    [Fact]
    public async Task Startup_NeverAppears_StopsAfter3Minutes()
    {
        var start = _clock.UtcNow;

        var report = await Sequence().RunAsync(new Preset { Windows = [Entry("Discord.exe")] }, default);

        Assert.Equal(ApplyOutcome.NotFound, Assert.Single(report.Results).Outcome);
        var elapsed = (_clock.UtcNow - start).TotalSeconds;
        Assert.InRange(elapsed, 180 + 5, 182 + 5); // 3 min of retries, then the 5s re-apply
    }

    [Fact]
    public async Task Startup_LaunchesOnlyOncePerEntry()
    {
        await Sequence().RunAsync(new Preset { Windows = [Entry("Discord.exe", launch: true)] }, default);

        Assert.Single(_ws.Launches);
    }

    [Fact]
    public async Task Startup_HiddenAtFirstThenAppears_IsShownAndPlaced()
    {
        _ws.Running.Add("KakaoTalk.exe");
        _onDelay = n => { if (n == 2) _ws.Windows.Add(Win(9, "KakaoTalk.exe", new PixelRect(0, 0, 460, 951), visible: false)); };

        var report = await Sequence().RunAsync(new Preset { Windows = [Entry("KakaoTalk.exe", launch: true)] }, default);

        Assert.Equal([9], _ws.Shown);
        Assert.Equal(ApplyOutcome.Applied, Assert.Single(report.Results).Outcome);
        Assert.Empty(_ws.Launches);
    }

    [Fact]
    public async Task Startup_Cancelled_StopsEarly()
    {
        using var cts = new CancellationTokenSource();
        _onDelay = n => { if (n == 2) cts.Cancel(); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Sequence().RunAsync(new Preset { Windows = [Entry("Discord.exe")] }, cts.Token));
        Assert.True(_delays <= 3);
    }
}
