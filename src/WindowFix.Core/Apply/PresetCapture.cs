using System.Text.RegularExpressions;
using WindowFix.Core.Layout;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;

namespace WindowFix.Core.Apply;

public static partial class PresetCapture
{
    public static Preset Capture(string name, IReadOnlyList<WindowInfo> selected, IReadOnlyList<MonitorInfo> monitors)
    {
        var matches = WindowMatcher.SuggestMatches(selected);
        var preset = new Preset { Name = name };
        for (int i = 0; i < selected.Count; i++)
        {
            var w = selected[i];
            var monitor = Placement.MonitorOf(w.Rect, monitors);
            var (launchPath, launchArgs) = LauncherFor(w.ExePath);
            preset.Windows.Add(new WindowEntry
            {
                Match = matches[i],
                Monitor = Placement.ToRef(monitor),
                Rect = Placement.ToRelative(w.Rect, monitor),
                Maximized = w.IsMaximized,
                LaunchIfMissing = false,
                LaunchPath = launchPath,
                LaunchArgs = launchArgs,
                DisplayName = Path.GetFileNameWithoutExtension(w.ExeName),
            });
        }
        return preset;
    }

    /// <summary>Windows that sit on a non-primary monitor; nothing when there is only one monitor.</summary>
    public static IReadOnlyList<WindowInfo> DefaultSelection(IReadOnlyList<WindowInfo> candidates, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count < 2) return [];
        return candidates.Where(w => !Placement.MonitorOf(w.Rect, monitors).IsPrimary).ToList();
    }

    /// <summary>Discord's exe lives in a versioned folder; launch it through the stable Update.exe instead.</summary>
    private static (string? Path, string? Args) LauncherFor(string? exePath)
    {
        if (exePath is not null && DiscordVersioned().Match(exePath) is { Success: true } m)
            return (System.IO.Path.Combine(m.Groups["root"].Value, "Update.exe"), "--processStart Discord.exe");
        return (exePath, null);
    }

    [GeneratedRegex(@"^(?<root>.*\\Discord[^\\]*)\\app-[^\\]+\\Discord\.exe$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscordVersioned();
}
