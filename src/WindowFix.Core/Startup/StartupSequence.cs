using WindowFix.Core.Apply;
using WindowFix.Core.Lock;
using WindowFix.Core.Model;

namespace WindowFix.Core.Startup;

/// <summary>
/// Boot-time restore: apply once with launch/unhide, retry missing windows every 2s for up to 3 minutes,
/// then re-apply everything 5s later because apps (Discord) resize themselves after loading.
/// </summary>
public sealed class StartupSequence(PresetApplier applier, IClock clock, Func<TimeSpan, Task> delay)
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);

    public async Task<ApplyReport> RunAsync(Preset p, CancellationToken ct)
    {
        var start = clock.UtcNow;
        var results = applier.Apply(p, ApplyOptions.Full).Results.ToDictionary(r => r.EntryId);
        var retryOptions = new ApplyOptions(Unhide: true, Launch: false);

        while (!new ApplyReport(results.Values.ToList()).AllDone && clock.UtcNow - start < MaxWait)
        {
            ct.ThrowIfCancellationRequested();
            await delay(RetryInterval);
            ct.ThrowIfCancellationRequested();

            var pending = results.Values
                .Where(r => r.Outcome is ApplyOutcome.NotFound or ApplyOutcome.LaunchedWaiting)
                .Select(r => r.EntryId)
                .ToHashSet();
            foreach (var r in applier.Apply(p, retryOptions, pending).Results)
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
