using System.Net;
using WindowFix.Core.Update;

namespace WindowFix.Core.Tests;

public sealed class UpdateCheckerTests
{
    private const string LatestJson = """
    {
      "tag_name": "v0.2.0",
      "html_url": "https://github.com/Mino7406/WindowFix/releases/tag/v0.2.0",
      "body": "새 기능",
      "draft": false,
      "prerelease": false,
      "assets": [
        { "name": "notes.txt", "browser_download_url": "https://example/notes.txt", "size": 10 },
        { "name": "WindowFix.exe", "browser_download_url": "https://github.com/Mino7406/WindowFix/releases/download/v0.2.0/WindowFix.exe", "size": 75523766 }
      ]
    }
    """;

    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("V1.0", "1.0.0")]
    [InlineData("1.2.3", "1.2.3")]
    public void TryParseVersion_AcceptsReleaseTags(string tag, string expected)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var v));
        Assert.Equal(Version.Parse(expected), v);
    }

    [Theory]
    [InlineData("v1.0.0-beta")]
    [InlineData("latest")]
    [InlineData("")]
    public void TryParseVersion_RejectsPrereleaseAndJunk(string tag)
    {
        Assert.False(UpdateChecker.TryParseVersion(tag, out _));
    }

    [Theory]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("1.0", "0.9.9", true)]
    [InlineData("0.1.0", "0.1.0.0", false)] // assembly versions carry a 4th part
    public void IsNewer(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current)));
    }

    [Fact]
    public void ParseLatest_ReadsVersionAssetAndPage()
    {
        var r = UpdateChecker.ParseLatest(LatestJson)!;

        Assert.Equal(new Version(0, 2, 0), r.Version);
        Assert.Equal("v0.2.0", r.Tag);
        Assert.Equal("https://github.com/Mino7406/WindowFix/releases/download/v0.2.0/WindowFix.exe", r.AssetUrl);
        Assert.Equal(75523766, r.AssetSize);
        Assert.Equal("https://github.com/Mino7406/WindowFix/releases/tag/v0.2.0", r.PageUrl);
    }

    [Fact]
    public void ParseLatest_SkipsDraftsAndPrereleases()
    {
        Assert.Null(UpdateChecker.ParseLatest(LatestJson.Replace("\"prerelease\": false", "\"prerelease\": true")));
        Assert.Null(UpdateChecker.ParseLatest(LatestJson.Replace("\"draft\": false", "\"draft\": true")));
    }

    [Fact]
    public void ParseLatest_NoExeAsset_StillReportsPage()
    {
        var r = UpdateChecker.ParseLatest(LatestJson.Replace("WindowFix.exe\"", "Other.zip\""))!;

        Assert.Null(r.AssetUrl);
        Assert.NotNull(r.PageUrl);
    }

    [Fact]
    public void ParseLatest_BrokenJson_Null()
    {
        Assert.Null(UpdateChecker.ParseLatest("{ not json"));
        Assert.Null(UpdateChecker.ParseLatest("{}"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-25.0, true)]
    [InlineData(-23.0, false)]
    [InlineData(5.0, true)] // clock moved backwards: check again
    public void ShouldCheck_OncePerDay(double? hoursAgo, bool expected)
    {
        var now = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        DateTime? last = hoursAgo is null ? null : now.AddHours(hoursAgo.Value);

        Assert.Equal(expected, UpdateChecker.ShouldCheck(last, now));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task CheckAsync_NewerRelease_ReturnsIt_WithGenericUserAgent()
    {
        var h = new FakeHandler(_ => Json(LatestJson));
        var checker = new UpdateChecker(new HttpClient(h), "Mino7406", "WindowFix");

        var r = await checker.CheckAsync(new Version(0, 1, 0), skippedTag: null, default);

        Assert.Equal("v0.2.0", r?.Tag);
        Assert.Equal("https://api.github.com/repos/Mino7406/WindowFix/releases/latest", h.Last!.RequestUri!.ToString());
        Assert.Contains("WindowFix", h.Last.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task CheckAsync_SameVersion_Null()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(_ => Json(LatestJson))), "Mino7406", "WindowFix");

        Assert.Null(await checker.CheckAsync(new Version(0, 2, 0), null, default));
    }

    [Fact]
    public async Task CheckAsync_SkippedVersion_Null()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(_ => Json(LatestJson))), "Mino7406", "WindowFix");

        Assert.Null(await checker.CheckAsync(new Version(0, 1, 0), skippedTag: "v0.2.0", default));
    }

    [Fact]
    public async Task CheckAsync_NotFoundOrOffline_NullWithoutThrowing()
    {
        var notFound = new UpdateChecker(new HttpClient(new FakeHandler(_ => Json("{}", HttpStatusCode.NotFound))), "a", "b");
        var offline = new UpdateChecker(new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline"))), "a", "b");

        Assert.Null(await notFound.CheckAsync(new Version(0, 1, 0), null, default));
        Assert.Null(await offline.CheckAsync(new Version(0, 1, 0), null, default));
    }
}
