using System.Runtime.InteropServices;

namespace DmxLicht.Core.Output;

/// <summary>
/// uDMX on Linux (Raspberry Pi) through libusb-1.0 (package libusb-1.0-0, on Raspberry Pi OS by default).
/// Without root the dongle needs a udev rule (60-udmx.rules in the Linux package) that gives the plugdev group access.
/// Same uDMX commands as the Windows transports: vendor control transfers on endpoint 0.
/// </summary>
public sealed class LibusbTransport : IUdmxTransport, IResettableTransport
{
    const int Vid = 0x16C0, Pid = 0x05DC;
    const string Lib = "libusb-1.0.so.0";
    const uint TimeoutMs = 500;

    // libusb error codes
    const int ERROR_IO = -1, ERROR_ACCESS = -3, ERROR_NO_DEVICE = -4, ERROR_NOT_FOUND = -5, ERROR_BUSY = -6,
              ERROR_TIMEOUT = -7, ERROR_PIPE = -9, ERROR_INTERRUPTED = -10;

    [StructLayout(LayoutKind.Sequential)]
    struct DeviceDescriptor
    {
        public byte bLength, bDescriptorType; public ushort bcdUSB;
        public byte bDeviceClass, bDeviceSubClass, bDeviceProtocol, bMaxPacketSize0;
        public ushort idVendor, idProduct, bcdDevice;
        public byte iManufacturer, iProduct, iSerialNumber, bNumConfigurations;
    }

    [DllImport(Lib)] static extern int libusb_init(out IntPtr ctx);
    [DllImport(Lib)] static extern nint libusb_get_device_list(IntPtr ctx, out IntPtr list);
    [DllImport(Lib)] static extern void libusb_free_device_list(IntPtr list, int unrefDevices);
    [DllImport(Lib)] static extern int libusb_get_device_descriptor(IntPtr dev, out DeviceDescriptor desc);
    [DllImport(Lib)] static extern int libusb_open(IntPtr dev, out IntPtr handle);
    [DllImport(Lib)] static extern void libusb_close(IntPtr handle);
    [DllImport(Lib)] static extern int libusb_claim_interface(IntPtr handle, int iface);
    [DllImport(Lib)] static extern int libusb_release_interface(IntPtr handle, int iface);
    [DllImport(Lib)] static extern int libusb_reset_device(IntPtr handle);
    [DllImport(Lib)]
    static extern int libusb_control_transfer(IntPtr handle, byte requestType, byte request, ushort value, ushort index,
        byte[] data, ushort length, uint timeout);
    [DllImport(Lib)] static extern IntPtr libusb_strerror(int code);

    static readonly object initLock = new();
    static IntPtr context;

    IntPtr handle;
    bool claimed;

    static IntPtr Context()
    {
        lock (initLock)
        {
            if (context != IntPtr.Zero) return context;
            int r = libusb_init(out IntPtr ctx);
            if (r < 0) throw new UdmxException(UdmxFault.DriverMissing, "libusb starten mislukt (" + Text(r) + ")");
            context = ctx;   // one context for the life of the app
            return context;
        }
    }

    static string Text(int code)
    {
        try { return Marshal.PtrToStringAnsi(libusb_strerror(code)) + ", code " + code; }
        catch { return "code " + code; }
    }

    /// <summary>Maps a libusb error to a fault.</summary>
    static UdmxException Fail(int code, string what) => new(code switch
    {
        ERROR_NO_DEVICE or ERROR_NOT_FOUND => UdmxFault.Gone,
        ERROR_TIMEOUT or ERROR_INTERRUPTED => UdmxFault.Timeout,
        ERROR_PIPE => UdmxFault.Rejected,
        ERROR_BUSY => UdmxFault.InUse,
        ERROR_ACCESS => UdmxFault.DriverMissing,
        _ => UdmxFault.Other,
    }, what + " (" + Text(code) + ")", code);

    public void Open()
    {
        try
        {
            IntPtr ctx = Context();
            nint n = libusb_get_device_list(ctx, out IntPtr list);
            if (n < 0) throw Fail((int)n, "USB-apparaten opvragen mislukt");
            try
            {
                bool found = false;
                int lastError = 0;
                for (int i = 0; i < n; i++)
                {
                    IntPtr dev = Marshal.ReadIntPtr(list, i * IntPtr.Size);
                    if (libusb_get_device_descriptor(dev, out DeviceDescriptor d) != 0 || d.idVendor != Vid || d.idProduct != Pid) continue;
                    found = true;
                    int r = libusb_open(dev, out IntPtr h);
                    if (r == 0) { handle = h; break; }
                    lastError = r;
                }
                if (!found) throw new UdmxException(UdmxFault.NotFound, "uDMX niet gevonden - steek de dongle in de USB-poort.");
                if (handle == IntPtr.Zero)
                {
                    if (lastError == ERROR_ACCESS)
                        throw new UdmxException(UdmxFault.DriverMissing,
                            "uDMX gevonden, maar geen toegang: de udev-regel ontbreekt (zie LEESMIJ-Linux.txt: 60-udmx.rules) " +
                            "of de gebruiker zit niet in de groep plugdev.", lastError);
                    throw Fail(lastError, "Kan uDMX niet openen");
                }
            }
            finally { libusb_free_device_list(list, 1); }

            // claiming tells a second program (QLC+, another DMX Light Control) that the dongle is taken;
            // control transfers to endpoint 0 also work without it, so only "busy" counts
            int c = libusb_claim_interface(handle, 0);
            if (c == ERROR_BUSY)
                throw new UdmxException(UdmxFault.InUse, "uDMX is in gebruik door een ander programma (QLC+ of een tweede DMX Light Control).", c);
            claimed = c == 0;
        }
        catch (DllNotFoundException)
        {
            Dispose();
            throw new UdmxException(UdmxFault.DriverMissing, "libusb-1.0 ontbreekt: sudo apt install libusb-1.0-0");
        }
        catch (EntryPointNotFoundException ex)
        {
            Dispose();
            throw new UdmxException(UdmxFault.DriverMissing, "libusb-1.0 onbekende versie: " + ex.Message);
        }
        catch { Dispose(); throw; }
    }

    public void SetRange(int start, byte[] values, int offset, int count)
    {
        if (handle == IntPtr.Zero) throw new UdmxException(UdmxFault.Gone, "uDMX is niet open");
        var buf = new byte[count];
        Array.Copy(values, offset, buf, 0, count);
        // vendor request, host to device: cmd_SetChannelRange (value = count, index = first channel)
        int r = libusb_control_transfer(handle, 0x40, 2, (ushort)count, (ushort)start, buf, (ushort)count, TimeoutMs);
        if (r < 0) throw Fail(r, "Versturen mislukt");
    }

    public void SetSingle(int channel, byte value)
    {
        if (handle == IntPtr.Zero) throw new UdmxException(UdmxFault.Gone, "uDMX is niet open");
        int r = libusb_control_transfer(handle, 0x40, 1, value, (ushort)channel, null, 0, TimeoutMs);
        if (r < 0) throw Fail(r, "Versturen mislukt");
    }

    /// <summary>USB port reset: the dongle re-enumerates as if replugged; the send loop reopens it.</summary>
    public bool ResetDevice()
    {
        if (handle == IntPtr.Zero) return false;
        int r = libusb_reset_device(handle);
        // NOT_FOUND: the reset worked but the device came back as a new one (reopen follows anyway)
        if (r < 0 && r != ERROR_NOT_FOUND) throw Fail(r, "USB-reset mislukt");
        return true;
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero) return;
        try { if (claimed) libusb_release_interface(handle, 0); } catch { }
        try { libusb_close(handle); } catch { }
        handle = IntPtr.Zero;
        claimed = false;
    }
}
