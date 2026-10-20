using System.Reflection;
using AudIOQP.Audio;

namespace AudIOQP.UI;

internal sealed class DevicePickerForm : Form
{
    const int WsExToolWindow = 0x00000080;

    readonly ListBox _list;
    readonly Label _empty;
    readonly bool _focusOnShow;
    string? _currentId;
    bool _suppress;
    int _hover = -1;

    public event EventHandler<AudioEndpoint>? DeviceChosen;
    public event EventHandler? Dismissed;

    public DevicePickerForm(IReadOnlyList<AudioEndpoint> devices, string? currentId, Screen screen, bool focusOnShow)
    {
        _currentId = currentId;
        _focusOnShow = focusOnShow;

        Text = "AudIOQP";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        Font = new Font("Segoe UI", 10f);
        BackColor = Theme.Border;
        Padding = new Padding(1);
        ClientSize = new Size(440, 390);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(320, 240);

        var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.Background };
        var title = new Label
        {
            Text = "Switch audio output",
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(18, 16)
        };
        var hint = new Label
        {
            Text = "Click a device, or use the arrow keys and Enter",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(20, 44)
        };
        var close = new Button
        {
            Text = "×",
            Font = new Font("Segoe UI", 14f),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Muted,
            BackColor = Theme.Background,
            Size = new Size(36, 32),
            TabStop = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        close.FlatAppearance.BorderSize = 0;
        close.FlatAppearance.MouseOverBackColor = Theme.Hover;
        close.Click += (_, _) => Cancel();
        header.Controls.Add(title);
        header.Controls.Add(hint);
        header.Controls.Add(close);
        header.Resize += (_, _) => close.Location = new Point(header.Width - 44, 12);

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 12, 0),
            Text = "1–9 select    Enter switches    Esc closes"
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            ItemHeight = Math.Max(40, (int)(Font.Height * 2.6))
        };
        EnableDoubleBuffer(_list);
        _list.DrawItem += DrawRow;
        _list.MouseMove += (_, e) =>
        {
            var index = _list.IndexFromPoint(e.Location);
            if (index == _hover)
                return;
            _hover = index;
            _list.Invalidate();
        };
        _list.MouseLeave += (_, _) =>
        {
            _hover = -1;
            _list.Invalidate();
        };
        _list.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;
            var index = _list.IndexFromPoint(e.Location);
            if (index < 0)
                return;
            _list.SelectedIndex = index;
            Confirm();
        };

        _empty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Muted,
            Text = "No whitelisted devices are connected.",
            Visible = false
        };

        root.Controls.Add(_list);
        root.Controls.Add(_empty);
        root.Controls.Add(footer);
        root.Controls.Add(header);
        Controls.Add(root);

        SetDevices(devices, currentId);
        ScreenPlacement.Attach(this, screen);
        Shown += (_, _) =>
        {
            if (!_focusOnShow)
                return;
            Foreground.Activate(this);
            _list.Focus();
        };
    }

    protected override bool ShowWithoutActivation => !_focusOnShow;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    public void SetDevices(IReadOnlyList<AudioEndpoint> devices, string? currentId)
    {
        _currentId = currentId;
        var selectedId = (_list.SelectedItem as AudioEndpoint)?.Id;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var device in devices)
            _list.Items.Add(device);

        var restore = IndexOf(selectedId);
        if (restore < 0)
            restore = IndexOf(currentId);
        if (restore < 0 && _list.Items.Count > 0)
            restore = 0;
        if (restore >= 0)
            _list.SelectedIndex = restore;
        _list.EndUpdate();
        var empty = _list.Items.Count == 0;
        _list.Visible = !empty;
        _empty.Visible = empty;
    }

    public void Dismiss()
    {
        _suppress = true;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Cancel();
            return true;
        }

        if (keyData == Keys.Enter)
        {
            Confirm();
            return true;
        }

        var code = keyData & Keys.KeyCode;
        var number = code switch
        {
            >= Keys.D1 and <= Keys.D9 => code - Keys.D1,
            >= Keys.NumPad1 and <= Keys.NumPad9 => code - Keys.NumPad1,
            _ => -1
        };
        if (number >= 0 && number < _list.Items.Count && (keyData & Keys.Modifiers) == 0)
        {
            _list.SelectedIndex = number;
            Confirm();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    void Confirm()
    {
        if (_suppress || _list.SelectedItem is not AudioEndpoint device)
            return;
        DeviceChosen?.Invoke(this, device);
    }

    void Cancel()
    {
        if (_suppress)
            return;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    int IndexOf(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return -1;
        for (var i = 0; i < _list.Items.Count; i++)
        {
            if (_list.Items[i] is AudioEndpoint device && DeviceSelector.IdEquals(device.Id, id))
                return i;
        }

        return -1;
    }

    void DrawRow(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _list.Items[e.Index] is not AudioEndpoint device)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var hover = e.Index == _hover && !selected;
        var back = selected ? Theme.Selection : hover ? Theme.Hover : Theme.Background;
        var fore = selected ? Theme.SelectionText : Theme.Text;
        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, e.Bounds);

        var nameRect = new Rectangle(e.Bounds.X + 36, e.Bounds.Y, e.Bounds.Width - 120, e.Bounds.Height);
        if (e.Index < 9)
        {
            var numberRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, 24, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.Index + 1).ToString(), Font, numberRect,
                selected ? Theme.SelectionText : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        TextRenderer.DrawText(e.Graphics, device.FriendlyName, Font, nameRect, fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (DeviceSelector.IdEquals(device.Id, _currentId))
        {
            var badge = "Current";
            var badgeRect = new Rectangle(e.Bounds.Right - 84, e.Bounds.Y, 72, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, badge, Font, badgeRect,
                selected ? Theme.SelectionText : Theme.Accent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
    }

    static void EnableDoubleBuffer(Control control)
    {
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(control, true);
    }
}
