using AudIOQP.Audio.Native;

namespace AudIOQP.Audio;

public sealed class AudioDeviceService : IDisposable
{
    const uint StateActive = 0x1;
    const uint StateDisabled = 0x2;
    const uint StateUnplugged = 0x8;
    // NotPresent entries are leftover endpoints for hardware that is gone. Listing them
    // buries the devices the user can actually select.
    const uint StateMask = StateActive | StateDisabled | StateUnplugged;
    const int ElementNotFound = unchecked((int)0x80070490);

    readonly object _gate = new();
    readonly SynchronizationContext? _sync;
    readonly IMMDeviceEnumerator _enumerator;
    readonly MmNotificationClient _notifications;
    System.Windows.Forms.Timer? _debounce;
    System.Windows.Forms.Timer? _poll;
    string _fingerprint = "";
    bool _watching;
    bool _disposed;

    public AudioDeviceService()
    {
        _sync = SynchronizationContext.Current;
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _notifications = new MmNotificationClient(OnNativeNotification);
        Refresh();
    }

    public IReadOnlyList<AudioEndpoint> Devices { get; private set; } = [];

    public AudioEndpoint? DefaultDevice { get; private set; }

    public event EventHandler? DevicesChanged;

    public void StartWatching()
    {
        if (_watching || _disposed)
            return;
        _watching = true;

        var hr = _enumerator.RegisterEndpointNotificationCallback(_notifications);
        if (hr < 0)
            Log.Error($"Audio device notifications could not be registered (0x{hr:X8}). The device list will refresh on a timer.");
        else
            Log.Info("Listening for audio device changes.");

        _debounce = new System.Windows.Forms.Timer { Interval = 200 };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Refresh();
        };

        _poll = new System.Windows.Forms.Timer { Interval = 5000 };
        _poll.Tick += (_, _) => Refresh();
        _poll.Start();
    }

    public void Refresh()
    {
        IReadOnlyList<AudioEndpoint> devices;
        AudioEndpoint? current;
        var changed = false;
        lock (_gate)
        {
            if (_disposed)
                return;
            try
            {
                devices = Enumerate();
            }
            catch (Exception ex)
            {
                Log.Error("Couldn't list playback devices.", ex);
                return;
            }

            current = TryReadDefault(devices);
            var fingerprint = Fingerprint(devices, current);
            changed = fingerprint != _fingerprint;
            Devices = devices;
            DefaultDevice = current;
            _fingerprint = fingerprint;
        }

        if (changed)
            DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDefault(string deviceId, bool includeCommunications)
    {
        var roles = new List<ERole> { ERole.Multimedia, ERole.Console };
        if (includeCommunications)
            roles.Insert(1, ERole.Communications);
        PolicyConfig.SetDefaultEndpoint(deviceId, roles);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _debounce?.Stop();
        _debounce?.Dispose();
        _poll?.Stop();
        _poll?.Dispose();
        if (_watching)
        {
            try
            {
                _enumerator.UnregisterEndpointNotificationCallback(_notifications);
            }
            catch (Exception ex)
            {
                Log.Error("Couldn't unregister audio device notifications.", ex);
            }
        }

        ComRelease.Release(_enumerator);
    }

    void OnNativeNotification()
    {
        if (_sync != null)
            _sync.Post(_ => Debounce(), null);
        else
            Debounce();
    }

    void Debounce()
    {
        if (_disposed)
            return;
        if (_debounce == null)
        {
            Refresh();
            return;
        }

        _debounce.Stop();
        _debounce.Start();
    }

    List<AudioEndpoint> Enumerate()
    {
        var hr = _enumerator.EnumAudioEndpoints(EDataFlow.Render, StateMask, out var collection);
        if (hr < 0)
            MarshalHr(hr);

        var list = new List<AudioEndpoint>();
        try
        {
            hr = collection.GetCount(out var count);
            if (hr < 0)
                MarshalHr(hr);
            for (uint i = 0; i < count; i++)
            {
                hr = collection.Item(i, out var device);
                if (hr < 0 || device == null)
                    continue;
                try
                {
                    hr = device.GetId(out var id);
                    if (hr < 0 || string.IsNullOrWhiteSpace(id))
                        continue;
                    hr = device.GetState(out var state);
                    if (hr < 0)
                        continue;
                    var name = PropertyStoreReader.ReadName(device, id);
                    list.Add(new AudioEndpoint(id, name, MapState(state)));
                }
                finally
                {
                    ComRelease.Release(device);
                }
            }
        }
        finally
        {
            ComRelease.Release(collection);
        }

        return list;
    }

    AudioEndpoint? TryReadDefault(IReadOnlyList<AudioEndpoint> devices)
    {
        foreach (var role in new[] { ERole.Console, ERole.Multimedia })
        {
            var hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, role, out var device);
            if (hr == ElementNotFound || device == null)
                continue;
            if (hr < 0)
            {
                ComRelease.Release(device);
                Log.Error($"Couldn't read the default playback device (0x{hr:X8}).");
                continue;
            }

            try
            {
                hr = device.GetId(out var id);
                if (hr < 0 || string.IsNullOrWhiteSpace(id))
                    continue;
                return devices.FirstOrDefault(d => DeviceSelector.IdEquals(d.Id, id))
                    ?? new AudioEndpoint(id, PropertyStoreReader.ReadName(device, id), EndpointStatus.Active);
            }
            finally
            {
                ComRelease.Release(device);
            }
        }

        return null;
    }

    static EndpointStatus MapState(uint state)
    {
        if ((state & StateActive) != 0)
            return EndpointStatus.Active;
        if ((state & StateDisabled) != 0)
            return EndpointStatus.Disabled;
        if ((state & StateUnplugged) != 0)
            return EndpointStatus.Unplugged;
        return EndpointStatus.NotPresent;
    }

    static string Fingerprint(IReadOnlyList<AudioEndpoint> devices, AudioEndpoint? current)
    {
        var body = string.Join("|", devices
            .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .Select(d => $"{d.Id}\u001F{d.FriendlyName}\u001F{(int)d.Status}"));
        return body + "\n" + (current?.Id ?? "");
    }

    static void MarshalHr(int hr) => System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
}
