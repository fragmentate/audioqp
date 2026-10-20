namespace AudIOQP.Audio;

public static class DeviceSelector
{
    public static AudioEndpoint? ResolveActive(IReadOnlyList<AudioEndpoint> devices, string? id, string? name)
    {
        var active = devices.Where(d => d.IsActive).ToList();
        if (!string.IsNullOrWhiteSpace(id))
        {
            var byId = active.FirstOrDefault(d => IdEquals(d.Id, id));
            if (byId != null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(name))
            return active.FirstOrDefault(d => NameEquals(d.FriendlyName, name));

        return null;
    }

    /// <summary>
    /// Whitelist order, skipping devices that are not currently active.
    /// Endpoint ids are allowed to change across unplug/replug; the friendly name is the fallback.
    /// </summary>
    public static List<AudioEndpoint> CycleList(IReadOnlyList<AudioEndpoint> devices, IReadOnlyList<DeviceRef> whitelist)
    {
        var result = new List<AudioEndpoint>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in whitelist)
        {
            var match = ResolveActive(devices, entry.Id, entry.Name);
            if (match == null || !seen.Add(match.Id))
                continue;
            result.Add(match);
        }

        return result;
    }

    public static AudioEndpoint? Next(IReadOnlyList<AudioEndpoint> cycle, string? currentId)
    {
        if (cycle.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(currentId))
            return cycle[0];

        var index = -1;
        for (var i = 0; i < cycle.Count; i++)
        {
            if (IdEquals(cycle[i].Id, currentId))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return cycle[0];
        return cycle[(index + 1) % cycle.Count];
    }

    public static bool IdEquals(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool NameEquals(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
