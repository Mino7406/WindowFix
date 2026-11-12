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
