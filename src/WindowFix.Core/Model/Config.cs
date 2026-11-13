namespace WindowFix.Core.Model;

public sealed class WindowMatch
{
    public string ExeName { get; set; } = "";
    public string? TitleContains { get; set; }
    public string? ClassName { get; set; }

    /// <summary>Title when the layout was saved; a tie-break hint, never a requirement.</summary>
    public string? SavedTitle { get; set; }
}

public sealed class MonitorRef
{
    public string DevicePath { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class WindowEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public WindowMatch Match { get; set; } = new();
    public MonitorRef Monitor { get; set; } = new();
    public RelRect Rect { get; set; }
    public bool Maximized { get; set; }
    public bool LaunchIfMissing { get; set; }
    public string? LaunchPath { get; set; }
    public string? LaunchArgs { get; set; }
    public string DisplayName { get; set; } = "";
}

public sealed class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<WindowEntry> Windows { get; set; } = [];
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode { System, Light, Dark }

public static class ThemeResolver
{
    /// <summary>Whether to show the dark theme; <paramref name="systemDark"/> is Windows' app mode.</summary>
    public static bool IsDark(ThemeMode mode, bool systemDark) => mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => systemDark,
    };
}

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>Run WindowFix elevated (needed to move windows of admin apps such as some games); boot via a scheduled task.</summary>
    public bool AlwaysAdmin { get; set; }
    public bool LockEnabled { get; set; }
    public Guid? CurrentPresetId { get; set; }
    public Guid? StartupPresetId { get; set; }
    public bool ShowTrayIcon { get; set; }
    public bool ShowLockButton { get; set; } = true;
    public int? LockButtonX { get; set; }
    public int? LockButtonY { get; set; }
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public List<Preset> Presets { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}
