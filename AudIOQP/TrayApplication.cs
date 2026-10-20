using AudIOQP.Audio;
using AudIOQP.Hotkeys;
using AudIOQP.Settings;
using AudIOQP.UI;

namespace AudIOQP;

internal sealed class TrayApplication : ApplicationContext
{
    readonly SynchronizationContext _sync;
    readonly SettingsStore _store;
    readonly AudioDeviceService _audio;
    readonly HotkeyWindow _hotkeys;
    readonly HotkeyRegistrar _registrar;
    readonly NotifyIcon _notify;
    readonly Icon _icon;
    AppSettings _settings;
    List<DevicePickerForm>? _pickers;
    PreferencesForm? _preferences;
    bool _closingPickers;
    bool _handlingHotkey;
    bool _exiting;

    public TrayApplication()
    {
        _sync = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _icon = AppIcon.Create();
        _store = new SettingsStore();
        var firstLaunch = !_store.Exists;
        _settings = _store.Load();
        _audio = new AudioDeviceService();
        _audio.DevicesChanged += (_, _) => _sync.Post(_ => OnDevicesChanged(), null);

        _hotkeys = new HotkeyWindow();
        _hotkeys.Create();
        // Windows grants this process the right to take the foreground only while WM_HOTKEY
        // is still being processed, so the picker is opened on this call and not posted.
        _hotkeys.HotkeyPressed += (_, id) => OnHotkey(id);
        _registrar = new HotkeyRegistrar(_hotkeys.Handle);

        _notify = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = TrimTooltip("AudIOQP")
        };
        _notify.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowPicker();
        };

        OnDevicesChanged();
        if (_settings.StartWithWindows)
        {
            var startupError = StartupRegistration.Apply(true);
            if (startupError != null)
                Log.Error(startupError);
        }

        if (!_store.Exists)
        {
            try
            {
                _store.Save(_settings);
            }
            catch (Exception ex)
            {
                Log.Error("Couldn't create the settings file.", ex);
            }
        }

        var hotkeyError = ApplyHotkeys(_settings);
        RebuildMenu();
        UpdateTooltip();
        Log.Info("AudIOQP is running. Default device: " + (_audio.DefaultDevice?.FriendlyName ?? "(none)"));

        if (hotkeyError != null)
            Balloon("Shortcut not registered", hotkeyError);
        else if (firstLaunch)
        {
            Balloon("AudIOQP is running",
                $"Press {_settings.OpenModal.Display} to choose a playback device. Right-click the tray icon for settings.");
        }

        _audio.StartWatching();
    }

    void OnDevicesChanged()
    {
        if (_exiting)
            return;
        var changed = SettingsLogic.InitializeFirstRun(_settings, _audio.Devices, _audio.DefaultDevice);
        changed |= SettingsLogic.ReconcileIdentifiers(_settings, _audio.Devices);
        if (changed)
        {
            try
            {
                _store.Save(_settings);
            }
            catch (Exception ex)
            {
                Log.Error("Couldn't save updated device names.", ex);
            }
        }

        UpdateTooltip();
        var cycle = DeviceSelector.CycleList(_audio.Devices, _settings.Whitelist);
        if (_pickers != null)
        {
            foreach (var picker in _pickers)
                picker.SetDevices(cycle, _audio.DefaultDevice?.Id);
        }

        _preferences?.ReplaceDevices(_audio.Devices);
        Log.Info("Devices: " + string.Join(", ", _audio.Devices.Select(d => $"{d.FriendlyName} [{d.Status}]")));
    }

    void OnHotkey(int id)
    {
        if (_exiting || _handlingHotkey)
            return;
        _handlingHotkey = true;
        try
        {
            switch (id)
            {
                case HotkeyRegistrar.ModalId:
                    if (_pickers is { Count: > 0 })
                        ClosePickers();
                    else
                        ShowPicker();
                    break;
                case HotkeyRegistrar.CycleId:
                    ClosePickers();
                    Cycle();
                    break;
                case HotkeyRegistrar.HomeId:
                    ClosePickers();
                    JumpHome();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Shortcut action failed.", ex);
            Balloon("AudIOQP", ex.Message);
        }
        finally
        {
            _handlingHotkey = false;
        }
    }

    void ShowPicker()
    {
        if (_exiting)
            return;
        if (_pickers is { Count: > 0 })
        {
            var focused = _pickers.FirstOrDefault(p => p.ContainsFocus) ?? _pickers.LastOrDefault(p => p.Visible);
            if (focused != null)
                Foreground.Activate(focused);
            return;
        }

        var devices = DeviceSelector.CycleList(_audio.Devices, _settings.Whitelist);
        if (devices.Count == 0)
        {
            Balloon("No playback devices", "No whitelisted devices are connected. Open Preferences and check the devices you want.");
            return;
        }

        var screens = ScreenPlacement.TargetScreens(_settings.ModalMode == ModalDisplayMode.AllScreens);
        var cursorName = ScreenPlacement.ScreenAtCursor().DeviceName;
        _pickers = [];
        DevicePickerForm? focusForm = null;
        foreach (var screen in screens)
        {
            var focus = string.Equals(screen.DeviceName, cursorName, StringComparison.OrdinalIgnoreCase);
            var form = new DevicePickerForm(
                devices,
                _audio.DefaultDevice?.Id,
                screen,
                focusOnShow: focus);
            if (focus)
                focusForm = form;
            form.Icon = _icon;
            form.DeviceChosen += (_, device) =>
            {
                ClosePickers();
                SwitchTo(device, overlay: false);
            };
            form.Dismissed += (_, _) => ClosePickers();
            form.FormClosed += (_, _) => ClosePickers();
            _pickers.Add(form);
        }

        foreach (var form in _pickers)
            form.Show();
        if (focusForm != null)
            Foreground.Activate(focusForm);
    }

    void ClosePickers()
    {
        if (_closingPickers || _pickers == null)
            return;
        _closingPickers = true;
        try
        {
            foreach (var form in _pickers.ToArray())
            {
                if (!form.IsDisposed)
                    form.Dismiss();
            }
        }
        finally
        {
            _pickers = null;
            _closingPickers = false;
        }
    }

    void Cycle()
    {
        var devices = DeviceSelector.CycleList(_audio.Devices, _settings.Whitelist);
        var next = DeviceSelector.Next(devices, _audio.DefaultDevice?.Id);
        if (next == null)
        {
            Balloon("No playback devices", "No whitelisted devices are connected.");
            return;
        }

        SwitchTo(next, overlay: true);
    }

    void JumpHome()
    {
        if (string.IsNullOrWhiteSpace(_settings.HomeDeviceId) && string.IsNullOrWhiteSpace(_settings.HomeDeviceName))
        {
            Balloon("No home device", "Choose a home device in Preferences.");
            return;
        }

        var home = DeviceSelector.ResolveActive(_audio.Devices, _settings.HomeDeviceId, _settings.HomeDeviceName);
        if (home == null)
        {
            var name = string.IsNullOrWhiteSpace(_settings.HomeDeviceName) ? "The home device" : _settings.HomeDeviceName;
            Balloon("Home device unavailable", name + " is not connected.");
            return;
        }

        SwitchTo(home, overlay: true);
    }

    void SwitchTo(AudioEndpoint device, bool overlay)
    {
        try
        {
            _audio.SetDefault(device.Id, _settings.SetCommunicationsRole);
            Log.Info("Switched default playback device to " + device.FriendlyName);
            _audio.Refresh();
            if (overlay && _settings.ShowOverlays)
                OverlayForm.ShowMessage(device.FriendlyName, "Audio output");
            UpdateTooltip();
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't switch to " + device.FriendlyName, ex);
            Balloon("Couldn't switch device", ex.Message);
        }
    }

    void ShowPreferences()
    {
        if (_preferences != null)
        {
            Foreground.Activate(_preferences);
            return;
        }

        ClosePickers();
        _preferences = new PreferencesForm(_settings.Clone(), _audio.Devices, ApplyFromDialog)
        {
            Icon = _icon
        };
        _preferences.FormClosed += (_, _) => _preferences = null;
        _preferences.ShowDialog();
    }

    string? ApplyFromDialog(AppSettings draft)
    {
        var hotkeyError = ApplyHotkeys(draft);
        if (hotkeyError != null)
            return hotkeyError;

        var startupError = StartupRegistration.Apply(draft.StartWithWindows);
        if (startupError != null)
        {
            ApplyHotkeys(_settings);
            return startupError;
        }

        _settings = draft.Clone();
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't save preferences.", ex);
            return "Couldn't save preferences. " + ex.Message;
        }

        RebuildMenu();
        UpdateTooltip();
        return null;
    }

    string? ApplyHotkeys(AppSettings settings)
    {
        if (_registrar.TryApply(settings, out var error))
        {
            Log.Info($"Shortcuts: {settings.OpenModal.Display}, {settings.Cycle.Display}, {settings.Home.Display}");
            return null;
        }

        Log.Error(error ?? "Couldn't register shortcuts.");
        return error;
    }

    void RebuildMenu()
    {
        var menu = new ContextMenuStrip();
        var current = new ToolStripMenuItem(CurrentLabel()) { Enabled = false };
        var open = new ToolStripMenuItem("Switch device…", null, (_, _) => ShowPicker())
        {
            ShortcutKeyDisplayString = _settings.OpenModal.Display
        };
        var cycle = new ToolStripMenuItem("Next device", null, (_, _) => Cycle())
        {
            ShortcutKeyDisplayString = _settings.Cycle.Display
        };
        var home = new ToolStripMenuItem("Home device", null, (_, _) => JumpHome())
        {
            ShortcutKeyDisplayString = _settings.Home.Display
        };
        var preferences = new ToolStripMenuItem("Preferences…", null, (_, _) => ShowPreferences());
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());
        menu.Items.Add(current);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(open);
        menu.Items.Add(cycle);
        menu.Items.Add(home);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(preferences);
        menu.Items.Add(exit);
        menu.Opening += (_, _) => current.Text = CurrentLabel();
        var previous = _notify.ContextMenuStrip;
        _notify.ContextMenuStrip = menu;
        previous?.Dispose();
    }

    string CurrentLabel()
    {
        var name = _audio.DefaultDevice?.FriendlyName;
        return string.IsNullOrWhiteSpace(name) ? "Current: (none)" : "Current: " + name;
    }

    void UpdateTooltip()
    {
        var name = _audio.DefaultDevice?.FriendlyName;
        _notify.Text = TrimTooltip(string.IsNullOrWhiteSpace(name) ? "AudIOQP" : "AudIOQP — " + name);
    }

    static string TrimTooltip(string text) => text.Length <= 63 ? text : text[..62] + "…";

    void Balloon(string title, string message)
    {
        _notify.BalloonTipTitle = title;
        _notify.BalloonTipText = message;
        _notify.ShowBalloonTip(4000);
    }

    void ExitApplication()
    {
        if (_exiting)
            return;
        _exiting = true;
        ClosePickers();
        _preferences?.Close();
        _registrar.UnregisterAll();
        _audio.Dispose();
        _hotkeys.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        _icon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_exiting)
            ExitApplication();
        base.Dispose(disposing);
    }
}
