namespace AudIOQP;

public static class Log
{
    static readonly Lock Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception == null ? message : message + " " + exception);
    }

    static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Root);
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
                File.AppendAllText(AppPaths.LogPath, line);
                var info = new FileInfo(AppPaths.LogPath);
                if (info.Length <= 262_144)
                    return;

                var text = File.ReadAllText(AppPaths.LogPath);
                var keep = text.Length > 100_000 ? text[^100_000..] : text;
                File.WriteAllText(AppPaths.LogPath, keep);
            }
        }
        catch
        {
            // Logging must not take down the tray process.
        }
    }
}
