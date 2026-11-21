namespace DmxLicht.Core.Output;

/// <summary>What went wrong talking to the dongle. Decides how the send loop reacts.</summary>
public enum UdmxFault
{
    /// <summary>No uDMX plugged in.</summary>
    NotFound,
    /// <summary>uDMX present but without a usable driver (WinUSB via Zadig).</summary>
    DriverMissing,
    /// <summary>Another program (or a second DmxLicht) has the dongle open.</summary>
    InUse,
    /// <summary>The device went away (unplugged, USB reset, power).</summary>
    Gone,
    /// <summary>The transfer took too long.</summary>
    Timeout,
    /// <summary>The device refused the command (USB stall).</summary>
    Rejected,
    Other,
}

public sealed class UdmxException : Exception
{
    public UdmxFault Fault { get; }
    public int Win32Code { get; }

    public UdmxException(UdmxFault fault, string message, int win32Code = 0) : base(message)
    {
        Fault = fault;
        Win32Code = win32Code;
    }

    /// <summary>Maps a Win32 error from a USB call to a fault.</summary>
    public static UdmxException FromWin32(int code, string what) => new(code switch
    {
        2 or 6 or 22 or 1167 => UdmxFault.Gone,          // file not found, invalid handle, bad command, device not connected
        121 or 995 or 1460 => UdmxFault.Timeout,         // semaphore timeout, aborted, timeout
        31 => UdmxFault.Rejected,                        // general failure: the device stalled the request
        5 or 32 => UdmxFault.InUse,                      // access denied, sharing violation
        _ => UdmxFault.Other,
    }, what + " (code " + code + ")", code);
}

/// <summary>A transport that can reset the USB device itself (port reset), e.g. libusbK.</summary>
public interface IResettableTransport
{
    /// <summary>True when the port was really reset (not every driver can).</summary>
    bool ResetDevice();
}

/// <summary>A transport whose blocked transfer can be cancelled from another thread (watchdog).</summary>
public interface ICancellableTransport
{
    void CancelPending();
}

/// <summary>
/// The raw USB link to a uDMX. Channel numbers are 0-based here (uDMX protocol).
/// Implementations throw <see cref="UdmxException"/>.
/// </summary>
public interface IUdmxTransport : IDisposable
{
    void Open();
    /// <summary>cmd_SetChannelRange: <paramref name="count"/> values starting at channel <paramref name="start"/>.</summary>
    void SetRange(int start, byte[] values, int offset, int count);
    /// <summary>cmd_SetSingleChannel.</summary>
    void SetSingle(int channel, byte value);
}
