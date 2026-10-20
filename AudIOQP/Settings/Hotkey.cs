using System.Text.Json.Serialization;

namespace AudIOQP.Settings;

public sealed class Hotkey
{
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }

    [JsonIgnore]
    public int VirtualKey { get; set; }

    /// <summary>
    /// Display name of the non-modifier key. This is the persisted form.
    /// </summary>
    public string Key
    {
        get => HotkeyNames.FromVirtualKey(VirtualKey);
        set => VirtualKey = HotkeyNames.ToVirtualKey(value);
    }

    [JsonIgnore]
    public bool HasModifier => Control || Alt || Shift || Win;

    [JsonIgnore]
    public bool IsValid => HasModifier && VirtualKey != 0 && !HotkeyNames.IsModifier(VirtualKey);

    /// <summary>
    /// RegisterHotKey modifier flags, without MOD_NOREPEAT.
    /// </summary>
    [JsonIgnore]
    public uint ModifierFlags =>
        (Control ? 0x0002u : 0) |
        (Alt ? 0x0001u : 0) |
        (Shift ? 0x0004u : 0) |
        (Win ? 0x0008u : 0);

    /// <summary>
    /// Virtual key passed to RegisterHotKey.
    /// Ctrl+Pause is delivered by Windows as Ctrl+Break (VK_CANCEL).
    /// </summary>
    [JsonIgnore]
    public uint RegisterVirtualKey =>
        VirtualKey == 0x13 && Control ? 0x03u : unchecked((uint)VirtualKey);

    [JsonIgnore]
    public string Display
    {
        get
        {
            var parts = new List<string>(5);
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(HotkeyNames.FromVirtualKey(VirtualKey));
            return string.Join("+", parts);
        }
    }

    public bool ConflictsWith(Hotkey other) =>
        IsValid && other.IsValid &&
        ModifierFlags == other.ModifierFlags &&
        RegisterVirtualKey == other.RegisterVirtualKey;

    public Hotkey Clone() => new()
    {
        Control = Control,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        VirtualKey = VirtualKey
    };

    public static Hotkey ModalDefault() => new() { Control = true, VirtualKey = 0x13 };

    public static Hotkey CycleDefault() => new() { Control = true, Shift = true, VirtualKey = 0xDD };

    public static Hotkey HomeDefault() => new() { Control = true, Shift = true, VirtualKey = 0x24 };

    public override string ToString() => Display;
}

public static class HotkeyNames
{
    static readonly Dictionary<string, int> ToVk = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<int, string> ToName = new();

    static HotkeyNames()
    {
        Add("Pause", 0x13);
        Add("Break", 0x03, preferred: true);
        Add("Backspace", 0x08);
        Add("Tab", 0x09);
        Add("Enter", 0x0D);
        Add("Escape", 0x1B);
        Add("Space", 0x20);
        Add("PageUp", 0x21);
        Add("PageDown", 0x22);
        Add("End", 0x23);
        Add("Home", 0x24);
        Add("Left", 0x25);
        Add("Up", 0x26);
        Add("Right", 0x27);
        Add("Down", 0x28);
        Add("Insert", 0x2D);
        Add("Delete", 0x2E);
        Add("PrintScreen", 0x2C);
        for (var i = 0; i <= 9; i++)
            Add(i.ToString(), 0x30 + i);
        for (var i = 0; i < 26; i++)
            Add(((char)('A' + i)).ToString(), 0x41 + i);
        Add(";", 0xBA);
        Add("=", 0xBB);
        Add(",", 0xBC);
        Add("-", 0xBD);
        Add(".", 0xBE);
        Add("/", 0xBF);
        Add("`", 0xC0);
        Add("[", 0xDB);
        Add("\\", 0xDC);
        Add("]", 0xDD);
        Add("'", 0xDE);
        Add("Oem6", 0xDD, preferred: false);
        Add("OemCloseBrackets", 0xDD, preferred: false);
        for (var i = 1; i <= 24; i++)
            Add("F" + i, 0x6F + i);
        for (var i = 0; i <= 9; i++)
            Add("NumPad" + i, 0x60 + i);
        Add("Multiply", 0x6A);
        Add("Add", 0x6B);
        Add("Subtract", 0x6D);
        Add("Decimal", 0x6E);
        Add("Divide", 0x6F);
    }

    static void Add(string name, int virtualKey, bool preferred = true)
    {
        ToVk[name] = virtualKey;
        if (preferred)
            ToName[virtualKey] = name;
    }

    public static bool IsModifier(int virtualKey) => virtualKey is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    public static int ToVirtualKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;
        return ToVk.TryGetValue(name.Trim(), out var vk) ? vk : 0;
    }

    public static string FromVirtualKey(int virtualKey)
    {
        if (virtualKey == 0)
            return "";
        return ToName.TryGetValue(virtualKey, out var name) ? name : "Key" + virtualKey.ToString("X2");
    }
}
