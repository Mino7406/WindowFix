namespace WindowFix.Core.Lock;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Limits how often one window is snapped back, so WindowFix never fights an app that keeps moving itself.
/// Each allowed call counts as one revert; more than <c>maxReverts</c> inside <c>window</c> pauses that window for <c>cooldown</c>.
/// </summary>
public sealed class SnapBackPolicy(IClock clock, int maxReverts = 5, TimeSpan? window = null, TimeSpan? cooldown = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(10);
    private readonly TimeSpan _cooldown = cooldown ?? TimeSpan.FromSeconds(60);
    private readonly Dictionary<nint, Queue<DateTime>> _history = [];
    private readonly Dictionary<nint, DateTime> _cooldownUntil = [];

    public bool ShouldRevert(nint hwnd)
    {
        var now = clock.UtcNow;
        if (_cooldownUntil.TryGetValue(hwnd, out var until))
        {
            if (now < until) return false;
            _cooldownUntil.Remove(hwnd);
        }

        if (!_history.TryGetValue(hwnd, out var q)) _history[hwnd] = q = new Queue<DateTime>();
        while (q.Count > 0 && now - q.Peek() >= _window) q.Dequeue();

        if (q.Count >= maxReverts)
        {
            q.Clear();
            _cooldownUntil[hwnd] = now + _cooldown;
            return false;
        }

        q.Enqueue(now);
        return true;
    }

    public void Forget(nint hwnd)
    {
        _history.Remove(hwnd);
        _cooldownUntil.Remove(hwnd);
    }
}
