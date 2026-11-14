using WindowFix.Core.Apply;
using WindowFix.Core.Lock;
using WindowFix.Core.Model;

namespace WindowFix.Core.Startup;

/// <summary>
/// Boot-time restore: apply once (unhide only; missing apps are launched after <see cref="LaunchGrace"/>), retry missing windows every 2s for up to 3 minutes,
/// then re-apply everything 5s later because apps (Discord) resize themselves after loading.
/// </summary>
public sealed class StartupSequence(PresetApplier applier, IClock clock, Func<TimeSpan, Task> delay)
{
    /// <summary>Right after logon apps appear within seconds: look every 0.5s for the first 20s, then every 2s.</summary>
    public static readonly TimeSpan FastRetryInterval = TimeSpan.FromSeconds(0.5);
    public static readonly TimeSpan FastPhase = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(3);
    /// <summary>Apps set to start with Windows (Discord, KakaoTalk) open themselves within seconds of logon;
    /// only an app still missing after this is started by WindowFix, so it isn't launched twice.</summary>
    public static readonly TimeSpan LaunchGrace = TimeSpan.FromSeconds(15);

    public async Task<ApplyReport> RunAsync(Preset p, CancellationToken ct)
    {
        var start = clock.UtcNow;
        var retryOptions = new ApplyOptions(Unhide: true, Launch: false);
        var results = applier.Apply(p, retryOptions).Results.ToDictionary(r => r.EntryId);
        bool launched = false;

        while (!new ApplyReport(results.Values.ToList()).AllDone && clock.UtcNow - start < MaxWait)
        {
            ct.ThrowIfCancellationRequested();
            await delay(clock.UtcNow - start < FastPhase ? FastRetryInterval : RetryInterval);
            ct.ThrowIfCancellationRequested();

            var pending = results.Values
                .Where(r => r.Outcome is ApplyOutcome.NotFound or ApplyOutcome.LaunchedWaiting)
                .Select(r => r.EntryId)
                .ToHashSet();
            var opts = retryOptions;
            if (!launched && clock.UtcNow - start >= LaunchGrace)
            {
                launched = true;
                opts = ApplyOptions.Full; // once: start whatever still hasn't opened by itself
            }
            foreach (var r in applier.Apply(p, opts, pending).Results)
            {
                // A launched app that has not shown its window yet is still "launched", not merely missing.
                results[r.EntryId] = r.Outcome == ApplyOutcome.NotFound && results[r.EntryId].Outcome == ApplyOutcome.LaunchedWaiting
                    ? results[r.EntryId]
                    : r;
            }
        }

        await delay(SettleDelay);
        ct.ThrowIfCancellationRequested();
        return applier.Apply(p, ApplyOptions.PlaceOnly);
    }
}
