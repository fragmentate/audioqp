using AudIOQP.Settings;

namespace AudIOQP.Hotkeys;

internal sealed class HotkeyRegistrar
{
    public const int ModalId = 1;
    public const int CycleId = 2;
    public const int HomeId = 3;

    readonly IntPtr _hwnd;
    AppSettings _active = new();

    public HotkeyRegistrar(IntPtr hwnd) => _hwnd = hwnd;

    public bool TryApply(AppSettings settings, out string? error)
    {
        var previous = _active.Clone();
        UnregisterAll();
        if (!Register(settings, out error))
        {
            UnregisterAll();
            Register(previous, out _);
            return false;
        }

        _active = settings.Clone();
        error = null;
        return true;
    }

    public void UnregisterAll()
    {
        NativeHotkey.Unregister(_hwnd, ModalId);
        NativeHotkey.Unregister(_hwnd, CycleId);
        NativeHotkey.Unregister(_hwnd, HomeId);
    }

    bool Register(AppSettings settings, out string? error)
    {
        if (!RegisterOne(ModalId, settings.OpenModal, out error))
            return false;
        if (!RegisterOne(CycleId, settings.Cycle, out error))
            return false;
        if (!RegisterOne(HomeId, settings.Home, out error))
            return false;
        error = null;
        return true;
    }

    bool RegisterOne(int id, Hotkey hotkey, out string? error)
    {
        if (!hotkey.IsValid)
        {
            error = $"The shortcut {hotkey.Display} is incomplete.";
            return false;
        }

        if (NativeHotkey.TryRegister(_hwnd, id, hotkey.ModifierFlags, hotkey.RegisterVirtualKey, out var win32))
        {
            error = null;
            return true;
        }

        error = $"{hotkey.Display} is already in use by another program (Windows error {win32}).";
        return false;
    }
}
