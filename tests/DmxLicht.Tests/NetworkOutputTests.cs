using System.Net;
using System.Net.Sockets;
using System.Text;
using DmxLicht.Core.Output;

namespace DmxLicht.Tests;

public class NetworkOutputTests
{
    static byte[] Frame()
    {
        var f = new byte[513];
        f[1] = 11; f[2] = 22; f[512] = 99;
        return f;
    }

    [Fact]
    public void ArtDmxPacketLayout()
    {
        byte[] p = ArtNetOutput.Build(Frame(), universe: 0x123, sequence: 7);
        Assert.Equal(530, p.Length);
        Assert.Equal("Art-Net\0", Encoding.ASCII.GetString(p, 0, 8));
        Assert.Equal(0x5000, p[8] | p[9] << 8);     // OpDmx, little endian
        Assert.Equal(14, p[11]);                     // protocol version
        Assert.Equal(7, p[12]);
        Assert.Equal(0x23, p[14]);                   // SubUni
        Assert.Equal(0x01, p[15]);                   // Net
        Assert.Equal(512, p[16] << 8 | p[17]);
        Assert.Equal(new byte[] { 11, 22 }, p[18..20]);
        Assert.Equal(99, p[529]);
    }

    [Fact]
    public void SacnPacketLayout()
    {
        var cid = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        byte[] p = SacnOutput.Build(Frame(), universe: 300, sequence: 5, priority: 120, cid);
        Assert.Equal(638, p.Length);
        Assert.Equal("ASC-E1.17\0\0\0", Encoding.ASCII.GetString(p, 4, 12));
        Assert.Equal(0x7000 | 622, p[16] << 8 | p[17]);   // root flags+length
        Assert.Equal(4, p[21]);
        Assert.Equal(cid, p[22..38]);
        Assert.Equal(0x7000 | 600, p[38] << 8 | p[39]);   // framing flags+length
        Assert.Equal(2, p[43]);
        Assert.Equal(120, p[108]);
        Assert.Equal(5, p[111]);
        Assert.Equal(300, p[113] << 8 | p[114]);
        Assert.Equal(0x7000 | 523, p[115] << 8 | p[116]); // DMP flags+length
        Assert.Equal(0xA1, p[118]);
        Assert.Equal(513, p[123] << 8 | p[124]);
        Assert.Equal(0, p[125]);                            // start code
        Assert.Equal(new byte[] { 11, 22 }, p[126..128]);
        Assert.Equal(99, p[637]);
        Assert.Equal(IPAddress.Parse("239.255.1.44"), SacnOutput.MulticastFor(300));
    }

    [Fact]
    public void ArtNetSendsChangesAndKeepAlive()
    {
        using var rx = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)rx.Client.LocalEndPoint!).Port;
        rx.Client.ReceiveTimeout = 3000;
        var o = new ArtNetOutput { Host = "127.0.0.1", Port = port, Universe = 2 };
        o.Start();
        try
        {
            o.SetFrame(Frame(), 512);
            IPEndPoint from = null;
            byte[] got;
            do got = rx.Receive(ref from); while (got[18] != 11);   // the first packet may be the empty frame
            Assert.Equal(2, got[14]);
            Assert.Equal(99, got[529]);
            Assert.True(o.Connected);
        }
        finally { o.Stop(); }
    }
}
