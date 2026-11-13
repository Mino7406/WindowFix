using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using WindowFix.Core.Update;

namespace WindowFix;

/// <summary>App-side glue for GitHub release updates: check, download, hand over to the swap step, restart.</summary>
public static class Updater
{
    public const string Owner = "Mino7406";
    public const string Repo = "WindowFix";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static Version Current => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public static string CurrentText => $"v{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";

    private static string TempDir => Path.Combine(Path.GetTempPath(), "WindowFix-update");

    public static Task<ReleaseInfo?> CheckAsync(string? skippedTag) =>
        new UpdateChecker(Http, Owner, Repo).CheckAsync(Current, skippedTag, CancellationToken.None);

    /// <summary>Delete leftovers of a previous update (the downloaded exe), unless we are that exe.</summary>
    public static void CleanupOld()
    {
        try
        {
            if (Directory.Exists(TempDir) && Environment.ProcessPath is { } me &&
                !me.StartsWith(TempDir, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(TempDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // still in use; next start will clean it up
        }
    }

    /// <summary>Download the release exe and start the swap step. True when the swap was launched (caller should exit).</summary>
    public static async Task<bool> InstallAsync(ReleaseInfo r)
    {
        if (r.AssetUrl is null || Environment.ProcessPath is not { } target) return false;
        Directory.CreateDirectory(TempDir);
        var dest = Path.Combine(TempDir, $"WindowFix-{r.Tag}.exe");
        if (!await UpdateDownloader.DownloadAsync(Http, r.AssetUrl, r.AssetSize, dest, CancellationToken.None)) return false;

        var psi = new ProcessStartInfo(dest, $"{UpdateSwap.ApplyArg} \"{target}\" {Environment.ProcessId}") { UseShellExecute = true };
        if (!CanWrite(Path.GetDirectoryName(target)!)) psi.Verb = "runas"; // e.g. installed under Program Files
        try
        {
            using var _ = Process.Start(psi);
            return true;
        }
        catch (Win32Exception)
        {
            return false; // UAC declined
        }
    }

    /// <summary>Runs in the downloaded exe: replace the old exe once it has exited, then start it again.</summary>
    public static void RunSwap(string target, string pidText)
    {
        int.TryParse(pidText, out int pid);
        UpdateSwap.Apply(Environment.ProcessPath!, target,
            oldExited: () =>
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    return p.HasExited;
                }
                catch (ArgumentException)
                {
                    return true; // already gone
                }
            },
            copy: (s, t) => File.Copy(s, t, overwrite: true),
            start: t => { using var _ = Process.Start(new ProcessStartInfo(t) { UseShellExecute = true }); },
            sleep: Thread.Sleep);
    }

    public static void OpenPage(string url)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // no browser registered; nothing else to do
        }
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".wf-write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
