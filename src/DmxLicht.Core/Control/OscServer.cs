using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DmxLicht.Core.Engine;

namespace DmxLicht.Core.Control;

/// <summary>An incoming OSC message: address plus arguments (int, float, string).</summary>
public sealed record OscMessage(string Address, IReadOnlyList<object> Args);

/// <summary>
/// Receives OSC over UDP and runs it as a command. The address becomes the command:
/// <c>/dmxlicht/scene/Rood</c>, <c>/scene "Rood"</c>, <c>/master 0.8</c>, <c>/blackout 1</c>, <c>/flash/red 5</c>.
/// A button that sends 0 on release is ignored for one-shot commands (scene, tap, flash...).
/// </summary>
public sealed class OscServer : IDisposable
{
    static readonly HashSet<string> OneShot = new() { "scene", "tap", "flash", "effect", "color", "colour", "on", "off", "stop", "timeline" };

    readonly LightEngine engine;
    UdpClient udp;
    CancellationTokenSource cts;

    public int Port { get; private set; }
    public string Error { get; private set; }
    public OscMessage Last { get; private set; }

    public OscServer(LightEngine engine) { this.engine = engine; }

    public void Start(int port)
    {
        Stop();
        Port = port;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            cts = new CancellationTokenSource();
            _ = Task.Run(() => Receive(cts.Token));
            Error = null;
            Log.Write("OSC luistert op poort " + port);
        }
        catch (Exception ex) { Error = ex.Message; Log.Write("OSC starten mislukt: " + ex.Message); }
    }

    public void Stop()
    {
        cts?.Cancel();
        udp?.Dispose();
        udp = null;
    }

    async Task Receive(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult r = await udp.ReceiveAsync(ct);
                foreach (OscMessage m in Parse(r.Buffer)) Handle(m);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Error = ex.Message; }
        }
    }

    void Handle(OscMessage m)
    {
        Last = m;
        string line = ToCommand(m);
        if (line != null) Commands.Run(engine, line);
    }

    /// <summary>OSC message to a command line, or null when it should be ignored.</summary>
    public static string ToCommand(OscMessage m)
    {
        var parts = m.Address.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0 && parts[0].Equals("dmxlicht", StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
        if (parts.Count == 0) return null;
        var args = m.Args.Select(a => a switch
        {
            // always with a decimal point: "1.0" means a fader at full (0..1), not 1 %
            float f => f.ToString("0.0#####", CultureInfo.InvariantCulture),
            double d => d.ToString("0.0#####", CultureInfo.InvariantCulture),
            _ => a?.ToString(),
        }).Where(a => a != null).ToList();

        string cmd = parts[0].ToLowerInvariant();
        if (OneShot.Contains(cmd) && args.Count > 0 && double.TryParse(args[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double last))
        {
            if (last == 0) return null;          // button released
            if (last == 1 && parts.Count > 1) args.RemoveAt(args.Count - 1); // "/scene/Rood 1" = press
        }
        return string.Join(" ", parts.Concat(args).Select(Quote));
    }

    static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

    /// <summary>Parses a packet: a message or a #bundle of messages.</summary>
    public static List<OscMessage> Parse(byte[] data)
    {
        var list = new List<OscMessage>();
        ParseInto(data, 0, data.Length, list);
        return list;
    }

    static void ParseInto(byte[] d, int start, int end, List<OscMessage> list)
    {
        int i = start;
        string address = ReadString(d, ref i);
        if (address == "#bundle")
        {
            i += 8; // timetag
            while (i + 4 <= end)
            {
                int size = ReadInt(d, ref i);
                if (size <= 0 || i + size > end) break;
                ParseInto(d, i, i + size, list);
                i += size;
            }
            return;
        }
        if (!address.StartsWith('/')) return;
        var args = new List<object>();
        if (i < end && d[i] == ',')
        {
            string tags = ReadString(d, ref i);
            foreach (char t in tags.Skip(1))
            {
                switch (t)
                {
                    case 'i': args.Add(ReadInt(d, ref i)); break;
                    case 'f': args.Add(ReadFloat(d, ref i)); break;
                    case 's': args.Add(ReadString(d, ref i)); break;
                    case 'T': args.Add(1); break;
                    case 'F': args.Add(0); break;
                    case 'h': i += 8; break;
                    case 'd': i += 8; break;
                    default: break;
                }
            }
        }
        list.Add(new OscMessage(address, args));
    }

    static string ReadString(byte[] d, ref int i)
    {
        int s = i;
        while (i < d.Length && d[i] != 0) i++;
        string v = Encoding.UTF8.GetString(d, s, i - s);
        i = (i + 4) & ~3; // skip the terminator and padding to 4 bytes
        return v;
    }

    static int ReadInt(byte[] d, ref int i)
    {
        int v = d[i] << 24 | d[i + 1] << 16 | d[i + 2] << 8 | d[i + 3];
        i += 4;
        return v;
    }

    static float ReadFloat(byte[] d, ref int i) => BitConverter.Int32BitsToSingle(ReadInt(d, ref i));

    public void Dispose() => Stop();

    // ---------- building messages (tests, and handy for sending feedback later) ----------
    public static byte[] Build(string address, params object[] args)
    {
        var ms = new MemoryStream();
        void Str(string s) { byte[] b = Encoding.UTF8.GetBytes(s); ms.Write(b); int pad = 4 - b.Length % 4; ms.Write(new byte[pad]); }
        void Int(int v) { ms.Write(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }); }
        Str(address);
        Str("," + string.Concat(args.Select(a => a switch { int => "i", float => "f", _ => "s" })));
        foreach (object a in args)
            switch (a)
            {
                case int v: Int(v); break;
                case float f: Int(BitConverter.SingleToInt32Bits(f)); break;
                default: Str(a.ToString()); break;
            }
        return ms.ToArray();
    }
}
