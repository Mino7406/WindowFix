using WindowFix.Core.Lock;
using WindowFix.Core.Tests.Fakes;

namespace WindowFix.Core.Tests;

public sealed class SnapBackPolicyTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void SnapBackPolicy_AllowsUpToLimit()
    {
        var p = new SnapBackPolicy(_clock);

        for (int i = 0; i < 5; i++) Assert.True(p.ShouldRevert(1), $"call {i + 1}");
        Assert.False(p.ShouldRevert(1));
    }

    [Fact]
    public void SnapBackPolicy_GivesUpAfterLimit()
    {
        var p = new SnapBackPolicy(_clock);
        for (int i = 0; i < 6; i++) p.ShouldRevert(1);

        _clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(p.ShouldRevert(1));

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(p.ShouldRevert(1));
    }

    [Fact]
    public void SnapBackPolicy_WindowSlides()
    {
        var p = new SnapBackPolicy(_clock);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(p.ShouldRevert(1), $"call {i + 1}");
            _clock.Advance(TimeSpan.FromSeconds(11));
        }
    }

    [Fact]
    public void SnapBackPolicy_PerWindow()
    {
        var p = new SnapBackPolicy(_clock);
        for (int i = 0; i < 6; i++) p.ShouldRevert(1);

        Assert.True(p.ShouldRevert(2));
    }

    [Fact]
    public void SnapBackPolicy_CooldownRemaining()
    {
        var p = new SnapBackPolicy(_clock);
        Assert.Null(p.CooldownRemaining(1));
        for (int i = 0; i < 6; i++) p.ShouldRevert(1);

        _clock.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(40), p.CooldownRemaining(1));
    }

    [Fact]
    public void SnapBackPolicy_ForgetResets()
    {
        var p = new SnapBackPolicy(_clock);
        for (int i = 0; i < 6; i++) p.ShouldRevert(1);

        p.Forget(1);

        Assert.True(p.ShouldRevert(1));
    }
}
