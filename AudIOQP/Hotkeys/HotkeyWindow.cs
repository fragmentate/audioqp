using System.Runtime.InteropServices;

namespace AudIOQP.Hotkeys;

/// <summary>
/// Message-only window that receives WM_HOTKEY. RegisterHotKey works system-wide,
/// including most full-screen apps, because the shell delivers it outside the target process.
/// </summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WmHotkey = 0x0312;
    const int HwndMessage = -3;

    public event EventHandler<int>? HotkeyPressed;

    public void Create()
    {
        CreateHandle(new CreateParams
        {
            Caption = "AudIOQP.Hotkeys",
            Parent = new IntPtr(HwndMessage)
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
            HotkeyPressed?.Invoke(this, m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}

internal static class NativeHotkey
{
    const uint ModNorepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool TryRegister(IntPtr hwnd, int id, uint modifiers, uint virtualKey, out int error)
    {
        Unregister(hwnd, id);
        if (RegisterHotKey(hwnd, id, modifiers | ModNorepeat, virtualKey))
        {
            error = 0;
            return true;
        }

        var first = Marshal.GetLastWin32Error();
        if (RegisterHotKey(hwnd, id, modifiers, virtualKey))
        {
            error = 0;
            return true;
        }

        error = Marshal.GetLastWin32Error();
        if (error == 0)
            error = first;
        return false;
    }

    public static void Unregister(IntPtr hwnd, int id)
    {
        UnregisterHotKey(hwnd, id);
    }
}
