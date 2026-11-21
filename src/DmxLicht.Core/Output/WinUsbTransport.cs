using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace DmxLicht.Core.Output;

// uDMX (Anyma, VID 16C0 / PID 05DC) via WinUSB. Needs the WinUSB driver (install with Zadig).
// Device lookup, open and the USB commands are v1.4's proven code; only the error reporting
// (UdmxException with a fault kind) and the transfer timeout are new. The sending strategy lives in UdmxOutput.
public sealed class WinUsbTransport : IUdmxTransport, ICancellableTransport
{
    const string VidPid = "VID_16C0&PID_05DC";
    static readonly IntPtr INVALID = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct WINUSB_SETUP_PACKET
    {
        public byte RequestType; public byte Request;
        public ushort Value; public ushort Index; public ushort Length;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public uint cbSize; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA d);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref SP_DEVINFO_DATA d, uint prop, out uint regType, byte[] buf, uint size, out uint required);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref SP_DEVINFO_DATA d, uint scope, uint profile, uint keyType, int sam);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid g, uint index, ref SP_DEVICE_INTERFACE_DATA d);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, uint size, out uint required, IntPtr devInfo);
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);

    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_Initialize(IntPtr deviceHandle, out IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_Free(IntPtr interfaceHandle);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_ControlTransfer(IntPtr interfaceHandle, WINUSB_SETUP_PACKET setup, byte[] buffer, uint bufferLength, out uint transferred, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_SetPipePolicy(IntPtr interfaceHandle, byte pipeId, uint policyType, uint valueLength, ref uint value);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CancelIoEx(IntPtr file, IntPtr overlapped);
    [DllImport("winusb.dll", SetLastError = true)]
    static extern bool WinUsb_SetPowerPolicy(IntPtr interfaceHandle, uint policyType, uint valueLength, ref byte value);

    IntPtr fileHandle = INVALID;
    IntPtr usbHandle = IntPtr.Zero;

    static string FindPath()
    {
        IntPtr set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 0x06); // ALLCLASSES | PRESENT
        if (set == INVALID) throw new UdmxException(UdmxFault.Other, "Kan USB-apparaten niet opvragen.");
        Guid guid = Guid.Empty;
        bool found = false, haveGuid = false;
        try
        {
            for (uint i = 0; ; i++)
            {
                SP_DEVINFO_DATA d = new SP_DEVINFO_DATA();
                d.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                if (!SetupDiEnumDeviceInfo(set, i, ref d)) break;
                byte[] buf = new byte[2048]; uint rt, req;
                if (!SetupDiGetDeviceRegistryProperty(set, ref d, 1, out rt, buf, (uint)buf.Length, out req)) continue;
                string hw = Encoding.Unicode.GetString(buf, 0, (int)Math.Min(req, (uint)buf.Length)).ToUpperInvariant();
                if (!hw.Contains(VidPid)) continue;
                found = true;
                IntPtr hk = SetupDiOpenDevRegKey(set, ref d, 1, 0, 1, 0x20019);
                if (hk == INVALID || hk == IntPtr.Zero) continue;
                using (RegistryKey rk = RegistryKey.FromHandle(new SafeRegistryHandle(hk, true)))
                {
                    object v = rk.GetValue("DeviceInterfaceGUIDs");
                    if (v == null) v = rk.GetValue("DeviceInterfaceGUID");
                    string g = null;
                    string[] arr = v as string[];
                    if (arr != null && arr.Length > 0) g = arr[0];
                    else if (v is string) g = (string)v;
                    if (g != null) { guid = new Guid(g.Trim()); haveGuid = true; }
                }
                if (haveGuid) break;
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }

        if (!found) throw new UdmxException(UdmxFault.NotFound, "uDMX niet gevonden - steek de dongle in de USB-poort.");
        if (!haveGuid) throw new UdmxException(UdmxFault.DriverMissing, "uDMX gevonden, maar de WinUSB-driver ontbreekt (Zadig).");

        IntPtr set2 = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
        if (set2 == INVALID) throw new UdmxException(UdmxFault.DriverMissing, "Kan uDMX-interface niet openen.");
        try
        {
            SP_DEVICE_INTERFACE_DATA ifd = new SP_DEVICE_INTERFACE_DATA();
            ifd.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            if (!SetupDiEnumDeviceInterfaces(set2, IntPtr.Zero, ref guid, 0, ref ifd))
                throw new UdmxException(UdmxFault.NotFound, "uDMX-interface niet gevonden.");
            uint need;
            SetupDiGetDeviceInterfaceDetail(set2, ref ifd, IntPtr.Zero, 0, out need, IntPtr.Zero);
            IntPtr det = Marshal.AllocHGlobal((int)need);
            try
            {
                Marshal.WriteInt32(det, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set2, ref ifd, det, need, out need, IntPtr.Zero))
                    throw new UdmxException(UdmxFault.Other, "Kan het pad van de uDMX niet bepalen.");
                return Marshal.PtrToStringAuto(new IntPtr(det.ToInt64() + 4));
            }
            finally { Marshal.FreeHGlobal(det); }
        }
        finally { SetupDiDestroyDeviceInfoList(set2); }
    }

    public void Open()
    {
        string path = FindPath();
        fileHandle = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000080, IntPtr.Zero);
        if (fileHandle == INVALID)
        {
            int e = Marshal.GetLastWin32Error();
            if (e == 5 || e == 32) throw new UdmxException(UdmxFault.InUse, "uDMX is in gebruik door een ander programma (code " + e + "). Sluit v1.4, QLC+ of een tweede DmxLicht.", e);
            throw UdmxException.FromWin32(e, "Kan uDMX niet openen");
        }
        if (!WinUsb_Initialize(fileHandle, out usbHandle))
        {
            int e = Marshal.GetLastWin32Error();
            CloseHandle(fileHandle); fileHandle = INVALID;
            throw new UdmxException(UdmxFault.DriverMissing, "WinUSB start niet - is de WinUSB-driver geinstalleerd (Zadig)? (code " + e + ")", e);
        }
        try
        {
            uint timeout = 500; // ms: een vastgelopen opdracht geeft snel een fout in plaats van de verzending te blokkeren
            if (!WinUsb_SetPipePolicy(usbHandle, 0x00, 3, 4, ref timeout))
                Log.Write("uDMX: time-out instellen mislukt (code " + Marshal.GetLastWin32Error() + ")");
            byte noSuspend = 0;  // USB-stroombesparing voor de dongle uit
            if (!WinUsb_SetPowerPolicy(usbHandle, 0x81, 1, ref noSuspend))
                Log.Write("uDMX: stroombesparing uitzetten mislukt (code " + Marshal.GetLastWin32Error() + ")");
        }
        catch { }
    }

    /// <summary>Breaks off a transfer that hangs (watchdog); it then fails with code 995.</summary>
    public void CancelPending()
    {
        if (fileHandle != INVALID) CancelIoEx(fileHandle, IntPtr.Zero);
    }

    public void SetRange(int start, byte[] values, int offset, int count)
    {
        WINUSB_SETUP_PACKET sp = new WINUSB_SETUP_PACKET();
        sp.RequestType = 0x40; sp.Request = 2; // cmd_SetChannelRange
        sp.Value = (ushort)count; sp.Index = (ushort)start; sp.Length = (ushort)count;
        byte[] buf = new byte[count];
        Array.Copy(values, offset, buf, 0, count);
        uint tr;
        if (usbHandle == IntPtr.Zero) throw new UdmxException(UdmxFault.Gone, "uDMX is niet open");
        if (!WinUsb_ControlTransfer(usbHandle, sp, buf, (uint)count, out tr, IntPtr.Zero))
            throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "Versturen mislukt");
    }

    public void SetSingle(int channel, byte value)
    {
        WINUSB_SETUP_PACKET sp = new WINUSB_SETUP_PACKET();
        sp.RequestType = 0x40; sp.Request = 1; // cmd_SetSingleChannel
        sp.Value = value; sp.Index = (ushort)channel; sp.Length = 0;
        uint tr;
        if (usbHandle == IntPtr.Zero) throw new UdmxException(UdmxFault.Gone, "uDMX is niet open");
        if (!WinUsb_ControlTransfer(usbHandle, sp, null, 0, out tr, IntPtr.Zero))
            throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "Versturen mislukt");
    }

    public void Dispose() => Close();

    public void Close()
    {
        if (usbHandle != IntPtr.Zero) { try { WinUsb_Free(usbHandle); } catch { } usbHandle = IntPtr.Zero; }
        if (fileHandle != INVALID) { try { CloseHandle(fileHandle); } catch { } fileHandle = INVALID; }
    }
}
