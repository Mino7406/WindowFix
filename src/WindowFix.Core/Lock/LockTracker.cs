using WindowFix.Core.Apply;
using WindowFix.Core.Layout;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;

namespace WindowFix.Core.Lock;

public enum LockAction { None, Revert, PlaceNew }

public readonly record struct LockDecision(LockAction Action, PixelRect Target, bool Maximize)
{
    public TimeSpan? RetryAfter { get; init; }

    public static LockDecision None { get; } = new(LockAction.None, default, false);
}

/// <summary>Holds which window belongs to which preset entry and decides what to do on each window event. No OS calls.</summary>
public sealed class LockTracker(Preset preset, Func<IReadOnlyList<MonitorInfo>> monitors, SnapBackPolicy policy,
    Func<int, IReadOnlyList<WindowInfo>>? processWindows = null)
{
    private readonly Dictionary<nint, Guid> _bound = [];

    public Preset Preset => preset;
    public IReadOnlyDictionary<nint, Guid> Bound => _bound;

    public void BindInitial(IEnumerable<EntryResult> applied)
    {
        foreach (var r in applied)
            if (r.Outcome == ApplyOutcome.Applied && r.Handle != 0) _bound[r.Handle] = r.EntryId;
    }

    public LockDecision OnShown(WindowInfo w)
    {
        if (_bound.ContainsKey(w.Handle)) return OnLocationChanged(w);
        if (!WindowMatcher.IsCandidate(w)) return LockDecision.None;

        var boundIds = _bound.Values.ToHashSet();
        var siblings = processWindows?.Invoke(w.ProcessId) ?? [];
        var entry = preset.Windows.FirstOrDefault(e =>
            !boundIds.Contains(e.Id) && WindowMatcher.Matches(e.Match, w) && !WindowMatcher.IsDecoy(e.Match, w, siblings));
        if (entry is null) return LockDecision.None;

        var target = TargetOf(entry);
        if (target is null) return LockDecision.None;

        _bound[w.Handle] = entry.Id;
        return new LockDecision(LockAction.PlaceNew, target.Value, entry.Maximized);
    }

    public LockDecision OnLocationChanged(WindowInfo w)
    {
        if (!_bound.TryGetValue(w.Handle, out var id)) return LockDecision.None;
        if (w.IsMinimized) return LockDecision.None;

        var entry = preset.Windows.FirstOrDefault(e => e.Id == id);
        if (entry is null) return LockDecision.None;

        var target = TargetOf(entry);
        if (target is null) return LockDecision.None;

        bool inPlace = entry.Maximized ? w.IsMaximized : !w.IsMaximized && w.Rect == target.Value;
        if (inPlace) return LockDecision.None;

        return policy.ShouldRevert(w.Handle)
            ? new LockDecision(LockAction.Revert, target.Value, entry.Maximized)
            : LockDecision.None with { RetryAfter = policy.CooldownRemaining(w.Handle) };
    }

    public void OnDestroyed(nint hwnd)
    {
        _bound.Remove(hwnd);
        policy.Forget(hwnd);
    }

    private PixelRect? TargetOf(WindowEntry entry)
    {
        var monitor = MonitorResolver.Resolve(entry.Monitor, monitors());
        return monitor is null ? null : PresetApplier.TargetRect(entry, monitor);
    }
}
