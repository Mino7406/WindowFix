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

    private sealed class FakeTasks : IElevatedTasks
    {
        public Dictionary<string, string> Tasks { get; } = [];
        public bool Exists(string name) => Tasks.ContainsKey(name);
        public void CreateLogonTask(string name, string exe, string args) => Tasks[name] = $"logon:{exe} {args}";
        public void CreateOnDemandTask(string name, string exe) => Tasks[name] = $"demand:{exe}";
        public void Delete(string name) => Tasks.Remove(name);
        public bool Run(string name) => Tasks.ContainsKey(name);
    }

    private const string Exe = @"C:\Tools\WindowFix.exe";

    [Fact]
    public void AlwaysAdmin_Elevated_UsesHighestPrivilegeTasksInsteadOfRunKey()
    {
        var key = new FakeRunKey { Value = "old" };
        var tasks = new FakeTasks();

        StartupRegistration.Sync(new AppSettings { AlwaysAdmin = true, LockEnabled = true }, Exe, key, tasks, selfElevated: true);

        Assert.Null(key.Value); // Windows skips elevated apps in the Run key
        Assert.Equal($"logon:{Exe} --startup", tasks.Tasks[StartupRegistration.StartupTask]);
        Assert.Equal($"demand:{Exe}", tasks.Tasks[StartupRegistration.OpenTask]);
    }

    [Fact]
    public void AlwaysAdmin_NothingToGuardAtBoot_KeepsOnlyTheOpenTask()
    {
        var tasks = new FakeTasks();
        tasks.Tasks[StartupRegistration.StartupTask] = "x";

        StartupRegistration.Sync(new AppSettings { AlwaysAdmin = true }, Exe, new FakeRunKey(), tasks, selfElevated: true);

        Assert.False(tasks.Exists(StartupRegistration.StartupTask));
        Assert.True(tasks.Exists(StartupRegistration.OpenTask));
    }

    [Fact]
    public void AlwaysAdminOff_Elevated_RemovesTasksAndUsesRunKey()
    {
        var key = new FakeRunKey();
        var tasks = new FakeTasks();
        tasks.Tasks[StartupRegistration.StartupTask] = "x";
        tasks.Tasks[StartupRegistration.OpenTask] = "y";

        StartupRegistration.Sync(new AppSettings { LockEnabled = true }, Exe, key, tasks, selfElevated: true);

        Assert.Empty(tasks.Tasks);
        Assert.NotNull(key.Value);
    }

    [Fact]
    public void AlwaysAdmin_NotElevated_LeavesTasksAlone()
    {
        var tasks = new FakeTasks();

        StartupRegistration.Sync(new AppSettings { AlwaysAdmin = true, LockEnabled = true }, Exe, new FakeRunKey(), tasks, selfElevated: false);

        Assert.Empty(tasks.Tasks); // creating a highest-privilege task needs admin; done once WindowFix runs elevated
    }

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
