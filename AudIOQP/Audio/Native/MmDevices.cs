using System.Runtime.InteropServices;

namespace AudIOQP.Audio.Native;

internal enum EDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2
}

internal enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint Pid;

    public PropertyKey(Guid formatId, uint pid)
    {
        FormatId = formatId;
        Pid = pid;
    }
}

/// <summary>
/// Sized larger than the native PROPVARIANT so the COM marshaller does not truncate the write.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort Vt;
    [FieldOffset(8)] public IntPtr Pointer;
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);

    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(IMMNotificationClient client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    [PreserveSig]
    int OpenPropertyStore(uint access, out IPropertyStore store);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out uint state);
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetAt(uint index, out PropertyKey key);

    [PreserveSig]
    int GetValue(ref PropertyKey key, out PropVariant value);

    [PreserveSig]
    int SetValue(ref PropertyKey key, ref PropVariant value);

    [PreserveSig]
    int Commit();
}

[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);

    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);

    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class MmNotificationClient : IMMNotificationClient
{
    readonly Action _changed;

    public MmNotificationClient(Action changed) => _changed = changed;

    public void OnDeviceStateChanged(string deviceId, uint newState) => Signal();

    public void OnDeviceAdded(string deviceId) => Signal();

    public void OnDeviceRemoved(string deviceId) => Signal();

    public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId) => Signal();

    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => Signal();

    void Signal()
    {
        try
        {
            _changed();
        }
        catch
        {
            // Callbacks arrive on a WASAPI thread. Never let them tear down the process.
        }
    }
}

internal static class ComRelease
{
    public static void Release(object? com)
    {
        if (com != null && Marshal.IsComObject(com))
        {
            try
            {
                Marshal.ReleaseComObject(com);
            }
            catch
            {
                // Already released.
            }
        }
    }
}

internal static class PropertyStoreReader
{
    public static readonly PropertyKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    public static readonly PropertyKey DeviceDesc = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
    public static readonly PropertyKey InterfaceFriendlyName = new(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);

    const uint StgmRead = 0;
    const ushort VtLpWstr = 31;
    const ushort VtBstr = 8;

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant pvar);

    public static string ReadName(IMMDevice device, string fallbackId)
    {
        var hr = device.OpenPropertyStore(StgmRead, out var store);
        if (hr < 0 || store == null)
            return fallbackId;

        try
        {
            var name = ReadString(store, FriendlyName)
                ?? ReadString(store, InterfaceFriendlyName)
                ?? ReadString(store, DeviceDesc);
            return string.IsNullOrWhiteSpace(name) ? fallbackId : name.Trim();
        }
        finally
        {
            ComRelease.Release(store);
        }
    }

    static string? ReadString(IPropertyStore store, PropertyKey key)
    {
        var hr = store.GetValue(ref key, out var value);
        if (hr < 0)
            return null;
        try
        {
            if (value.Pointer == IntPtr.Zero)
                return null;
            if (value.Vt == VtLpWstr)
                return Marshal.PtrToStringUni(value.Pointer);
            if (value.Vt == VtBstr)
                return Marshal.PtrToStringBSTR(value.Pointer);
            return null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }
}
