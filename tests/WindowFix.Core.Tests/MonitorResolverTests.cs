using WindowFix.Core.Layout;
using WindowFix.Core.Model;

namespace WindowFix.Core.Tests;

public sealed class MonitorResolverTests
{
    private static MonitorInfo Mon(string path, string gdi, int w, int h, bool primary) =>
        new(path, gdi, new PixelRect(0, 0, w, h), new PixelRect(0, 0, w, h), primary);

    [Fact]
    public void Resolve_ByDevicePath_EvenIfGdiNameChanged()
    {
        var saved = new MonitorRef { DevicePath = @"\\?\DISPLAY#DEL40F0#abc", IsPrimary = false, Width = 1920, Height = 1080 };
        var current = new[]
        {
            Mon(@"\\?\display#gsm5b7f#xyz", @"\\.\DISPLAY1", 1920, 1080, true),
            Mon(@"\\?\display#del40f0#ABC", @"\\.\DISPLAY7", 2560, 1440, false),
        };

        Assert.Equal(@"\\.\DISPLAY7", MonitorResolver.Resolve(saved, current)?.GdiName);
    }

    [Fact]
    public void Resolve_FallbackPrimaryFlagAndResolution()
    {
        var saved = new MonitorRef { DevicePath = "gone", IsPrimary = false, Width = 1920, Height = 1080 };
        var current = new[]
        {
            Mon("a", @"\\.\DISPLAY1", 1920, 1080, true),
            Mon("b", @"\\.\DISPLAY2", 1920, 1080, false),
        };

        Assert.Equal("b", MonitorResolver.Resolve(saved, current)?.DevicePath);
    }

    [Fact]
    public void Resolve_NothingMatches_ReturnsNull()
    {
        var saved = new MonitorRef { DevicePath = "gone", IsPrimary = false, Width = 1920, Height = 1080 };
        var current = new[] { Mon("a", @"\\.\DISPLAY1", 1920, 1080, true) };

        Assert.Null(MonitorResolver.Resolve(saved, current));
    }
}
