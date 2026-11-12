using WindowFix.Core.Layout;
using WindowFix.Core.Model;

namespace WindowFix.Core.Tests;

public sealed class PlacementTests
{
    private static MonitorInfo Mon(int x, int y, int w, int h, bool primary = false, string path = "p") =>
        new(path, @"\\.\DISPLAY1", new PixelRect(x, y, w, h), new PixelRect(x, y, w, h - 40), primary);

    [Fact]
    public void RoundTrip_SameMonitor_IsExact()
    {
        var m = Mon(1920, 0, 1920, 1080);
        var abs = new PixelRect(1925, 10, 1500, 900);

        var rel = Placement.ToRelative(abs, m);

        Assert.Equal(new RelRect(5, 10, 1500, 900), rel);
        Assert.Equal(abs, Placement.ToAbsolute(rel, m));
    }

    [Fact]
    public void RoundTrip_NegativeMonitorOrigin()
    {
        var m = Mon(-2560, -200, 2560, 1440);
        var abs = new PixelRect(-2550, -190, 1557, 930);

        Assert.Equal(abs, Placement.ToAbsolute(Placement.ToRelative(abs, m), m));
    }

    [Fact]
    public void ToAbsolute_MonitorMovedButSameSize_KeepsRelativeSpot()
    {
        var now = Mon(-1920, 0, 1920, 1080);

        Assert.Equal(new PixelRect(-1915, 10, 100, 100), Placement.ToAbsolute(new RelRect(5, 10, 100, 100), now));
    }

    [Fact]
    public void ToAbsolute_SmallerResolution_ClampsInsideBounds()
    {
        var m = Mon(0, 0, 1920, 1080);

        var r = Placement.ToAbsolute(new RelRect(1800, 0, 1557, 930), m);

        Assert.Equal(new PixelRect(363, 0, 1557, 930), r);
    }

    [Fact]
    public void ToAbsolute_WindowLargerThanMonitor_ShrinksToBounds()
    {
        var m = Mon(1920, 0, 1280, 720);

        var r = Placement.ToAbsolute(new RelRect(-10, -10, 2000, 1000), m);

        Assert.Equal(new PixelRect(1920, 0, 1280, 720), r);
    }

    [Fact]
    public void MonitorOf_PicksLargestOverlap()
    {
        var a = Mon(0, 0, 1920, 1080, primary: true, path: "a");
        var b = Mon(1920, 0, 1920, 1080, path: "b");

        var picked = Placement.MonitorOf(new PixelRect(1800, 100, 800, 600), [a, b]);

        Assert.Equal("b", picked.DevicePath);
    }

    [Fact]
    public void MonitorOf_NoOverlap_ReturnsPrimary()
    {
        var a = Mon(1920, 0, 1920, 1080, path: "a");
        var b = Mon(0, 0, 1920, 1080, primary: true, path: "b");

        var picked = Placement.MonitorOf(new PixelRect(-5000, -5000, 100, 100), [a, b]);

        Assert.Equal("b", picked.DevicePath);
    }

    [Fact]
    public void ToRef_CopiesIdentityAndSize()
    {
        var r = Placement.ToRef(Mon(1920, 0, 2560, 1440, primary: false, path: "dev"));

        Assert.Equal("dev", r.DevicePath);
        Assert.False(r.IsPrimary);
        Assert.Equal(2560, r.Width);
        Assert.Equal(1440, r.Height);
    }
}
