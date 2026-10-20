using System.Runtime.InteropServices;

namespace AudIOQP.Audio.Native;

/// <summary>
/// Undocumented IPolicyConfig. SetDefaultEndpoint is the 11th method (vtable slot 10)
/// on the Windows 7 through Windows 11 interface. The first ten slots are reserved
/// so the call lands on the right method. Early Windows 10 builds used different
/// interface ids with the same slot, and are tried only when QueryInterface fails.
/// </summary>
internal static class PolicyConfig
{
    public static void SetDefaultEndpoint(string deviceId, IReadOnlyCollection<ERole> roles)
    {
        var client = new PolicyConfigClient();
        try
        {
            if (client is IPolicyConfig current)
            {
                Set(current.SetDefaultEndpoint, deviceId, roles);
                return;
            }

            if (client is IPolicyConfigTh2 th2)
            {
                Set(th2.SetDefaultEndpoint, deviceId, roles);
                return;
            }

            if (client is IPolicyConfigTh1 th1)
            {
                Set(th1.SetDefaultEndpoint, deviceId, roles);
                return;
            }

            throw new InvalidOperationException("Windows audio policy configuration is unavailable.");
        }
        finally
        {
            ComRelease.Release(client);
        }
    }

    static void Set(Func<string, ERole, int> setDefault, string deviceId, IReadOnlyCollection<ERole> roles)
    {
        foreach (var role in roles)
        {
            var hr = setDefault(deviceId, role);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }
    }
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient
{
}

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    void Reserved1();
    void Reserved2();
    void Reserved3();
    void Reserved4();
    void Reserved5();
    void Reserved6();
    void Reserved7();
    void Reserved8();
    void Reserved9();
    void Reserved10();

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

    void Reserved12();
}

[ComImport]
[Guid("6BE54BE8-A068-4875-A49D-0C2966473B11")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigTh2
{
    void Reserved1();
    void Reserved2();
    void Reserved3();
    void Reserved4();
    void Reserved5();
    void Reserved6();
    void Reserved7();
    void Reserved8();
    void Reserved9();
    void Reserved10();

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

    void Reserved12();
}

[ComImport]
[Guid("CA286FC3-91FD-42C3-8E9B-CAAFA66242E3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigTh1
{
    void Reserved1();
    void Reserved2();
    void Reserved3();
    void Reserved4();
    void Reserved5();
    void Reserved6();
    void Reserved7();
    void Reserved8();
    void Reserved9();
    void Reserved10();

    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);

    void Reserved12();
}
