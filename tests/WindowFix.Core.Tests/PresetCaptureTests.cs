using WindowFix.Core.Apply;
using WindowFix.Core.Model;
using WindowFix.Core.Tests.Fakes;
using static WindowFix.Core.Tests.Fakes.FakeWindowSystem;

namespace WindowFix.Core.Tests;

public sealed class PresetCaptureTests
{
    private static readonly MonitorInfo[] Monitors = [Primary, Secondary];

    [Fact]
    public void Capture_StoresMonitorRelativeRectAndMatch()
    {
        var discord = Win(1, "Discord.exe", new PixelRect(1914, 0, 1601, 958), cls: "Chrome_WidgetWin_1", maximized: false);
        var kakao = Win(2, "KakaoTalk.exe", new PixelRect(3508, 0, 460, 951), title: "카카오톡", cls: "EVA_Window_Dblclk");

        var p = PresetCapture.Capture("평소", [discord, kakao], Monitors);

        Assert.Equal("평소", p.Name);
        Assert.Equal(2, p.Windows.Count);
        var d = p.Windows[0];
        Assert.Equal("Discord.exe", d.Match.ExeName);
        Assert.Equal("Chrome_WidgetWin_1", d.Match.ClassName);
        Assert.Null(d.Match.TitleContains);
        Assert.Equal("dev-secondary", d.Monitor.DevicePath);
        Assert.Equal(new RelRect(-6, 0, 1601, 958), d.Rect); // 1914-1920: invisible border overhangs the left edge
        Assert.False(d.Maximized);
        Assert.False(d.LaunchIfMissing);
        Assert.Equal(@"C:\Apps\Discord.exe", d.LaunchPath);
        Assert.Equal("Discord", d.DisplayName);
        Assert.Equal(new RelRect(1588, 0, 460, 951), p.Windows[1].Rect);
        Assert.Equal("KakaoTalk", p.Windows[1].DisplayName);
    }

    [Fact]
    public void Capture_Discord_UsesUpdateExeLauncher()
    {
        var w = Win(1, "Discord.exe", new PixelRect(1920, 0, 100, 100),
            path: @"C:\Users\u\AppData\Local\Discord\app-1.0.9200\Discord.exe");

        var e = PresetCapture.Capture("x", [w], Monitors).Windows[0];

        Assert.Equal(@"C:\Users\u\AppData\Local\Discord\Update.exe", e.LaunchPath);
        Assert.Equal("--processStart Discord.exe", e.LaunchArgs);
    }

    [Fact]
    public void Capture_NonDiscordPath_KeepsExePathNoArgs()
    {
        var w = Win(1, "chrome.exe", new PixelRect(1920, 0, 100, 100), path: @"C:\Program Files\Google\Chrome\Application\chrome.exe");

        var e = PresetCapture.Capture("x", [w], Monitors).Windows[0];

        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe", e.LaunchPath);
        Assert.Null(e.LaunchArgs);
    }

    [Fact]
    public void DefaultSelection_OnlyNonPrimaryMonitorWindows()
    {
        var onPrimary = Win(1, "a.exe", new PixelRect(10, 10, 800, 600));
        var onSecondary = Win(2, "b.exe", new PixelRect(2000, 10, 800, 600));

        var sel = PresetCapture.DefaultSelection([onPrimary, onSecondary], Monitors);

        Assert.Equal(2, Assert.Single(sel).Handle);
    }

    [Fact]
    public void DefaultSelection_SingleMonitor_SelectsNothing()
    {
        var w = Win(1, "a.exe", new PixelRect(10, 10, 800, 600));

        Assert.Empty(PresetCapture.DefaultSelection([w], [Primary]));
    }
}
