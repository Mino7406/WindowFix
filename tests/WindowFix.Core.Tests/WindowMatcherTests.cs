using WindowFix.Core.Matching;
using WindowFix.Core.Model;

namespace WindowFix.Core.Tests;

public sealed class WindowMatcherTests
{
    internal static WindowInfo Win(
        nint h, string exe = "Discord.exe", string title = "Discord", string cls = "Chrome_WidgetWin_1",
        int w = 800, int hgt = 600, bool visible = true, bool owned = false, bool tool = false, bool cloaked = false) =>
        new(h, exe, @"C:\" + exe, title, cls, new PixelRect(0, 0, w, hgt), visible, false, false, owned, tool, cloaked, 100);

    [Fact]
    public void IsCandidate_RejectsOwnedToolCloakedUntitledInvisible()
    {
        Assert.True(WindowMatcher.IsCandidate(Win(1)));
        Assert.False(WindowMatcher.IsCandidate(Win(1, owned: true)));
        Assert.False(WindowMatcher.IsCandidate(Win(1, tool: true)));
        Assert.False(WindowMatcher.IsCandidate(Win(1, cloaked: true)));
        Assert.False(WindowMatcher.IsCandidate(Win(1, title: "")));
        Assert.False(WindowMatcher.IsCandidate(Win(1, visible: false)));
    }

    [Fact]
    public void Matches_ExeCaseInsensitive()
    {
        Assert.True(WindowMatcher.Matches(new WindowMatch { ExeName = "discord.exe" }, Win(1, exe: "Discord.exe")));
        Assert.False(WindowMatcher.Matches(new WindowMatch { ExeName = "KakaoTalk.exe" }, Win(1, exe: "Discord.exe")));
    }

    [Fact]
    public void Matches_TitleContains_CaseInsensitive()
    {
        var m = new WindowMatch { ExeName = "chrome.exe", TitleContains = "youtube" };

        Assert.True(WindowMatcher.Matches(m, Win(1, exe: "chrome.exe", title: "재생목록 - YouTube - Chrome")));
        Assert.False(WindowMatcher.Matches(m, Win(1, exe: "chrome.exe", title: "Gmail - Chrome")));
    }

    [Fact]
    public void Matches_ClassName_MustBeEqualWhenSet()
    {
        var m = new WindowMatch { ExeName = "KakaoTalk.exe", ClassName = "EVA_Window_Dblclk" };

        Assert.True(WindowMatcher.Matches(m, Win(1, exe: "KakaoTalk.exe", cls: "EVA_Window_Dblclk")));
        Assert.False(WindowMatcher.Matches(m, Win(1, exe: "KakaoTalk.exe", cls: "Other")));
    }

    [Fact]
    public void PickBest_PrefersClassMatchThenLargest()
    {
        var main = Win(1, exe: "KakaoTalk.exe", cls: "EVA_Window_Dblclk", w: 800, hgt: 600);
        var other = Win(2, exe: "KakaoTalk.exe", cls: "Other", w: 1200, hgt: 900);
        var noClass = new WindowMatch { ExeName = "KakaoTalk.exe" };
        var withClass = new WindowMatch { ExeName = "KakaoTalk.exe", ClassName = "EVA_Window_Dblclk" };

        Assert.Equal(1, WindowMatcher.PickBest(withClass, [other, main], new HashSet<nint>())?.Handle);
        Assert.Equal(2, WindowMatcher.PickBest(noClass, [main, other], new HashSet<nint>())?.Handle);
    }

    [Fact]
    public void PickBest_PrefersExactSavedTitle()
    {
        var chat = Win(1, exe: "KakaoTalk.exe", title: "채팅방", cls: "EVA_Window_Dblclk", w: 900, hgt: 900);
        var main = Win(2, exe: "KakaoTalk.exe", title: "카카오톡", cls: "EVA_Window_Dblclk", w: 460, hgt: 951);
        var m = new WindowMatch { ExeName = "KakaoTalk.exe", ClassName = "EVA_Window_Dblclk", SavedTitle = "카카오톡" };

        Assert.Equal(2, WindowMatcher.PickBest(m, [chat, main], new HashSet<nint>())?.Handle);
    }

    [Fact]
    public void IsDecoy_OtherWindowOfSameProcessHasSavedTitle_True()
    {
        var chat = Win(1, exe: "KakaoTalk.exe", title: "채팅방", cls: "EVA_Window_Dblclk");
        var hiddenMain = Win(2, exe: "KakaoTalk.exe", title: "카카오톡", cls: "EVA_Window_Dblclk", visible: false);
        var m = new WindowMatch { ExeName = "KakaoTalk.exe", ClassName = "EVA_Window_Dblclk", SavedTitle = "카카오톡" };

        Assert.True(WindowMatcher.IsDecoy(m, chat, [chat, hiddenMain]));
        Assert.False(WindowMatcher.IsDecoy(m, hiddenMain, [chat, hiddenMain]));
    }

    [Fact]
    public void IsDecoy_NoSiblingWithSavedTitle_False()
    {
        var discord = Win(1, title: "#일반 - Discord");
        var m = new WindowMatch { ExeName = "Discord.exe", SavedTitle = "감비아 - Discord" };

        Assert.False(WindowMatcher.IsDecoy(m, discord, [discord]));
        Assert.False(WindowMatcher.IsDecoy(new WindowMatch { ExeName = "Discord.exe" }, discord, [discord]));
    }

    [Fact]
    public void PickBest_SkipsTaken()
    {
        var a = Win(1, w: 1000);
        var b = Win(2, w: 500);
        var m = new WindowMatch { ExeName = "Discord.exe" };

        Assert.Equal(2, WindowMatcher.PickBest(m, [a, b], new HashSet<nint> { 1 })?.Handle);
        Assert.Null(WindowMatcher.PickBest(m, [a, b], new HashSet<nint> { 1, 2 }));
    }

    [Fact]
    public void SuggestMatches_SameExeTwice_FillsTitle()
    {
        var yt = Win(1, exe: "chrome.exe", title: "YouTube - Chrome", cls: "Chrome_WidgetWin_1");
        var gm = Win(2, exe: "chrome.exe", title: "Gmail - Chrome", cls: "Chrome_WidgetWin_1");

        var s = WindowMatcher.SuggestMatches([yt, gm]);

        Assert.Equal("YouTube - Chrome", s[0].TitleContains);
        Assert.Equal("Gmail - Chrome", s[1].TitleContains);
        Assert.Equal("chrome.exe", s[0].ExeName);
        Assert.Equal("Chrome_WidgetWin_1", s[0].ClassName);
    }

    [Fact]
    public void SuggestMatches_UniqueExe_NoTitle()
    {
        var s = WindowMatcher.SuggestMatches([Win(1, exe: "Discord.exe"), Win(2, exe: "KakaoTalk.exe", cls: "EVA_Window_Dblclk")]);

        Assert.Null(s[0].TitleContains);
        Assert.Null(s[1].TitleContains);
        Assert.Equal("EVA_Window_Dblclk", s[1].ClassName);
    }
}
