using System.Diagnostics;

namespace WindowFix.Core.Startup;

/// <summary>Highest-privilege scheduled tasks via schtasks.exe (create/delete need an elevated caller; Run does not).</summary>
public sealed class SchtasksElevatedTasks : IElevatedTasks
{
    public bool Exists(string name) => Schtasks($"/Query /TN \"{name}\"") == 0;

    public void CreateLogonTask(string name, string exe, string args) =>
        Schtasks($"/Create /F /TN \"{name}\" /TR \"\\\"{exe}\\\" {args}\" /SC ONLOGON /RL HIGHEST /IT /DELAY 0000:10");

    public void CreateOnDemandTask(string name, string exe) =>
        Schtasks($"/Create /F /TN \"{name}\" /TR \"\\\"{exe}\\\"\" /SC ONCE /ST 00:00 /RL HIGHEST /IT");

    public void Delete(string name) => Schtasks($"/Delete /F /TN \"{name}\"");

    public bool Run(string name) => Schtasks($"/Run /TN \"{name}\"") == 0;

    private static int Schtasks(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return -1;
            p.WaitForExit(10_000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
