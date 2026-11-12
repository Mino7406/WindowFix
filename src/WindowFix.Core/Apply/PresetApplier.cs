using WindowFix.Core.Layout;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;
using WindowFix.Core.Platform;

namespace WindowFix.Core.Apply;

public enum ApplyOutcome { Applied, NotFound, MonitorMissing, Failed, LaunchedWaiting }

public sealed record EntryResult(Guid EntryId, ApplyOutcome Outcome, nint Handle);

public sealed record ApplyReport(IReadOnlyList<EntryResult> Results)
{
    public bool AllDone => Results.All(r => r.Outcome is ApplyOutcome.Applied or ApplyOutcome.MonitorMissing or ApplyOutcome.Failed);
}

public sealed record ApplyOptions(bool Unhide, bool Launch)
{
    public static ApplyOptions Full { get; } = new(true, true);
    public static ApplyOptions PlaceOnly { get; } = new(false, false);
}

public sealed class PresetApplier(IWindowSystem ws)
{
    public ApplyReport Apply(Preset p, ApplyOptions opts, ISet<Guid>? onlyEntries = null)
    {
        var monitors = ws.GetMonitors();
        var visible = ws.GetWindows(includeHidden: false);
        IReadOnlyList<WindowInfo>? hidden = null;
        var taken = new HashSet<nint>();
        var results = new List<EntryResult>();

        foreach (var e in p.Windows)
        {
            if (onlyEntries is not null && !onlyEntries.Contains(e.Id)) continue;

            var monitor = MonitorResolver.Resolve(e.Monitor, monitors);
            if (monitor is null)
            {
                results.Add(new(e.Id, ApplyOutcome.MonitorMissing, 0));
                continue;
            }

            var w = WindowMatcher.PickBest(e.Match, visible, taken);
            if (w is null && e.LaunchIfMissing)
            {
                if (opts.Unhide)
                {
                    hidden ??= ws.GetWindows(includeHidden: true)
                        .Where(x => !x.IsVisible && !x.IsOwned && !x.IsToolWindow).ToList();
                    w = WindowMatcher.PickBest(e.Match, hidden, taken);
                    if (w is not null) ws.Show(w.Handle);
                }
                if (w is null && opts.Launch && !ws.IsRunning(e.Match.ExeName) && !string.IsNullOrEmpty(e.LaunchPath))
                {
                    ws.Launch(e.LaunchPath, e.LaunchArgs);
                    results.Add(new(e.Id, ApplyOutcome.LaunchedWaiting, 0));
                    continue;
                }
            }

            if (w is null)
            {
                results.Add(new(e.Id, ApplyOutcome.NotFound, 0));
                continue;
            }

            taken.Add(w.Handle);
            results.Add(new(e.Id, ws.SetPlacement(w.Handle, TargetRect(e, monitor), e.Maximized) ? ApplyOutcome.Applied : ApplyOutcome.Failed, w.Handle));
        }
        return new ApplyReport(results);
    }

    /// <summary>Absolute target for an entry on a resolved monitor; clamps only when the monitor's resolution changed.</summary>
    public static PixelRect TargetRect(WindowEntry e, MonitorInfo monitor)
    {
        bool resized = monitor.Bounds.Width != e.Monitor.Width || monitor.Bounds.Height != e.Monitor.Height;
        return Placement.ToAbsolute(e.Rect, monitor, clamp: resized);
    }
}
