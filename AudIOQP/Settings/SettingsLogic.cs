using AudIOQP.Audio;

namespace AudIOQP.Settings;

public static class SettingsLogic
{
    public static void Repair(AppSettings settings)
    {
        settings.OpenModal = Valid(settings.OpenModal, Hotkey.ModalDefault());
        settings.Cycle = Valid(settings.Cycle, Hotkey.CycleDefault());
        settings.Home = Valid(settings.Home, Hotkey.HomeDefault());
        settings.HomeDeviceId ??= "";
        settings.HomeDeviceName ??= "";
        settings.Whitelist ??= [];
        settings.Whitelist.RemoveAll(w =>
            string.IsNullOrWhiteSpace(w.Id) && string.IsNullOrWhiteSpace(w.Name));
    }

    static Hotkey Valid(Hotkey? hotkey, Hotkey fallback) =>
        hotkey is { IsValid: true } ? hotkey : fallback.Clone();

    public static string? ValidateHotkeys(AppSettings settings)
    {
        if (!settings.OpenModal.IsValid || !settings.Cycle.IsValid || !settings.Home.IsValid)
            return "Each shortcut needs Ctrl, Alt, Shift, or Win plus a key.";
        if (settings.OpenModal.ConflictsWith(settings.Cycle) ||
            settings.OpenModal.ConflictsWith(settings.Home) ||
            settings.Cycle.ConflictsWith(settings.Home))
            return "The three shortcuts must be different. Ctrl+Pause and Ctrl+Break count as the same shortcut.";
        return null;
    }

    /// <summary>
    /// Fills the whitelist from active devices the first time any are seen.
    /// </summary>
    public static bool InitializeFirstRun(AppSettings settings, IReadOnlyList<AudioEndpoint> devices, AudioEndpoint? currentDefault)
    {
        if (settings.Initialized)
            return false;
        if (!devices.Any(d => d.IsActive))
            return false;

        settings.Whitelist = devices
            .Where(d => d.IsActive)
            .Select(d => new DeviceRef(d.Id, d.FriendlyName))
            .ToList();
        if (string.IsNullOrWhiteSpace(settings.HomeDeviceId) && currentDefault is { IsActive: true })
        {
            settings.HomeDeviceId = currentDefault.Id;
            settings.HomeDeviceName = currentDefault.FriendlyName;
        }

        settings.Initialized = true;
        return true;
    }

    /// <summary>
    /// When Windows assigns a new endpoint id after replug, keep the whitelist entry and home device
    /// pointed at the active device with the same friendly name.
    /// </summary>
    public static bool ReconcileIdentifiers(AppSettings settings, IReadOnlyList<AudioEndpoint> devices)
    {
        var changed = ReconcileList(settings.Whitelist, devices);
        var active = DeviceSelector.ResolveActive(devices, settings.HomeDeviceId, settings.HomeDeviceName);
        if (active != null &&
            (!DeviceSelector.IdEquals(settings.HomeDeviceId, active.Id) ||
             !DeviceSelector.NameEquals(settings.HomeDeviceName, active.FriendlyName)))
        {
            settings.HomeDeviceId = active.Id;
            settings.HomeDeviceName = active.FriendlyName;
            changed = true;
        }

        return changed;
    }

    static bool ReconcileList(List<DeviceRef> list, IReadOnlyList<AudioEndpoint> devices)
    {
        var changed = false;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            var active = DeviceSelector.ResolveActive(devices, entry.Id, entry.Name);
            if (active == null)
                continue;
            if (!DeviceSelector.IdEquals(entry.Id, active.Id) ||
                !string.Equals(entry.Name, active.FriendlyName, StringComparison.Ordinal))
            {
                list[i] = new DeviceRef(active.Id, active.FriendlyName);
                changed = true;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Count;)
        {
            var active = DeviceSelector.ResolveActive(devices, list[i].Id, list[i].Name);
            if (active != null && !seen.Add(active.Id))
            {
                list.RemoveAt(i);
                changed = true;
                continue;
            }

            i++;
        }

        return changed;
    }
}
