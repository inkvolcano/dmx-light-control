using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DmxLicht.Core.Output;

/// <summary>
/// Base for UDP outputs: sends a changed frame at most every <see cref="MinIntervalMs"/>
/// and repeats the last frame every second (receivers drop out after a few seconds of silence).
/// </summary>
public abstract class NetworkOutput : IDmxOutput
{
    readonly object lockObj = new();
    byte[] data = new byte[513];
    bool dirty = true;
    Thread thread;
    volatile bool run;
    UdpClient udp;
    long frames, errors;
    volatile string error;
    DateTime? since, lastErrorAt;
    readonly Queue<long> recent = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    protected byte Sequence;

    public abstract string Name { get; }
    /// <summary>"target:port" for the status line.</summary>
    public abstract string Target { get; }
    public int MinIntervalMs { get; set; } = 25;
    /// <summary>Local IPv4 to send from (choose the network card), or null for the default.</summary>
    public string BindAddress { get; set; }

    public bool Connected => udp != null && error == null;
    public string Error => error;
    public string Status => Connected ? Name + " -> " + Target : Name + ": " + error;

    public OutputDiagnostics Diagnostics
    {
        get
        {
            lock (recent)
            {
                long now = clock.ElapsedMilliseconds;
                while (recent.Count > 0 && now - recent.Peek() > 2000) recent.Dequeue();
                return new OutputDiagnostics(Name, Connected, Connected ? null : "Other", Connected ? Target : error, since, 0,
                    Interlocked.Read(ref frames), Interlocked.Read(ref errors), 0, recent.Count / 2.0, Target, lastErrorAt, error);
            }
        }
    }

    /// <summary>Where universe <paramref name="index"/> (0 = the first of the frame) goes.</summary>
    protected abstract IPEndPoint Destination(int index);
    /// <summary>The packet for universe <paramref name="index"/>; its 512 channels start at frame[offset + 1].</summary>
    protected abstract byte[] Packet(byte[] frame, int offset, int index);
    protected virtual void Configure(UdpClient client) { }

    public void Start()
    {
        if (run) return;
        run = true;
        thread = new Thread(Loop) { IsBackground = true, Name = Name };
        thread.Start();
    }

    public void Stop()
    {
        run = false;
        thread?.Join(1000);
        thread = null;
        udp?.Dispose();
        udp = null;
    }

    /// <summary>Re-opens the socket (after changing the target or network card).</summary>
    public void Reconnect() { lock (lockObj) { udp?.Dispose(); udp = null; dirty = true; } }

    public void SetFrame(byte[] channels, int count)
    {
        lock (lockObj)
        {
            if (data.Length != channels.Length) { data = new byte[channels.Length]; dirty = true; }
            for (int i = 1; i < channels.Length; i++)
                if (data[i] != channels[i]) { data[i] = channels[i]; dirty = true; }
        }
    }

    void Loop()
    {
        long lastSend = long.MinValue / 2;
        while (run)
        {
            try
            {
                if (udp == null)
                {
                    var client = BindAddress != null && IPAddress.TryParse(BindAddress, out IPAddress local)
                        ? new UdpClient(new IPEndPoint(local, 0)) : new UdpClient(AddressFamily.InterNetwork);
                    Configure(client);
                    udp = client;
                    since = DateTime.Now;
                    error = null;
                }
                long now = clock.ElapsedMilliseconds;
                byte[] frame = null;
                lock (lockObj)
                {
                    if ((dirty && now - lastSend >= MinIntervalMs) || now - lastSend >= 1000)
                    {
                        frame = (byte[])data.Clone();
                        dirty = false;
                    }
                }
                if (frame != null)
                {
                    Sequence = (byte)(Sequence == 255 ? 1 : Sequence + 1);
                    int universes = Math.Max(1, (frame.Length - 1) / 512);
                    for (int u = 0; u < universes; u++)
                    {
                        byte[] p = Packet(frame, u * 512, u);
                        udp.Send(p, p.Length, Destination(u));
                    }
                    lastSend = now;
                    Interlocked.Increment(ref frames);
                    lock (recent) recent.Enqueue(now);
                    error = null;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                lastErrorAt = DateTime.Now;
                if (error != ex.Message) Log.Write(Name + ": " + ex.Message);
                error = ex.Message;
                udp?.Dispose();
                udp = null;
                for (int i = 0; i < 20 && run; i++) Thread.Sleep(50);
                continue;
            }
            Thread.Sleep(5);
        }
    }

    /// <summary>Local IPv4 addresses (for choosing the network card).</summary>
    public static List<string> LocalAddresses()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString()).Distinct().ToList();
        }
        catch { return new List<string>(); }
    }
}

/// <summary>Art-Net 4 ArtDmx to a node, a broadcast address or 127.0.0.1 (e.g. a visualiser).</summary>
public sealed class ArtNetOutput : NetworkOutput
{
    public const int DefaultPort = 6454;
    public string Host { get; set; } = "255.255.255.255";
    public int Port { get; set; } = DefaultPort;
    /// <summary>15-bit port address: net (7 bits) . subnet (4) . universe (4); 0 = the first universe.</summary>
    public int Universe { get; set; }

    public override string Name => "Art-Net";
    public override string Target => $"{Host} U{Universe}";

    protected override void Configure(UdpClient client) => client.EnableBroadcast = true;
    protected override IPEndPoint Destination(int index) => new(IPAddress.Parse(Host), Port);
    protected override byte[] Packet(byte[] frame, int offset, int index) => Build(frame, Universe + index, Sequence, offset);

    public static byte[] Build(byte[] frame, int universe, byte sequence, int offset = 0)
    {
        var p = new byte[18 + 512];
        Encoding.ASCII.GetBytes("Art-Net").CopyTo(p, 0);    // ID with trailing 0
        p[8] = 0x00; p[9] = 0x50;                            // OpDmx 0x5000, little endian
        p[10] = 0; p[11] = 14;                               // protocol version 14
        p[12] = sequence;
        p[13] = 0;                                           // physical port
        p[14] = (byte)(universe & 0xFF);                     // SubUni
        p[15] = (byte)((universe >> 8) & 0x7F);              // Net
        p[16] = 512 >> 8; p[17] = 512 & 0xFF;                // length, big endian
        Array.Copy(frame, offset + 1, p, 18, Math.Min(512, frame.Length - offset - 1));
        return p;
    }
}

/// <summary>sACN (ANSI E1.31) to the universe's multicast group, or unicast to one receiver.</summary>
public sealed class SacnOutput : NetworkOutput
{
    public const int Port = 5568;
    static readonly byte[] Cid = Guid.NewGuid().ToByteArray();
    /// <summary>1..63999.</summary>
    public int Universe { get; set; } = 1;
    /// <summary>Receiver IP, or null/empty for multicast 239.255.x.y.</summary>
    public string Unicast { get; set; }
    public int Priority { get; set; } = 100;

    public override string Name => "sACN";
    public override string Target => (string.IsNullOrWhiteSpace(Unicast) ? MulticastFor(Universe).ToString() : Unicast) + $" U{Universe}";

    public static IPAddress MulticastFor(int universe) => new(new byte[] { 239, 255, (byte)(universe >> 8), (byte)(universe & 0xFF) });

    protected override void Configure(UdpClient client) => client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 8);

    protected override IPEndPoint Destination(int index) =>
        new(string.IsNullOrWhiteSpace(Unicast) ? MulticastFor(Universe + index) : IPAddress.Parse(Unicast), Port);

    protected override byte[] Packet(byte[] frame, int offset, int index) => Build(frame, Universe + index, Sequence, Priority, Cid, offset);

    public static byte[] Build(byte[] frame, int universe, byte sequence, int priority, byte[] cid, int offset = 0)
    {
        var p = new byte[638];
        // root layer
        p[0] = 0x00; p[1] = 0x10;                                   // preamble size
        Encoding.ASCII.GetBytes("ASC-E1.17").CopyTo(p, 4);          // ACN packet identifier (12 bytes, zero padded)
        FlagsLength(p, 16, 638 - 16);
        p[21] = 0x04;                                               // VECTOR_ROOT_E131_DATA
        Array.Copy(cid, 0, p, 22, 16);
        // framing layer
        FlagsLength(p, 38, 638 - 38);
        p[43] = 0x02;                                               // VECTOR_E131_DATA_PACKET
        Encoding.UTF8.GetBytes("DMX Light Control").CopyTo(p, 44);           // source name (64 bytes)
        p[108] = (byte)Math.Clamp(priority, 0, 200);
        p[111] = sequence;
        p[113] = (byte)(universe >> 8); p[114] = (byte)universe;
        // DMP layer
        FlagsLength(p, 115, 638 - 115);
        p[117] = 0x02;                                              // VECTOR_DMP_SET_PROPERTY
        p[118] = 0xA1;                                              // address & data type
        p[121] = 0x00; p[122] = 0x01;                               // address increment
        p[123] = 0x02; p[124] = 0x01;                               // property count 513
        p[125] = 0x00;                                              // DMX start code
        Array.Copy(frame, offset + 1, p, 126, Math.Min(512, frame.Length - offset - 1));
        return p;
    }

    static void FlagsLength(byte[] p, int at, int length)
    {
        p[at] = (byte)(0x70 | (length >> 8));
        p[at + 1] = (byte)length;
    }
}
