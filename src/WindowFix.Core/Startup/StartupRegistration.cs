using Microsoft.Win32;
using WindowFix.Core.Model;

namespace WindowFix.Core.Startup;

public interface IRunKey
{
    string? Get();
    void Set(string value);
    void Delete();
}

/// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Run, value "WindowFix". No admin rights needed.</summary>
public sealed class RegistryRunKey : IRunKey
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowFix";

    public string? Get()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }

    public void Set(string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, value, RegistryValueKind.String);
    }

    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

/// <summary>Scheduled tasks that run WindowFix with highest privileges without a UAC prompt.</summary>
public interface IElevatedTasks
{
    bool Exists(string name);
    void CreateLogonTask(string name, string exe, string args);
    void CreateOnDemandTask(string name, string exe);
    void Delete(string name);
    bool Run(string name);
}

public static class StartupRegistration
{
    public const string StartupArg = "--startup";
    public const string StartupTask = "WindowFix 시작 (관리자)";
    public const string OpenTask = "WindowFix 열기 (관리자)";

    /// <summary>Runs at Windows start when the lock is on (it needs a live process) or a startup preset is set.</summary>
    public static void Sync(AppSettings s, string exePath, IRunKey key) => Sync(s, exePath, key, null, selfElevated: false);

    /// <summary>
    /// Normal mode: HKCU Run key. "Always admin" mode: Windows skips elevated apps listed in the Run key, so boot uses a
    /// logon task with highest privileges, plus an on-demand task that lets a normal launch re-open elevated without UAC.
    /// Tasks can only be created/removed while WindowFix itself runs elevated.
    /// </summary>
    public static void Sync(AppSettings s, string exePath, IRunKey key, IElevatedTasks? tasks, bool selfElevated)
    {
        bool boot = s.LockEnabled || s.StartupPresetId is not null;

        if (s.AlwaysAdmin)
        {
            if (key.Get() is not null) key.Delete();
            if (tasks is null || !selfElevated) return;
            if (boot) tasks.CreateLogonTask(StartupTask, exePath, StartupArg);
            else if (tasks.Exists(StartupTask)) tasks.Delete(StartupTask);
            tasks.CreateOnDemandTask(OpenTask, exePath);
            return;
        }

        if (tasks is not null && selfElevated)
        {
            if (tasks.Exists(StartupTask)) tasks.Delete(StartupTask);
            if (tasks.Exists(OpenTask)) tasks.Delete(OpenTask);
        }

        if (boot)
        {
            var value = $"\"{exePath}\" {StartupArg}";
            if (key.Get() != value) key.Set(value);
        }
        else if (key.Get() is not null)
        {
            key.Delete();
        }
    }
}
