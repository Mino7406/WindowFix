using WindowFix.Core.Model;
using WindowFix.Core.Startup;

namespace WindowFix.Core.Tests;

public sealed class StartupRegistrationTests
{
    private sealed class FakeRunKey : IRunKey
    {
        public string? Value { get; set; }
        public string? Get() => Value;
        public void Set(string value) => Value = value;
        public void Delete() => Value = null;
    }

    private const string Exe = @"C:\Tools\WindowFix.exe";

    [Fact]
    public void Registration_LockOnly_Writes()
    {
        var key = new FakeRunKey();

        StartupRegistration.Sync(new AppSettings { LockEnabled = true }, Exe, key);

        Assert.Equal("\"C:\\Tools\\WindowFix.exe\" --startup", key.Value);
    }

    [Fact]
    public void Registration_StartupPresetOnly_Writes()
    {
        var key = new FakeRunKey();

        StartupRegistration.Sync(new AppSettings { StartupPresetId = Guid.NewGuid() }, Exe, key);

        Assert.NotNull(key.Value);
    }

    [Fact]
    public void Registration_BothOff_Deletes()
    {
        var key = new FakeRunKey { Value = "old" };

        StartupRegistration.Sync(new AppSettings(), Exe, key);

        Assert.Null(key.Value);
    }

    [Fact]
    public void Registration_QuotesPathWithSpaces()
    {
        var key = new FakeRunKey();

        StartupRegistration.Sync(new AppSettings { LockEnabled = true }, @"C:\Program Files\Window Fix\WindowFix.exe", key);

        Assert.Equal("\"C:\\Program Files\\Window Fix\\WindowFix.exe\" --startup", key.Value);
    }
}
