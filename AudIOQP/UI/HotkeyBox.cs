using System.Runtime.InteropServices;
using AudIOQP.Settings;

namespace AudIOQP.UI;

internal sealed class HotkeyBox : TextBox
{
    public Hotkey? Hotkey { get; private set; }

    public event EventHandler? HotkeyChanged;

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        BorderStyle = BorderStyle.FixedSingle;
    }

    public void SetHotkey(Hotkey hotkey)
    {
        Hotkey = hotkey.Clone();
        Text = Hotkey.Display;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        var modifiers = keyData & Keys.Modifiers;
        if (code == Keys.Escape && modifiers == 0)
            return false;
        if (code is Keys.Tab or Keys.Escape)
            return base.ProcessDialogKey(keyData);

        ApplyKey(keyData);
        return true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        SelectionStart = TextLength;
        SelectionLength = 0;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        ApplyKey(e.KeyData);
    }

    void ApplyKey(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        if (code is Keys.Back or Keys.Delete && (keyData & Keys.Modifiers) == 0)
        {
            Hotkey = null;
            Text = "";
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsBareModifier(code))
        {
            Text = ModifierPrefix(keyData) + "…";
            return;
        }

        var control = (keyData & Keys.Control) != 0;
        var alt = (keyData & Keys.Alt) != 0;
        var shift = (keyData & Keys.Shift) != 0;
        var win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        var virtualKey = (int)code;
        if (virtualKey == 0x03)
            virtualKey = 0x13;

        if (!control && !alt && !shift && !win)
        {
            Text = "Add Ctrl, Alt, Shift, or Win";
            return;
        }

        if (HotkeyNames.IsModifier(virtualKey) || virtualKey == 0)
            return;

        Hotkey = new Hotkey
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            VirtualKey = virtualKey
        };
        Text = Hotkey.Display;
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    static bool IsBareModifier(Keys code) => code is
        Keys.Control or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Shift or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.Alt or Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.LWin or Keys.RWin;

    static string ModifierPrefix(Keys keyData)
    {
        var parts = new List<string>();
        if ((keyData & Keys.Control) != 0) parts.Add("Ctrl");
        if ((keyData & Keys.Alt) != 0) parts.Add("Alt");
        if ((keyData & Keys.Shift) != 0) parts.Add("Shift");
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
            parts.Add("Win");
        return parts.Count == 0 ? "" : string.Join("+", parts) + "+";
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int virtualKey);
}
