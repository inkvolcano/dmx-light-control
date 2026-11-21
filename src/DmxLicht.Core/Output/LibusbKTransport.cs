using System.Runtime.InteropServices;

namespace DmxLicht.Core.Output;

/// <summary>
/// uDMX through libusbK.dll, which the colleague's driver package (uDMX_Driver_libUSBK) installs.
/// libusbK.dll can talk to devices on the libusbK, libusb0 and WinUSB drivers alike.
/// Not tested against hardware yet; it is only used when the WinUSB route reports a missing driver.
/// </summary>
public sealed class LibusbKTransport : IUdmxTransport, IResettableTransport, ICancellableTransport
{
    const int Vid = 0x16C0, Pid = 0x05DC;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SetupPacket { public byte RequestType, Request; public ushort Value, Index, Length; }

    [DllImport("libusbK.dll", SetLastError = true)] static extern bool LstK_Init(out IntPtr list, int flags);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool LstK_FindByVidPid(IntPtr list, int vid, int pid, out IntPtr devInfo);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool LstK_Free(IntPtr list);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool UsbK_Init(out IntPtr handle, IntPtr devInfo);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool UsbK_Free(IntPtr handle);
    [DllImport("libusbK.dll", SetLastError = true)]
    static extern bool UsbK_ControlTransfer(IntPtr handle, SetupPacket setup, byte[] buffer, uint length, out uint transferred, IntPtr overlapped);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool UsbK_ResetDevice(IntPtr handle);
    [DllImport("libusbK.dll", SetLastError = true)] static extern bool UsbK_AbortPipe(IntPtr handle, byte pipeId);
    [DllImport("libusbK.dll", SetLastError = true)]
    static extern bool UsbK_SetPowerPolicy(IntPtr handle, uint policyType, uint valueLength, ref byte value);
    [DllImport("libusbK.dll", SetLastError = true)]
    static extern bool UsbK_SetPipePolicy(IntPtr handle, byte pipeId, uint policyType, uint valueLength, ref uint value);

    IntPtr list, handle;

    public static bool Available => NativeLibrary.TryLoad("libusbK.dll", out IntPtr h) && Free(h);
    static bool Free(IntPtr h) { NativeLibrary.Free(h); return true; }

    public void Open()
    {
        try
        {
            if (!LstK_Init(out list, 0)) throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "libusbK: apparaten opvragen mislukt");
            if (!LstK_FindByVidPid(list, Vid, Pid, out IntPtr info))
                throw new UdmxException(UdmxFault.NotFound, "uDMX niet gevonden (libusbK)");
            if (!UsbK_Init(out handle, info))
            {
                int e = Marshal.GetLastWin32Error();
                if (e == 5 || e == 32) throw new UdmxException(UdmxFault.InUse, "uDMX is in gebruik door een ander programma (libusbK, code " + e + ")", e);
                throw UdmxException.FromWin32(e, "libusbK: uDMX openen mislukt");
            }
            uint timeout = 500;
            UsbK_SetPipePolicy(handle, 0x00, 3, 4, ref timeout); // PIPE_TRANSFER_TIMEOUT
            byte noSuspend = 0;
            try { UsbK_SetPowerPolicy(handle, 0x81, 1, ref noSuspend); } catch (EntryPointNotFoundException) { } // AUTO_SUSPEND off
        }
        catch (DllNotFoundException) { Dispose(); throw new UdmxException(UdmxFault.DriverMissing, "libusbK.dll ontbreekt"); }
        catch (EntryPointNotFoundException ex) { Dispose(); throw new UdmxException(UdmxFault.DriverMissing, "libusbK.dll onbekende versie: " + ex.Message); }
        catch { Dispose(); throw; }
    }

    public void SetRange(int start, byte[] values, int offset, int count)
    {
        var buf = new byte[count];
        Array.Copy(values, offset, buf, 0, count);
        var sp = new SetupPacket { RequestType = 0x40, Request = 2, Value = (ushort)count, Index = (ushort)start, Length = (ushort)count };
        if (!UsbK_ControlTransfer(handle, sp, buf, (uint)count, out _, IntPtr.Zero))
            throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "Versturen mislukt (libusbK)");
    }

    public void SetSingle(int channel, byte value)
    {
        var sp = new SetupPacket { RequestType = 0x40, Request = 1, Value = value, Index = (ushort)channel, Length = 0 };
        if (!UsbK_ControlTransfer(handle, sp, null, 0, out _, IntPtr.Zero))
            throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "Versturen mislukt (libusbK)");
    }

    public void CancelPending()
    {
        try { if (handle != IntPtr.Zero) UsbK_AbortPipe(handle, 0x00); } catch (EntryPointNotFoundException) { }
    }

    /// <summary>USB port reset: the dongle re-enumerates as if replugged.</summary>
    public bool ResetDevice()
    {
        if (handle == IntPtr.Zero) return false;
        if (!UsbK_ResetDevice(handle))
            throw UdmxException.FromWin32(Marshal.GetLastWin32Error(), "libusbK: USB-reset mislukt");
        return true;
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero) { try { UsbK_Free(handle); } catch { } handle = IntPtr.Zero; }
        if (list != IntPtr.Zero) { try { LstK_Free(list); } catch { } list = IntPtr.Zero; }
    }
}

/// <summary>WinUSB first (proven); if the driver is not WinUSB, libusbK.dll when it is installed.</summary>
public sealed class AutoUdmxTransport : IUdmxTransport, IResettableTransport, ICancellableTransport
{
    public void CancelPending() { if (inner is ICancellableTransport c) c.CancelPending(); }

    public bool ResetDevice() => inner is IResettableTransport r && r.ResetDevice();

    IUdmxTransport inner;
    public string Driver { get; private set; }

    public void Open()
    {
        var win = new WinUsbTransport();
        try { win.Open(); inner = win; Driver = "WinUSB"; return; }
        catch (UdmxException e) when (e.Fault == UdmxFault.DriverMissing && LibusbKTransport.Available)
        {
            win.Dispose();
            var k = new LibusbKTransport();
            k.Open();
            inner = k;
            Driver = "libusbK";
            Log.Write("uDMX via libusbK geopend");
        }
    }

    public void SetRange(int start, byte[] values, int offset, int count) => inner.SetRange(start, values, offset, count);
    public void SetSingle(int channel, byte value) => inner.SetSingle(channel, value);
    public void Dispose() => inner?.Dispose();
}
