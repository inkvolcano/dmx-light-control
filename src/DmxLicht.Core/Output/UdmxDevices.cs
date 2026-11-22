using System.Runtime.InteropServices;
using System.Text;

namespace DmxLicht.Core.Output;

/// <summary>Finds uDMX dongles that Windows (or Linux) currently sees (whatever driver they have).</summary>
public static class UdmxDevices
{
    const string VidPid = "VID_16C0&PID_05DC";
    static readonly IntPtr INVALID = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA d);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA d, StringBuilder id, int size, out int required);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    /// <summary>Device instance ids like USB\VID_16C0&amp;PID_05DC\5&amp;1234... of plugged-in uDMX dongles.</summary>
    public static List<string> InstanceIds()
    {
        if (!OperatingSystem.IsWindows()) return SysfsIds();
        var ids = new List<string>();
        IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, "USB", IntPtr.Zero, 0x06); // ALLCLASSES | PRESENT
        if (set == INVALID) return ids;
        try
        {
            for (uint i = 0; ; i++)
            {
                var d = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                if (!SetupDiEnumDeviceInfo(set, i, ref d)) break;
                var sb = new StringBuilder(512);
                if (SetupDiGetDeviceInstanceIdW(set, ref d, sb, sb.Capacity, out _) &&
                    sb.ToString().Contains(VidPid, StringComparison.OrdinalIgnoreCase))
                    ids.Add(sb.ToString());
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return ids;
    }

    /// <summary>Linux: USB devices in /sys/bus/usb/devices with the uDMX's vendor and product id (e.g. "1-1.3").</summary>
    static List<string> SysfsIds()
    {
        var ids = new List<string>();
        try
        {
            foreach (string d in Directory.GetDirectories("/sys/bus/usb/devices"))
            {
                string vid = Path.Combine(d, "idVendor"), pid = Path.Combine(d, "idProduct");
                if (File.Exists(vid) && File.Exists(pid)
                    && File.ReadAllText(vid).Trim().Equals("16c0", StringComparison.OrdinalIgnoreCase)
                    && File.ReadAllText(pid).Trim().Equals("05dc", StringComparison.OrdinalIgnoreCase))
                    ids.Add(Path.GetFileName(d));
            }
        }
        catch { }
        return ids;
    }
}
