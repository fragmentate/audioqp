using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudIOQP.Settings;

public sealed class SettingsStore
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Path { get; }

    public SettingsStore(string? path = null)
    {
        Path = path ?? AppPaths.SettingsPath;
    }

    public bool Exists => File.Exists(Path);

    public AppSettings Load()
    {
        if (!File.Exists(Path))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(Path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            SettingsLogic.Repair(settings);
            return settings;
        }
        catch (Exception ex)
        {
            Log.Error("Settings file could not be read. Defaults will be used until the next save.", ex);
            TryBackup(Path);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        SettingsLogic.Repair(settings);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, Options);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, Path, overwrite: true);
    }

    static void TryBackup(string path)
    {
        try
        {
            var backup = path + ".bak";
            File.Copy(path, backup, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't keep a backup of the unreadable settings file.", ex);
        }
    }
}
