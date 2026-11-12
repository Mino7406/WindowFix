using System.Text.Json;
using WindowFix.Core.Model;

namespace WindowFix.Core.Storage;

/// <summary>The config file exists but cannot be read right now; never fall back to defaults (that would overwrite it).</summary>
public sealed class ConfigUnavailableException(string message, Exception inner) : Exception(message, inner);

/// <summary>
/// Reads and writes <see cref="AppConfig"/> as JSON. Writes are atomic and flushed; a corrupt file is backed up and
/// replaced by defaults; a file that exists but can't be read (locked, no access) throws instead of losing the user's layouts.
/// </summary>
public sealed class ConfigStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowFix", "config.json");

    public string FilePath => path;

    public AppConfig Load()
    {
        if (!File.Exists(path)) return new AppConfig();

        string text = ReadWithRetry();
        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(text, Options);
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".bak", overwrite: true);
            return new AppConfig();
        }
        return Normalize(config ?? new AppConfig());
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, config, Options);
            fs.Flush(flushToDisk: true);
        }
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }

    private string ReadWithRetry()
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 3) throw new ConfigUnavailableException($"설정 파일을 읽을 수 없어요: {path}", ex);
                Thread.Sleep(200);
            }
        }
    }

    private static AppConfig Normalize(AppConfig c)
    {
        c.Presets ??= [];
        c.Settings ??= new AppSettings();
        c.Presets.RemoveAll(p => p is null);
        foreach (var p in c.Presets)
        {
            p.Name ??= "";
            p.Windows ??= [];
            p.Windows.RemoveAll(w => w is null);
            foreach (var w in p.Windows)
            {
                w.Match ??= new WindowMatch();
                w.Monitor ??= new MonitorRef();
                w.DisplayName ??= w.Match.ExeName;
            }
        }
        return c;
    }
}
