namespace WindowFix.Core.Update;

/// <summary>Downloads the release exe over HTTPS to a temp file and only keeps it when the size matches the release.</summary>
public static class UpdateDownloader
{
    public static async Task<bool> DownloadAsync(HttpClient http, string url, long expectedSize, string destPath, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        var part = destPath + ".part";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd("WindowFix-Updater/1.0");
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return false;
            await using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await res.Content.CopyToAsync(file, ct).ConfigureAwait(false);
            }
            long got = new FileInfo(part).Length;
            if (got == 0 || (expectedSize > 0 && got != expectedSize))
            {
                File.Delete(part);
                return false;
            }
            File.Move(part, destPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { }
            return false;
        }
    }
}

/// <summary>
/// Runs inside the freshly downloaded exe (started with <see cref="ApplyArg"/>): waits for the old WindowFix to exit,
/// copies itself over the original exe, then starts it. If the copy keeps failing it still starts the old exe, so the
/// user is never left without WindowFix running. The settings file is never touched.
/// </summary>
public static class UpdateSwap
{
    public const string ApplyArg = "--apply-update";

    public static bool Apply(string source, string target, Func<bool> oldExited, Action<string, string> copy,
        Action<string> start, Action<TimeSpan> sleep, int attempts = 40)
    {
        var pause = TimeSpan.FromMilliseconds(250);
        for (int i = 0; i < attempts && !oldExited(); i++) sleep(pause);

        bool copied = false;
        for (int i = 0; i < attempts && !copied; i++)
        {
            try
            {
                copy(source, target);
                copied = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                sleep(pause); // antivirus or the old process still holding the file
            }
        }
        start(target);
        return copied;
    }
}
