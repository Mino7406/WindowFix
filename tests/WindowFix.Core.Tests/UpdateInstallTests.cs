using System.Net;
using WindowFix.Core.Model;
using WindowFix.Core.Update;

namespace WindowFix.Core.Tests;

public sealed class UpdateInstallTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "WindowFixTests", Guid.NewGuid().ToString("N"));

    public UpdateInstallTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class BytesHandler(byte[] body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new ByteArrayContent(body) });
    }

    [Fact]
    public async Task Download_WritesFile_WhenSizeMatches()
    {
        var dest = Path.Combine(_dir, "new.exe");

        bool ok = await UpdateDownloader.DownloadAsync(new HttpClient(new BytesHandler(new byte[1234])), "https://x/WindowFix.exe", 1234, dest, default);

        Assert.True(ok);
        Assert.Equal(1234, new FileInfo(dest).Length);
        Assert.False(File.Exists(dest + ".part"));
    }

    [Fact]
    public async Task Download_SizeMismatch_FailsAndLeavesNothing()
    {
        var dest = Path.Combine(_dir, "new.exe");

        bool ok = await UpdateDownloader.DownloadAsync(new HttpClient(new BytesHandler(new byte[100])), "https://x/WindowFix.exe", 1234, dest, default);

        Assert.False(ok);
        Assert.False(File.Exists(dest));
        Assert.False(File.Exists(dest + ".part"));
    }

    [Fact]
    public async Task Download_RejectsNonHttps()
    {
        Assert.False(await UpdateDownloader.DownloadAsync(new HttpClient(new BytesHandler(new byte[10])), "http://x/WindowFix.exe", 10, Path.Combine(_dir, "a.exe"), default));
    }

    [Fact]
    public async Task Download_HttpError_False()
    {
        Assert.False(await UpdateDownloader.DownloadAsync(new HttpClient(new BytesHandler([], HttpStatusCode.NotFound)), "https://x/WindowFix.exe", 0, Path.Combine(_dir, "a.exe"), default));
    }

    [Fact]
    public void Swap_WaitsForOldProcess_ThenCopiesAndStarts()
    {
        var log = new List<string>();
        int polls = 0;

        bool ok = UpdateSwap.Apply("new.exe", "app.exe",
            oldExited: () => ++polls >= 3,
            copy: (s, t) => log.Add($"copy {s}->{t}"),
            start: t => log.Add($"start {t}"),
            sleep: _ => log.Add("sleep"));

        Assert.True(ok);
        Assert.Equal(["sleep", "sleep", "copy new.exe->app.exe", "start app.exe"], log);
    }

    [Fact]
    public void Swap_RetriesWhileTargetIsLocked()
    {
        int tries = 0;
        var started = new List<string>();

        bool ok = UpdateSwap.Apply("new.exe", "app.exe", () => true,
            copy: (_, _) => { if (++tries < 4) throw new IOException("in use"); },
            start: started.Add, sleep: _ => { });

        Assert.True(ok);
        Assert.Equal(4, tries);
        Assert.Equal(["app.exe"], started);
    }

    [Fact]
    public void Swap_GivesUp_AndStillRestartsTheOldApp()
    {
        var started = new List<string>();

        bool ok = UpdateSwap.Apply("new.exe", "app.exe", () => true,
            copy: (_, _) => throw new UnauthorizedAccessException("no write access"),
            start: started.Add, sleep: _ => { }, attempts: 5);

        Assert.False(ok);
        Assert.Equal(["app.exe"], started); // never leave the user without WindowFix running
    }

    [Fact]
    public void Settings_UpdateDefaults()
    {
        var s = new AppSettings();

        Assert.True(s.AutoUpdateCheck);
        Assert.Null(s.LastUpdateCheckUtc);
        Assert.Null(s.SkippedUpdateTag);
    }
}
