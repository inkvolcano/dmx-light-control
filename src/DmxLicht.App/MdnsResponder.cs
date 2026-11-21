using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Answers "dmxlicht.local" on the local network (multicast DNS, as iPhones, Macs, Android 12+ and Windows
/// resolve .local names), so phones can use http://dmxlicht.local:5150 even when the PC's IP address changes.
/// Only A records (IPv4); the answer is the PC's address on the asker's own network. Runs only while network
/// access is on. Windows' own mDNS service may share the port (ReuseAddress).
/// </summary>
sealed class MdnsResponder : IDisposable
{
    static readonly IPAddress Group = IPAddress.Parse("224.0.0.251");
    const int Port = 5353;
    UdpClient udp;
    CancellationTokenSource stop;
    string name = "dmxlicht";

    public static string Status { get; private set; } = "off";
    public static string HostName { get; private set; } = "dmxlicht.local";

    public void Apply(bool on, string wantedName)
    {
        string n = Clean(wantedName);
        if (on && udp != null && n == name) return;
        StopListening();
        name = n;
        HostName = name + ".local";
        if (!on) { Status = "off"; return; }
        try
        {
            var u = new UdpClient(AddressFamily.InterNetwork) { ExclusiveAddressUse = false };
            u.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            u.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            foreach (IPAddress local in LocalIPv4())
                try { u.JoinMulticastGroup(Group, local); } catch { }
            u.MulticastLoopback = true;
            udp = u;
            stop = new CancellationTokenSource();
            _ = Task.Run(() => Listen(u, stop.Token));
            Status = "on";
            Log.Write("Vaste naam actief: http://" + HostName);
        }
        catch (Exception ex)
        {
            Status = "error: " + ex.Message;
            Log.Write("Vaste naam (" + HostName + ") werkt niet: " + ex.Message);
        }
    }

    /// <summary>Letters, digits and dashes only (a DNS label).</summary>
    public static string Clean(string s)
    {
        var b = new StringBuilder();
        foreach (char c in (s ?? "").Trim().ToLowerInvariant()) if (char.IsAsciiLetterOrDigit(c) || c == '-') b.Append(c);
        string r = b.ToString().Trim('-');
        return r.Length == 0 ? "dmxlicht" : r[..Math.Min(40, r.Length)];
    }

    static List<IPAddress> LocalIPv4() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.SupportsMulticast)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
            .Select(a => a.Address).ToList();

    async Task Listen(UdpClient u, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await u.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { continue; }
            try
            {
                byte[] answer = Answer(r.Buffer, name + ".local", r.RemoteEndPoint.Address, out bool unicast);
                if (answer == null) continue;
                // QU bit or a one-shot query from another port: answer the asker directly, else the group
                IPEndPoint to = unicast || r.RemoteEndPoint.Port != Port ? r.RemoteEndPoint : new IPEndPoint(Group, Port);
                await u.SendAsync(answer, answer.Length, to);
            }
            catch { }
        }
    }

    /// <summary>An mDNS response with our A record when <paramref name="packet"/> asks for <paramref name="host"/>; else null.</summary>
    public static byte[] Answer(byte[] packet, string host, IPAddress asker, out bool unicast, IReadOnlyList<(IPAddress Ip, IPAddress Mask)> addresses = null)
    {
        unicast = false;
        if (packet.Length < 12) return null;
        int flags = packet[2] << 8 | packet[3];
        if ((flags & 0x8000) != 0) return null;               // a response, not a query
        int qd = packet[4] << 8 | packet[5];
        int pos = 12;
        ushort id = (ushort)(packet[0] << 8 | packet[1]);
        for (int q = 0; q < qd && pos < packet.Length; q++)
        {
            string qname = ReadName(packet, ref pos);
            if (qname == null || pos + 4 > packet.Length) return null;
            int type = packet[pos] << 8 | packet[pos + 1];
            int cls = packet[pos + 2] << 8 | packet[pos + 3];
            pos += 4;
            if (!qname.Equals(host, StringComparison.OrdinalIgnoreCase) || (type != 1 && type != 255)) continue;
            unicast = (cls & 0x8000) != 0;
            IPAddress ip = Pick(asker, addresses);
            if (ip == null) return null;
            var o = new List<byte>
            {
                (byte)(id >> 8), (byte)id, 0x84, 0x00,   // id, flags: response + authoritative
                0, 0, 0, 1, 0, 0, 0, 0,                   // 0 questions, 1 answer, 0 authority, 0 additional
            };
            foreach (string label in host.Split('.'))
            {
                byte[] l = Encoding.ASCII.GetBytes(label);
                o.Add((byte)l.Length); o.AddRange(l);
            }
            o.Add(0);
            o.AddRange(new byte[] { 0, 1, 0x80, 1, 0, 0, 0, 120, 0, 4 });   // type A, class IN + cache-flush, TTL 120 s, 4 bytes
            o.AddRange(ip.GetAddressBytes());
            return o.ToArray();
        }
        return null;
    }

    /// <summary>Our address on the asker's network (same subnet), else the preferred LAN address.</summary>
    static IPAddress Pick(IPAddress asker, IReadOnlyList<(IPAddress Ip, IPAddress Mask)> addresses)
    {
        addresses ??= NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && a.IPv4Mask != null && !a.Address.ToString().StartsWith("169.254."))
            .Select(a => (a.Address, a.IPv4Mask)).ToList();
        if (asker != null && asker.IsIPv4MappedToIPv6) asker = asker.MapToIPv4();
        if (asker != null && asker.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] q = asker.GetAddressBytes();
            foreach (var (ip, mask) in addresses)
            {
                byte[] a = ip.GetAddressBytes(), m = mask.GetAddressBytes();
                bool same = true;
                for (int i = 0; i < 4; i++) if ((a[i] & m[i]) != (q[i] & m[i])) { same = false; break; }
                if (same) return ip;
            }
            if (IPAddress.IsLoopback(asker) || addresses.Any(x => x.Ip.Equals(asker))) return addresses.FirstOrDefault().Ip ?? asker;
        }
        return PhoneAccess.Addresses().Select(a => IPAddress.Parse(a.Ip)).FirstOrDefault();
    }

    static string ReadName(byte[] p, ref int pos)
    {
        var labels = new List<string>();
        int jumps = 0, end = -1;
        while (pos < p.Length)
        {
            int len = p[pos];
            if (len == 0) { pos++; break; }
            if ((len & 0xC0) == 0xC0)
            {
                if (pos + 1 >= p.Length || ++jumps > 10) return null;
                int target = (len & 0x3F) << 8 | p[pos + 1];
                if (end < 0) end = pos + 2;
                pos = target;
                continue;
            }
            if (pos + 1 + len > p.Length) return null;
            labels.Add(Encoding.ASCII.GetString(p, pos + 1, len));
            pos += 1 + len;
        }
        if (end >= 0) pos = end;
        return string.Join(".", labels);
    }

    void StopListening()
    {
        try { stop?.Cancel(); } catch { }
        try { udp?.Dispose(); } catch { }
        udp = null;
    }

    public void Dispose() => StopListening();
}
