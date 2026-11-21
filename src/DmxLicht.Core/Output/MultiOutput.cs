namespace DmxLicht.Core.Output;

/// <summary>Which outputs are on and where they send. Part of the app settings.</summary>
public sealed class OutputSettings
{
    public bool Udmx { get; set; } = true;
    /// <summary>Which universe the USB dongle sends (1..8).</summary>
    public int UdmxUniverse { get; set; } = 1;
    public bool ArtNet { get; set; }
    public string ArtNetHost { get; set; } = "255.255.255.255";
    public int ArtNetUniverse { get; set; }
    public bool Sacn { get; set; }
    public int SacnUniverse { get; set; } = 1;
    /// <summary>Empty = multicast.</summary>
    public string SacnUnicast { get; set; } = "";
    public int SacnPriority { get; set; } = 100;
    /// <summary>Network card (local IPv4) for Art-Net/sACN; empty = default.</summary>
    public string NetworkBind { get; set; } = "";
}

/// <summary>Sends every frame to all enabled outputs (uDMX, Art-Net, sACN) at the same time.</summary>
public sealed class MultiOutput : IDmxOutput
{
    public UdmxOutput Udmx { get; }
    public ArtNetOutput ArtNet { get; } = new();
    public SacnOutput Sacn { get; } = new();
    OutputSettings settings = new();
    bool started;

    public MultiOutput(UdmxOutput udmx) { Udmx = udmx; }

    IEnumerable<IDmxOutput> Enabled()
    {
        if (settings.Udmx) yield return Udmx;
        if (settings.ArtNet) yield return ArtNet;
        if (settings.Sacn) yield return Sacn;
    }

    /// <summary>The output shown in the status bar: the dongle when it is on, else the first network output.</summary>
    IDmxOutput Primary => Enabled().FirstOrDefault() ?? Udmx;

    public string Name => string.Join(" + ", Enabled().Select(o => o.Name).DefaultIfEmpty("-"));
    public bool Connected => Enabled().Any(o => o.Connected);
    public string Error => Primary.Error;
    public string Status => Primary.Status;
    public OutputDiagnostics Diagnostics => Primary.Diagnostics;

    /// <summary>One line per enabled output, for the settings page.</summary>
    public IReadOnlyList<OutputDiagnostics> All => Enabled().Select(o => o.Diagnostics).ToList();

    public void Apply(OutputSettings s)
    {
        string bind = string.IsNullOrWhiteSpace(s.NetworkBind) ? null : s.NetworkBind.Trim();
        bool netChanged = ArtNet.Host != s.ArtNetHost || ArtNet.BindAddress != bind || Sacn.Unicast != s.SacnUnicast || Sacn.BindAddress != bind;
        ArtNet.Host = string.IsNullOrWhiteSpace(s.ArtNetHost) ? "255.255.255.255" : s.ArtNetHost.Trim();
        ArtNet.Universe = Math.Clamp(s.ArtNetUniverse, 0, 32767);
        Udmx.Universe = Math.Clamp(s.UdmxUniverse, 1, 8);
        ArtNet.BindAddress = bind;
        Sacn.Universe = Math.Clamp(s.SacnUniverse, 1, 63999);
        Sacn.Unicast = s.SacnUnicast?.Trim();
        Sacn.Priority = Math.Clamp(s.SacnPriority, 0, 200);
        Sacn.BindAddress = bind;
        settings = s;
        if (netChanged) { ArtNet.Reconnect(); Sacn.Reconnect(); }
        if (started) StartStop();
    }

    void StartStop()
    {
        if (settings.Udmx) Udmx.Start(); else Udmx.Stop();
        if (settings.ArtNet) ArtNet.Start(); else ArtNet.Stop();
        if (settings.Sacn) Sacn.Start(); else Sacn.Stop();
    }

    public void Start() { started = true; StartStop(); }

    public void Stop()
    {
        started = false;
        Udmx.Stop(); ArtNet.Stop(); Sacn.Stop();
    }

    public void Reconnect() { Udmx.Reconnect(); ArtNet.Reconnect(); Sacn.Reconnect(); }

    public void SetFrame(byte[] channels, int count)
    {
        // network outputs always get the frame, so switching them on later sends the current look at once
        Udmx.SetFrame(channels, count);
        ArtNet.SetFrame(channels, count);
        Sacn.SetFrame(channels, count);
    }
}
