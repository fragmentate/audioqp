namespace AudIOQP;

public static class AppPaths
{
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudIOQP");

    public static string SettingsPath => Path.Combine(Root, "settings.json");

    public static string LogPath => Path.Combine(Root, "audioqp.log");
}
