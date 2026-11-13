using WindowFix.Core.Platform;
using static WindowFix.Core.Platform.NativeMethods;

namespace WindowFix.Core.Lock;

/// <summary>
/// Feeds out-of-context WinEvents to a <see cref="LockTracker"/> and carries out its decisions.
/// Start/Stop must be called on a thread with a message loop (the UI thread); callbacks arrive on that thread.
/// </summary>
public sealed class WinEventLockService(IWindowSystem ws) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly List<nint> _hooks = [];
    // Location changes fire for every mouse-cursor move system-wide; listen only in the processes that own a locked
    // window so WindowFix stays asleep while the user just uses the PC.
    private readonly Dictionary<uint, nint> _locationHooks = [];
    private readonly HashSet<nint> _dragging = [];
    private readonly Dictionary<nint, PendingEval> _pending = [];
    private WinEventProc? _proc; // kept in a field so the GC never collects the callback
    private SynchronizationContext? _ui;
    private LockTracker? _tracker;

    public bool IsRunning => _tracker is not null;

    public void Start(LockTracker tracker)
    {
        Stop();
        _ui = SynchronizationContext.Current
              ?? throw new InvalidOperationException("Start must be called on the UI thread.");
        _tracker = tracker;
        _proc = OnEvent;
        const uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, 0, _proc, 0, 0, flags));
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZEEND, 0, _proc, 0, 0, flags));
        _hooks.Add(SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_SHOW, 0, _proc, 0, 0, flags));
        SyncLocationHooks();
    }

    /// <summary>One location hook per process that owns a bound window; called whenever the bound set may change.</summary>
    private void SyncLocationHooks()
    {
        var tracker = _tracker;
        var want = new HashSet<uint>();
        if (tracker is not null)
            foreach (var hwnd in tracker.Bound.Keys)
                if (GetWindowThreadProcessId(hwnd, out uint pid) != 0 && pid != 0) want.Add(pid);

        foreach (var pid in _locationHooks.Keys.Where(p => !want.Contains(p)).ToList())
        {
            UnhookWinEvent(_locationHooks[pid]);
            _locationHooks.Remove(pid);
        }
        if (_proc is null) return;
        foreach (var pid in want.Where(p => !_locationHooks.ContainsKey(p)))
        {
            var h = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, 0, _proc, pid, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (h != 0) _locationHooks[pid] = h;
        }
    }

    public void Stop()
    {
        foreach (var h in _hooks) if (h != 0) UnhookWinEvent(h);
        _hooks.Clear();
        foreach (var h in _locationHooks.Values) UnhookWinEvent(h);
        _locationHooks.Clear();
        foreach (var p in _pending.Values) p.Timer.Dispose();
        _pending.Clear();
        _dragging.Clear();
        _tracker = null;
    }

    public void Dispose() => Stop();

    private void OnEvent(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var tracker = _tracker;
        if (tracker is null || hwnd == 0 || idObject != OBJID_WINDOW || idChild != 0) return;
        bool bound = tracker.Bound.ContainsKey(hwnd);

        switch (evt)
        {
            case EVENT_SYSTEM_MOVESIZESTART:
                if (bound) _dragging.Add(hwnd);
                break;
            case EVENT_SYSTEM_MOVESIZEEND:
                if (_dragging.Remove(hwnd)) Evaluate(hwnd, shown: false, userInitiated: true);
                break;
            case EVENT_OBJECT_LOCATIONCHANGE:
            case EVENT_SYSTEM_MINIMIZEEND:
                if (bound && !_dragging.Contains(hwnd)) Schedule(hwnd, shown: false);
                break;
            case EVENT_OBJECT_SHOW:
                // tooltips, menus and the like show constantly; ignore them unless something is still unaccounted for
                if ((bound || tracker.HasUnboundEntries) && GetAncestor(hwnd, GA_ROOT) == hwnd) Schedule(hwnd, shown: true);
                break;
            case EVENT_OBJECT_DESTROY:
                if (bound) Forget(hwnd);
                break;
        }
    }

    private void Schedule(nint hwnd, bool shown, TimeSpan? delay = null)
    {
        var due = delay ?? Debounce;
        if (_pending.TryGetValue(hwnd, out var p))
        {
            p.Shown |= shown;
            p.Timer.Change(due, Timeout.InfiniteTimeSpan);
            return;
        }
        var timer = new Timer(_ => _ui?.Post(_ => Fire(hwnd), null), null, due, Timeout.InfiniteTimeSpan);
        _pending[hwnd] = new PendingEval(timer) { Shown = shown };
    }

    private void Fire(nint hwnd)
    {
        if (!_pending.Remove(hwnd, out var p)) return;
        p.Timer.Dispose();
        Evaluate(hwnd, p.Shown);
    }

    private void Evaluate(nint hwnd, bool shown, bool userInitiated = false)
    {
        var tracker = _tracker;
        if (tracker is null) return;

        var info = ws.GetWindow(hwnd);
        if (info is null)
        {
            Forget(hwnd);
            return;
        }

        var d = shown ? tracker.OnShown(info) : tracker.OnLocationChanged(info, userInitiated);
        if (d.Action is LockAction.Revert or LockAction.PlaceNew)
            ws.SetPlacement(hwnd, d.Target, d.Maximize);
        if (shown) SyncLocationHooks(); // a newly shown window may now be bound (maybe in a new process)
        else if (d.RetryAfter is { } wait)
            Schedule(hwnd, shown: false, wait + TimeSpan.FromMilliseconds(100)); // look again once the cooldown ends
    }

    private void Forget(nint hwnd)
    {
        bool wasBound = _tracker?.Bound.ContainsKey(hwnd) == true;
        _tracker?.OnDestroyed(hwnd);
        _dragging.Remove(hwnd);
        if (wasBound) SyncLocationHooks();
        if (_pending.Remove(hwnd, out var p)) p.Timer.Dispose();
    }

    private sealed class PendingEval(Timer timer)
    {
        public Timer Timer { get; } = timer;
        public bool Shown { get; set; }
    }
}
