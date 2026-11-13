using System.Net.Http.Headers;
using System.Text.Json;

namespace WindowFix.Core.Update;

/// <summary>A published (non-draft, non-prerelease) GitHub release.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string? AssetUrl, long AssetSize, string PageUrl, string Notes);

/// <summary>
/// Asks GitHub for the latest release of the repo and says whether it is newer than the running build.
/// Never throws for network/HTTP problems (offline, rate limit, no releases yet): it just reports "nothing new".
/// Sends only a generic User-Agent — no user data.
/// </summary>
public sealed class UpdateChecker(HttpClient http, string owner, string repo)
{
    public const string AssetName = "WindowFix.exe";
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(24);

    public async Task<ReleaseInfo?> CheckAsync(Version current, string? skippedTag, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("WindowFix-Updater", "1.0"));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;
            var release = ParseLatest(await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (release is null || !IsNewer(release.Version, current)) return null;
            return string.Equals(release.Tag, skippedTag, StringComparison.OrdinalIgnoreCase) ? null : release;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    public static ReleaseInfo? ParseLatest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (Bool(root, "draft") || Bool(root, "prerelease")) return null;
            var tag = Str(root, "tag_name");
            if (tag is null || !TryParseVersion(tag, out var version)) return null;

            string? assetUrl = null;
            long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    if (!string.Equals(Str(a, "name"), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                    assetUrl = Str(a, "browser_download_url");
                    size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0;
                    break;
                }
            }
            return new ReleaseInfo(version, tag, assetUrl, size, Str(root, "html_url") ?? $"https://github.com/releases/{tag}", Str(root, "body") ?? "");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"v1.2.3", "1.2" → version; pre-release suffixes ("-beta") are not offered as updates.</summary>
    public static bool TryParseVersion(string tag, out Version version)
    {
        version = new Version(0, 0);
        var t = tag.Trim();
        if (t.StartsWith('v') || t.StartsWith('V')) t = t[1..];
        if (t.Length == 0 || t.Contains('-') || t.Contains('+')) return false;
        if (!Version.TryParse(t.Contains('.') ? t : t + ".0", out var v)) return false;
        version = Normalize(v);
        return true;
    }

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    public static bool ShouldCheck(DateTime? lastCheckUtc, DateTime nowUtc) =>
        lastCheckUtc is not { } last || nowUtc - last >= CheckEvery || last > nowUtc;

    /// <summary>Compare as major.minor.patch (assembly versions carry a 4th part, -1 parts become 0).</summary>
    private static Version Normalize(Version v) => new(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
