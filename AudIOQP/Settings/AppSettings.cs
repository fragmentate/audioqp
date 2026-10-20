using AudIOQP.Audio;

namespace AudIOQP.Settings;

public enum ModalDisplayMode
{
    ActiveScreen = 0,
    AllScreens = 1
}

public sealed class AppSettings
{
    public Hotkey OpenModal { get; set; } = Hotkey.ModalDefault();
    public Hotkey Cycle { get; set; } = Hotkey.CycleDefault();
    public Hotkey Home { get; set; } = Hotkey.HomeDefault();
    public string HomeDeviceId { get; set; } = "";
    public string HomeDeviceName { get; set; } = "";
    public List<DeviceRef> Whitelist { get; set; } = [];
    public ModalDisplayMode ModalMode { get; set; } = ModalDisplayMode.ActiveScreen;
    public bool StartWithWindows { get; set; }
    public bool ShowOverlays { get; set; } = true;
    public bool SetCommunicationsRole { get; set; } = true;

    /// <summary>
    /// Set once the first-run whitelist has been captured, or the user has saved preferences.
    /// Stays false when no playback devices were present yet, so a later arrival can fill the list.
    /// </summary>
    public bool Initialized { get; set; }

    public AppSettings Clone() => new()
    {
        OpenModal = OpenModal.Clone(),
        Cycle = Cycle.Clone(),
        Home = Home.Clone(),
        HomeDeviceId = HomeDeviceId,
        HomeDeviceName = HomeDeviceName,
        Whitelist = Whitelist.Select(w => new DeviceRef(w.Id, w.Name)).ToList(),
        ModalMode = ModalMode,
        StartWithWindows = StartWithWindows,
        ShowOverlays = ShowOverlays,
        SetCommunicationsRole = SetCommunicationsRole,
        Initialized = Initialized
    };
}
