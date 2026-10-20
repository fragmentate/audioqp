using AudIOQP.Audio;
using AudIOQP.Settings;

namespace AudIOQP.UI;

internal sealed class PreferencesForm : Form
{
    readonly AppSettings _draft;
    List<AudioEndpoint> _devices;
    readonly HotkeyBox _modal = new() { Width = 220 };
    readonly HotkeyBox _cycle = new() { Width = 220 };
    readonly HotkeyBox _home = new() { Width = 220 };
    readonly ComboBox _homeDevice = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Top,
        FlatStyle = FlatStyle.Flat
    };
    readonly CheckedListBox _checks = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle
    };
    readonly RadioButton _activeScreen = new() { Text = "Active screen (the monitor with the pointer)", AutoSize = true };
    readonly RadioButton _allScreens = new() { Text = "All screens (one window centered on each monitor)", AutoSize = true };
    readonly CheckBox _startup = new() { Text = "Start AudIOQP when Windows starts", AutoSize = true };
    readonly CheckBox _overlays = new() { Text = "Show a short confirmation when cycling or jumping home", AutoSize = true };
    readonly CheckBox _communications = new() { Text = "Also switch the default communications device", AutoSize = true };
    readonly Label _error = new() { AutoSize = true, MaximumSize = new Size(480, 0), ForeColor = Theme.Danger };
    readonly Label _deviceCount = new() { AutoSize = true, ForeColor = Theme.Muted, Dock = DockStyle.Top, Padding = new Padding(0, 6, 0, 4) };
    readonly Func<AppSettings, string?>? _tryApply;
    bool _rebuilding;

    public PreferencesForm(AppSettings draft, IReadOnlyList<AudioEndpoint> devices, Func<AppSettings, string?>? tryApply = null)
    {
        _tryApply = tryApply;
        _draft = draft;
        _devices = devices.ToList();
        Text = "AudIOQP Preferences";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(540, 660);
        MinimumSize = new Size(540, 660);
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(12);
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.Controls.Add(BuildHotkeys(), 0, 0);
        root.Controls.Add(BuildDevices(), 0, 1);
        root.Controls.Add(BuildOptions(), 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);
        Controls.Add(root);

        _modal.SetHotkey(_draft.OpenModal);
        _cycle.SetHotkey(_draft.Cycle);
        _home.SetHotkey(_draft.Home);
        _modal.HotkeyChanged += (_, _) => PullHotkeys();
        _cycle.HotkeyChanged += (_, _) => PullHotkeys();
        _home.HotkeyChanged += (_, _) => PullHotkeys();
        _activeScreen.Checked = _draft.ModalMode != ModalDisplayMode.AllScreens;
        _allScreens.Checked = !_activeScreen.Checked;
        _startup.Checked = _draft.StartWithWindows;
        _overlays.Checked = _draft.ShowOverlays;
        _communications.Checked = _draft.SetCommunicationsRole;
        _activeScreen.CheckedChanged += (_, _) => PullOptions();
        _allScreens.CheckedChanged += (_, _) => PullOptions();
        _startup.CheckedChanged += (_, _) => PullOptions();
        _overlays.CheckedChanged += (_, _) => PullOptions();
        _communications.CheckedChanged += (_, _) => PullOptions();
        _checks.ItemCheck += (_, e) =>
        {
            if (_rebuilding)
                return;
            BeginInvoke(SyncWhitelistFromChecks);
        };
        _homeDevice.SelectedIndexChanged += (_, _) =>
        {
            if (_rebuilding)
                return;
            if (_homeDevice.SelectedItem is HomeRow row)
            {
                _draft.HomeDeviceId = row.Id;
                _draft.HomeDeviceName = row.Name;
            }
        };

        _checks.BackColor = Theme.Background;
        _checks.ForeColor = Theme.Text;
        _homeDevice.BackColor = Theme.Background;
        _homeDevice.ForeColor = Theme.Text;
        ScreenPlacement.Attach(this, ScreenPlacement.ScreenAtCursor());
        ReplaceDevices(_devices);
    }

    static GroupBox StyleGroup(GroupBox group)
    {
        group.BackColor = Theme.Background;
        group.ForeColor = Theme.Text;
        return group;
    }

    public void ReplaceDevices(IReadOnlyList<AudioEndpoint> devices)
    {
        _devices = devices.ToList();
        RebuildDeviceLists();
    }

    GroupBox BuildHotkeys()
    {
        var group = StyleGroup(new GroupBox { Text = "Shortcuts", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) });
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddHotkeyRow(table, 0, "Device list", _modal);
        AddHotkeyRow(table, 1, "Cycle devices", _cycle);
        AddHotkeyRow(table, 2, "Home device", _home);
        var help = new Label
        {
            Text = "Click a box and press a combination. Backspace clears it.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill
        };
        table.Controls.Add(help, 0, 3);
        table.SetColumnSpan(help, 2);
        group.Controls.Add(table);
        return group;
    }

    static void AddHotkeyRow(TableLayoutPanel table, int row, string label, HotkeyBox box)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 2, 0, 2);
        table.Controls.Add(box, 1, row);
    }

    Control BuildDevices()
    {
        var group = StyleGroup(new GroupBox
        {
            Text = "Playback devices",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 10, 8)
        });
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Home device", AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(_homeDevice, 0, 1);
        var hint = new Label
        {
            Text = "Checked devices are shown in the switcher and cycled in this order.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill
        };
        var header = new Panel { Dock = DockStyle.Fill };
        _deviceCount.Dock = DockStyle.Bottom;
        hint.Dock = DockStyle.Top;
        header.Controls.Add(_deviceCount);
        header.Controls.Add(hint);
        layout.Controls.Add(header, 0, 2);
        layout.Controls.Add(_checks, 0, 3);
        group.Controls.Add(layout);
        return group;
    }

    Control BuildOptions()
    {
        var group = StyleGroup(new GroupBox { Text = "Behavior", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6) });
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        stack.Controls.Add(_activeScreen, 0, 0);
        stack.Controls.Add(_allScreens, 0, 1);
        stack.Controls.Add(_overlays, 0, 2);
        stack.Controls.Add(_communications, 0, 3);
        stack.Controls.Add(_startup, 0, 4);
        group.Controls.Add(stack);
        return group;
    }

    Control BuildButtons()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.Controls.Add(_error, 0, 0);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var ok = new Button { Text = "OK", Width = 88, Height = 28 };
        var cancel = new Button { Text = "Cancel", Width = 88, Height = 28 };
        var apply = new Button { Text = "Apply", Width = 88, Height = 28 };
        ok.Click += (_, _) =>
        {
            if (!Commit(close: true))
                return;
        };
        cancel.Click += (_, _) => Close();
        apply.Click += (_, _) => Commit(close: false);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(apply);
        panel.Controls.Add(buttons, 0, 1);
        CancelButton = cancel;
        return panel;
    }

    void RebuildDeviceLists()
    {
        _rebuilding = true;
        try
        {
            var rows = BuildRows();
            _checks.Items.Clear();
            foreach (var row in rows)
            {
                var index = _checks.Items.Add(row);
                _checks.SetItemChecked(index, IsWhitelisted(row));
            }

            var previousId = _draft.HomeDeviceId;
            var previousName = _draft.HomeDeviceName;
            _homeDevice.Items.Clear();
            _homeDevice.Items.Add(new HomeRow("", "", "(None)"));
            foreach (var device in _devices.OrderBy(d => d.FriendlyName, StringComparer.OrdinalIgnoreCase))
            {
                var label = device.IsActive
                    ? device.FriendlyName
                    : $"{device.FriendlyName}  ({device.AvailabilityLabel})";
                _homeDevice.Items.Add(new HomeRow(device.Id, device.FriendlyName, label));
            }

            var selected = 0;
            if (!string.IsNullOrWhiteSpace(previousId) || !string.IsNullOrWhiteSpace(previousName))
            {
                var found = -1;
                for (var i = 0; i < _homeDevice.Items.Count; i++)
                {
                    if (_homeDevice.Items[i] is not HomeRow row)
                        continue;
                    if (DeviceSelector.IdEquals(row.Id, previousId) ||
                        (found < 0 && DeviceSelector.NameEquals(row.Name, previousName) && row.Id.Length > 0))
                        found = i;
                    if (DeviceSelector.IdEquals(row.Id, previousId) && previousId.Length > 0)
                        break;
                }

                if (found < 0 && !string.IsNullOrWhiteSpace(previousName))
                {
                    found = _homeDevice.Items.Add(new HomeRow(previousId, previousName, previousName + "  (disconnected)"));
                }

                selected = Math.Max(found, 0);
            }

            if (_homeDevice.Items.Count > 0)
                _homeDevice.SelectedIndex = selected;

            var connected = _devices.Count(d => d.IsActive);
            _deviceCount.Text = connected == 1 ? "1 playback device connected" : $"{connected} playback devices connected";
        }
        finally
        {
            _rebuilding = false;
        }

        SyncWhitelistFromChecks();
    }

    List<DeviceRow> BuildRows()
    {
        var rows = new List<DeviceRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddDevice(AudioEndpoint device)
        {
            if (!seen.Add(device.Id))
                return;
            rows.Add(new DeviceRow(device.Id, device.FriendlyName, device.IsActive, device.AvailabilityLabel));
        }

        foreach (var entry in _draft.Whitelist)
        {
            var live = _devices.FirstOrDefault(d =>
                DeviceSelector.IdEquals(d.Id, entry.Id) || DeviceSelector.NameEquals(d.FriendlyName, entry.Name));
            if (live != null)
                AddDevice(live);
            else if (seen.Add(entry.Id.Length > 0 ? entry.Id : entry.Name))
                rows.Add(new DeviceRow(entry.Id, entry.Name, false, "disconnected"));
        }

        foreach (var device in _devices.OrderBy(d => d.FriendlyName, StringComparer.OrdinalIgnoreCase))
            AddDevice(device);
        return rows;
    }

    bool IsWhitelisted(DeviceRow row)
    {
        return _draft.Whitelist.Any(entry =>
            DeviceSelector.IdEquals(entry.Id, row.Id) ||
            DeviceSelector.NameEquals(entry.Name, row.Name));
    }

    void SyncWhitelistFromChecks()
    {
        if (_rebuilding)
            return;
        var list = new List<DeviceRef>();
        for (var i = 0; i < _checks.Items.Count; i++)
        {
            if (!_checks.GetItemChecked(i) || _checks.Items[i] is not DeviceRow row)
                continue;
            list.Add(new DeviceRef(row.Id, row.Name));
        }

        _draft.Whitelist = list;
    }

    void PullHotkeys()
    {
        if (_modal.Hotkey == null || _cycle.Hotkey == null || _home.Hotkey == null)
        {
            _error.Text = "Each shortcut needs Ctrl, Alt, Shift, or Win plus a key.";
            return;
        }

        _draft.OpenModal = _modal.Hotkey.Clone();
        _draft.Cycle = _cycle.Hotkey.Clone();
        _draft.Home = _home.Hotkey.Clone();
        _error.Text = SettingsLogic.ValidateHotkeys(_draft) ?? "";
    }

    void PullOptions()
    {
        _draft.ModalMode = _allScreens.Checked ? ModalDisplayMode.AllScreens : ModalDisplayMode.ActiveScreen;
        _draft.StartWithWindows = _startup.Checked;
        _draft.ShowOverlays = _overlays.Checked;
        _draft.SetCommunicationsRole = _communications.Checked;
    }

    bool Commit(bool close)
    {
        if (_modal.Hotkey == null || _cycle.Hotkey == null || _home.Hotkey == null)
        {
            _error.Text = "Each shortcut needs Ctrl, Alt, Shift, or Win plus a key.";
            return false;
        }

        PullHotkeys();
        PullOptions();
        SyncWhitelistFromChecks();
        _draft.Initialized = true;
        var problem = SettingsLogic.ValidateHotkeys(_draft);
        if (problem != null)
        {
            _error.Text = problem;
            return false;
        }

        var applyError = _tryApply?.Invoke(_draft.Clone());
        if (!string.IsNullOrEmpty(applyError))
        {
            _error.Text = applyError;
            return false;
        }

        _error.Text = "";
        if (close)
            Close();
        return true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && ActiveControl is not HotkeyBox)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    sealed class DeviceRow(string id, string name, bool available, string availability)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public override string ToString() =>
            available || string.IsNullOrEmpty(availability) ? Name : $"{Name}  ({availability})";
    }

    sealed class HomeRow(string id, string name, string label)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public override string ToString() => label;
    }
}
