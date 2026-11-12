using WindowFix.Core.Model;
using WindowFix.Core.Storage;

namespace WindowFix.Core.Tests;

public sealed class ThemeTests
{
    [Theory]
    [InlineData(ThemeMode.Dark, true, true)]
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData(ThemeMode.Light, false, false)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.System, true, true)]   // Windows app mode = dark
    [InlineData(ThemeMode.System, false, false)] // Windows app mode = light
    public void IsDark(ThemeMode mode, bool systemDark, bool expected)
    {
        Assert.Equal(expected, ThemeResolver.IsDark(mode, systemDark));
    }

    [Fact]
    public void DefaultTheme_FollowsSystem()
    {
        Assert.Equal(ThemeMode.System, new AppSettings().Theme);
    }

    [Fact]
    public void Theme_RoundTripsAsText()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowFixTests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        store.Save(new AppConfig { Settings = new AppSettings { Theme = ThemeMode.Light } });

        Assert.Contains("\"Light\"", File.ReadAllText(path));
        Assert.Equal(ThemeMode.Light, store.Load().Settings.Theme);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
