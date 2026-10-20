using System.Runtime.InteropServices;

namespace AudIOQP.UI;

internal static class ScreenPlacement
{
    public static Screen ScreenAtCursor() => Screen.FromPoint(Cursor.Position);

    public static IReadOnlyList<Screen> TargetScreens(bool allScreens)
    {
        var cursor = ScreenAtCursor();
        if (!allScreens)
            return [cursor];

        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return [cursor];

        // Show the cursor's monitor last so it keeps activation.
        return screens
            .OrderBy(s => string.Equals(s.DeviceName, cursor.DeviceName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToArray();
    }

    /// <summary>
    /// Parks the form on the target monitor before its handle is created, then recenters
    /// after show and after a per-monitor DPI change.
    /// </summary>
    public static void Attach(Form form, Screen screen)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = screen.WorkingArea.Location;
        form.Load += (_, _) => Center(form, screen);
        form.Shown += (_, _) => Center(form, screen);
        form.DpiChanged += (_, _) => Center(form, screen);
    }

    public static void Center(Form form, Screen screen)
    {
        if (form.IsDisposed)
            return;
        var area = screen.WorkingArea;
        var width = Math.Min(form.Width, Math.Max(1, area.Width));
        var height = Math.Min(form.Height, Math.Max(1, area.Height));
        var x = area.Left + Math.Max(0, (area.Width - width) / 2);
        var y = area.Top + Math.Max(0, (area.Height - height) / 2);
        if (form.Left == x && form.Top == y && form.Width == width && form.Height == height)
            return;
        form.Bounds = new Rectangle(x, y, width, height);
    }
}

internal static class Foreground
{
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    static readonly IntPtr HwndTopMost = new(-1);
    const uint SwpNoMove = 0x0002;
    const uint SwpNoSize = 0x0001;
    const uint SwpShowWindow = 0x0040;

    public static void Activate(Form form)
    {
        if (form.IsDisposed)
            return;
        var hwnd = form.Handle;
        SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var thisThread = GetCurrentThreadId();
        if (foregroundThread != 0 && foregroundThread != thisThread)
        {
            AttachThreadInput(foregroundThread, thisThread, true);
            SetForegroundWindow(hwnd);
            AttachThreadInput(foregroundThread, thisThread, false);
        }
        else
        {
            SetForegroundWindow(hwnd);
        }

        form.Activate();
        form.Focus();
    }
}
