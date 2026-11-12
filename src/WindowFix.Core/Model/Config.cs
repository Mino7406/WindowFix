namespace WindowFix.Core.Model;

public sealed class WindowMatch
{
    public string ExeName { get; set; } = "";
    public string? TitleContains { get; set; }
    public string? ClassName { get; set; }
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

public sealed class AppSettings
{
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
