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

public static class StartupRegistration
{
    public const string StartupArg = "--startup";

    /// <summary>Runs at Windows start when the lock is on (it needs a live process) or a startup preset is set.</summary>
    public static void Sync(AppSettings s, string exePath, IRunKey key)
    {
        if (s.LockEnabled || s.StartupPresetId is not null)
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
