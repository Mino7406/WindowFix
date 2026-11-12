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

    /// <summary>Best untaken matching window: same title as when saved first, then class match, then the largest area.</summary>
    public static WindowInfo? PickBest(WindowMatch m, IEnumerable<WindowInfo> windows, ISet<nint> taken) =>
        windows
            .Where(w => !taken.Contains(w.Handle) && Matches(m, w))
            .OrderByDescending(w => m.SavedTitle is not null && w.Title == m.SavedTitle)
            .ThenByDescending(w => m.ClassName is not null && w.ClassName == m.ClassName)
            .ThenByDescending(w => (long)w.Rect.Width * w.Rect.Height)
            .FirstOrDefault();

    /// <summary>
    /// True when <paramref name="w"/> matches only loosely and another window of the same process carries the exact
    /// saved title — e.g. a KakaoTalk chat window while the real main window ("카카오톡") is hidden in the tray.
    /// </summary>
    public static bool IsDecoy(WindowMatch m, WindowInfo w, IEnumerable<WindowInfo> processWindows) =>
        !string.IsNullOrEmpty(m.SavedTitle) && w.Title != m.SavedTitle &&
        processWindows.Any(o => o.Handle != w.Handle && o.ProcessId == w.ProcessId && !o.IsOwned &&
                                o.Title == m.SavedTitle && Matches(m, o));

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
            SavedTitle = w.Title,
        }).ToList();
    }
}
