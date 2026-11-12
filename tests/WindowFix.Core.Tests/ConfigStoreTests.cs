using WindowFix.Core.Model;
using WindowFix.Core.Storage;

namespace WindowFix.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "WindowFixTests", Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var config = new ConfigStore(ConfigPath).Load();

        Assert.Empty(config.Presets);
        Assert.True(config.Settings.ShowLockButton);
        Assert.False(config.Settings.ShowTrayIcon);
        Assert.False(config.Settings.LockEnabled);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsPresetWithAllFields()
    {
        var entry = new WindowEntry
        {
            Id = Guid.NewGuid(),
            Match = new WindowMatch { ExeName = "Discord.exe", TitleContains = "감비아", ClassName = "Chrome_WidgetWin_1" },
            Monitor = new MonitorRef { DevicePath = @"\\?\DISPLAY#DEL1234#5&abc", IsPrimary = false, Width = 2560, Height = 1440 },
            Rect = new RelRect(-7, 3, 1557, 930),
            Maximized = true,
            LaunchIfMissing = true,
            LaunchPath = @"C:\Users\u\AppData\Local\Discord\Update.exe",
            LaunchArgs = "--processStart Discord.exe",
            DisplayName = "Discord",
        };
        var preset = new Preset { Id = Guid.NewGuid(), Name = "평소", Windows = [entry] };
        var original = new AppConfig
        {
            Presets = [preset],
            Settings = new AppSettings
            {
                LockEnabled = true,
                CurrentPresetId = preset.Id,
                StartupPresetId = preset.Id,
                ShowTrayIcon = true,
                ShowLockButton = false,
                LockButtonX = -100,
                LockButtonY = 900,
            },
        };

        var store = new ConfigStore(ConfigPath);
        store.Save(original);
        var loaded = store.Load();

        var p = Assert.Single(loaded.Presets);
        Assert.Equal(preset.Id, p.Id);
        Assert.Equal("평소", p.Name);
        var e = Assert.Single(p.Windows);
        Assert.Equal(entry.Id, e.Id);
        Assert.Equal("Discord.exe", e.Match.ExeName);
        Assert.Equal("감비아", e.Match.TitleContains);
        Assert.Equal("Chrome_WidgetWin_1", e.Match.ClassName);
        Assert.Equal(entry.Monitor.DevicePath, e.Monitor.DevicePath);
        Assert.False(e.Monitor.IsPrimary);
        Assert.Equal(2560, e.Monitor.Width);
        Assert.Equal(1440, e.Monitor.Height);
        Assert.Equal(new RelRect(-7, 3, 1557, 930), e.Rect);
        Assert.True(e.Maximized);
        Assert.True(e.LaunchIfMissing);
        Assert.Equal(entry.LaunchPath, e.LaunchPath);
        Assert.Equal(entry.LaunchArgs, e.LaunchArgs);
        Assert.Equal("Discord", e.DisplayName);

        var s = loaded.Settings;
        Assert.True(s.LockEnabled);
        Assert.Equal(preset.Id, s.CurrentPresetId);
        Assert.Equal(preset.Id, s.StartupPresetId);
        Assert.True(s.ShowTrayIcon);
        Assert.False(s.ShowLockButton);
        Assert.Equal(-100, s.LockButtonX);
        Assert.Equal(900, s.LockButtonY);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndBacksUp()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{ not json");

        var config = new ConfigStore(ConfigPath).Load();

        Assert.Empty(config.Presets);
        Assert.True(config.Settings.ShowLockButton);
        Assert.True(File.Exists(ConfigPath + ".bak"));
        Assert.Equal("{ not json", File.ReadAllText(ConfigPath + ".bak"));
    }

    [Fact]
    public void Save_IsAtomic_TempThenReplace()
    {
        var store = new ConfigStore(ConfigPath); // directory does not exist yet

        store.Save(new AppConfig());
        store.Save(new AppConfig { Presets = [new Preset { Id = Guid.NewGuid(), Name = "두번째" }] });

        Assert.True(File.Exists(ConfigPath));
        Assert.False(File.Exists(ConfigPath + ".tmp"));
        Assert.Equal("두번째", Assert.Single(store.Load().Presets).Name);
    }
}
