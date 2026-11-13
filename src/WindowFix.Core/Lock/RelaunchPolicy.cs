namespace WindowFix.Core.Lock;

/// <summary>"Relaunch if closed": at most <c>max</c> restarts per entry within <c>window</c>, so a crash loop can't spin forever.</summary>
public sealed class RelaunchPolicy(IClock clock, int max = 3, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(10);
    private readonly Dictionary<Guid, Queue<DateTime>> _history = [];

    public bool MayRelaunch(Guid entryId)
    {
        var now = clock.UtcNow;
        if (!_history.TryGetValue(entryId, out var q)) _history[entryId] = q = new Queue<DateTime>();
        while (q.Count > 0 && now - q.Peek() >= _window) q.Dequeue();
        if (q.Count >= max) return false;
        q.Enqueue(now);
        return true;
    }
}
