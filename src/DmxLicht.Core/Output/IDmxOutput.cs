namespace DmxLicht.Core.Output;

/// <summary>
/// Something that sends DMX frames to lamps (USB dongle, later Art-Net / sACN).
/// Implementations run their own background loop and reconnect by themselves.
/// </summary>
public interface IDmxOutput
{
    /// <summary>Short device name for the status bar, e.g. "uDMX".</summary>
    string Name { get; }

    bool Connected { get; }

    /// <summary>Why there is no connection, or null when connected.</summary>
    string Error { get; }

    /// <summary>Human-readable state for the status bar.</summary>
    string Status { get; }

    /// <summary>Counters and current state for the diagnostics panel.</summary>
    OutputDiagnostics Diagnostics { get; }

    void Start();
    void Stop();
    void Reconnect();

    /// <summary>Hands over the latest frame. channels[1..512] = DMX 1..512; count = highest channel in use.</summary>
    void SetFrame(byte[] channels, int count);
}
