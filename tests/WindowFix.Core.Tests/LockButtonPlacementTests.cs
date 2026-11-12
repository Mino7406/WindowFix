using WindowFix.Core.Layout;
using WindowFix.Core.Model;

namespace WindowFix.Core.Tests;

public sealed class LockButtonPlacementTests
{
    private const int Len = 70, Thick = 28;

    private static readonly MonitorInfo Primary =
        new("p", @"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true);

    private static readonly MonitorInfo Secondary =
        new("s", @"\\.\DISPLAY2", new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 0, 1920, 1040), false);

    private static readonly MonitorInfo[] Both = [Primary, Secondary];

    [Fact]
    public void Resolve_NoSaved_DefaultsToSecondaryRightEdgeNearBottom()
    {
        Assert.Equal(new TabRect(3812, 797, Thick, Len, ScreenEdge.Right), LockButtonPlacement.Resolve(null, null, Both, Len, Thick));
    }

    [Fact]
    public void Resolve_SavedOffscreen_FallsBackToDefault()
    {
        Assert.Equal(new TabRect(3812, 797, Thick, Len, ScreenEdge.Right), LockButtonPlacement.Resolve(5000, 100, Both, Len, Thick));
    }

    [Fact]
    public void Resolve_SavedOnScreen_SnapsToNearestEdge()
    {
        Assert.Equal(new TabRect(0, 465, Thick, Len, ScreenEdge.Left), LockButtonPlacement.Resolve(100, 500, Both, Len, Thick));
    }

    [Fact]
    public void Resolve_SingleMonitor_UsesPrimary()
    {
        Assert.Equal(new TabRect(1892, 797, Thick, Len, ScreenEdge.Right), LockButtonPlacement.Resolve(null, null, [Primary], Len, Thick));
    }

    [Fact]
    public void Snap_NearLeftEdgeOfSecondary_GluesToLeft()
    {
        Assert.Equal(new TabRect(1920, 465, Thick, Len, ScreenEdge.Left), LockButtonPlacement.Snap(1930, 500, Both, Len, Thick));
    }

    [Fact]
    public void Snap_NearTop_IsHorizontal()
    {
        Assert.Equal(new TabRect(2465, 0, Len, Thick, ScreenEdge.Top), LockButtonPlacement.Snap(2500, 5, Both, Len, Thick));
    }

    [Fact]
    public void Snap_NearBottom_SitsOnWorkAreaBottom()
    {
        Assert.Equal(new TabRect(2465, 1012, Len, Thick, ScreenEdge.Bottom), LockButtonPlacement.Snap(2500, 1030, Both, Len, Thick));
    }

    [Fact]
    public void Snap_ClampsAlongTheEdge()
    {
        Assert.Equal(new TabRect(3812, 0, Thick, Len, ScreenEdge.Right), LockButtonPlacement.Snap(3839, 10, Both, Len, Thick));
    }

    [Fact]
    public void Snap_PointOutsideAllMonitors_UsesNearestMonitor()
    {
        Assert.Equal(ScreenEdge.Right, LockButtonPlacement.Snap(4100, 500, Both, Len, Thick).Edge);
        Assert.Equal(3812, LockButtonPlacement.Snap(4100, 500, Both, Len, Thick).X);
    }

    [Fact]
    public void TabRect_CenterIsMiddle()
    {
        var t = new TabRect(3812, 797, Thick, Len, ScreenEdge.Right);
        Assert.Equal((3826, 832), (t.CenterX, t.CenterY));
    }

    [Fact]
    public void Corners_GluedToRightEdge_FlatOnTheRight()
    {
        Assert.Equal((9.0, 0.0, 0.0, 9.0), LockButtonPlacement.Corners(ScreenEdge.Right, roundness: 0, radius: 9));
        Assert.Equal((0.0, 9.0, 9.0, 0.0), LockButtonPlacement.Corners(ScreenEdge.Left, roundness: 0, radius: 9));
        Assert.Equal((0.0, 0.0, 9.0, 9.0), LockButtonPlacement.Corners(ScreenEdge.Top, roundness: 0, radius: 9));
        Assert.Equal((9.0, 9.0, 0.0, 0.0), LockButtonPlacement.Corners(ScreenEdge.Bottom, roundness: 0, radius: 9));
    }

    [Fact]
    public void Corners_WhileDragging_SymmetricRoundedRect()
    {
        Assert.Equal((9.0, 9.0, 9.0, 9.0), LockButtonPlacement.Corners(ScreenEdge.Right, roundness: 1, radius: 9));
        Assert.Equal((9.0, 4.5, 4.5, 9.0), LockButtonPlacement.Corners(ScreenEdge.Right, roundness: 0.5, radius: 9));
    }

    [Fact]
    public void Interpolate_EndsExactlyOnTarget()
    {
        var start = new TabRect(3000, 500, 28, 70, ScreenEdge.Right);
        var end = new TabRect(3812, 797, 28, 70, ScreenEdge.Right);

        Assert.Equal(start, LockButtonPlacement.Interpolate(start, end, 0));
        Assert.Equal(end, LockButtonPlacement.Interpolate(start, end, 1));
        Assert.Equal(end, LockButtonPlacement.Interpolate(start, end, 1.7));
    }

    [Fact]
    public void Interpolate_EasesOut()
    {
        var start = new TabRect(0, 0, 28, 70, ScreenEdge.Left);
        var end = new TabRect(800, 0, 70, 28, ScreenEdge.Top);

        var mid = LockButtonPlacement.Interpolate(start, end, 0.5);

        Assert.Equal(700, mid.X);      // ease-out cubic: 1 - 0.5^3 = 0.875 of the way
        Assert.Equal(65, mid.Width);   // size morphs too (vertical tab → horizontal tab)
        Assert.Equal(33, mid.Height);
        Assert.Equal(ScreenEdge.Top, mid.Edge);
    }

    [Fact]
    public void IsClick_SmallMove_True()
    {
        Assert.True(LockButtonPlacement.IsClick(2, -3));
    }

    [Fact]
    public void IsClick_Drag_False()
    {
        Assert.False(LockButtonPlacement.IsClick(5, 0));
        Assert.False(LockButtonPlacement.IsClick(0, -12));
    }
}
