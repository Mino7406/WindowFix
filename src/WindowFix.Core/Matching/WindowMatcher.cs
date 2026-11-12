using WindowFix.Core.Model;

namespace WindowFix.Core.Matching;

public static class WindowMatcher
{
    /// <summary>Whether a window is a normal app window a user would put in a preset.</summary>
    public static bool IsCandidate(WindowInfo w) =>
        w.IsVisible && !w.IsOwned && !w.IsToolWindow && !w.IsCloaked && !string.IsNullOrWhiteSpace(w.Title);

    public static bool Matches(WindowMatch m, WindowInfo w)
    {
        if (!string.Equals(m.ExeName, w.ExeName, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(m.TitleContains) &&
            w.Title.IndexOf(m.TitleContains, StringComparison.OrdinalIgnoreCase) < 0) return false;
        if (!string.IsNullOrEmpty(m.ClassName) &&
            !string.Equals(m.ClassName, w.ClassName, StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>Best untaken matching window: class match first, then the largest area.</summary>
    public static WindowInfo? PickBest(WindowMatch m, IEnumerable<WindowInfo> windows, ISet<nint> taken) =>
        windows
            .Where(w => !taken.Contains(w.Handle) && Matches(m, w))
            .OrderByDescending(w => m.ClassName is not null && w.ClassName == m.ClassName)
            .ThenByDescending(w => (long)w.Rect.Width * w.Rect.Height)
            .FirstOrDefault();

    /// <summary>One match per selected window; title is filled only when the same exe was selected more than once.</summary>
    public static IReadOnlyList<WindowMatch> SuggestMatches(IReadOnlyList<WindowInfo> selected)
    {
        var dupExes = selected
            .GroupBy(w => w.ExeName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selected.Select(w => new WindowMatch
        {
            ExeName = w.ExeName,
            ClassName = w.ClassName,
            TitleContains = dupExes.Contains(w.ExeName) ? w.Title : null,
        }).ToList();
    }
}
