using Microsoft.Win32;

namespace AudIOQP;

public static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "AudIOQP";

    public static string? Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null)
                return "The Windows startup list is unavailable.";

            if (!enabled)
            {
                if (key.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                return null;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
                return "The application path is unknown, so it can't be set to start with Windows.";

            key.SetValue(ValueName, $"\"{exe}\"");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't update the Windows startup setting.", ex);
            return "Couldn't update the Windows startup setting. " + ex.Message;
        }
    }
}
