using System.Text.Json;
using WindowFix.Core.Model;

namespace WindowFix.Core.Storage;

/// <summary>Reads and writes <see cref="AppConfig"/> as JSON. Writes are atomic; a corrupt file is backed up and replaced by defaults.</summary>
public sealed class ConfigStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowFix", "config.json");

    public string FilePath => path;

    public AppConfig Load()
    {
        if (!File.Exists(path)) return new AppConfig();

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return new AppConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(text, Options) ?? new AppConfig();
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".bak", overwrite: true);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }
}
