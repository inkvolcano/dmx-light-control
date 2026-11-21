using System.Net;
using System.Net.Sockets;
using DmxLicht.Core;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Output;
using DmxLicht.Core.Show;

namespace DmxLicht.Tests;

/// <summary>More than 512 channels: fixtures in universes 1..8, outputs per universe.</summary>
public class UniverseTests
{
    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    static (Rig rig, ShowState s) TwoUniverses()
    {
        var patch = new List<PatchedFixture>
        {
            new() { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1, Universe = 1 },
            new() { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 1, Universe = 2 },   // same address, other universe
            new() { Id = "c", TypeId = "generic-rgb", Mode = "3ch", Address = 510, Universe = 3 },
        };
        var s = new ShowState();
        s.Look("a").On = true; s.Look("a").Color = ColorRef.Palette("red");
        s.Look("b").On = true; s.Look("b").Color = ColorRef.Palette("green");
        s.Look("c").On = true; s.Look("c").Color = ColorRef.Palette("blue");
        return (Rig.Build(patch, Lib), s);
    }

    [Fact]
    public void FrameHoldsAllUniversesOneAfterAnother()
    {
        var (rig, s) = TwoUniverses();
        Assert.Equal(3, rig.Universes);
        Assert.Equal(1 + 3 * 512, rig.FrameSize);
        Assert.Empty(rig.Conflicts());                                   // address 1 in two universes is fine
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 0, 0 }, f[1..4]);                 // universe 1, channel 1
        Assert.Equal(new byte[] { 0, 255, 0 }, f[513..516]);             // universe 2, channel 1
        Assert.Equal(new byte[] { 0, 0, 255 }, f[(1024 + 510)..(1024 + 513)]);   // universe 3, channels 510-512
        Assert.Equal(1536, rig.UsedChannelCount);
        Assert.Equal(new[] { 3, 3, 512 }, new[] { rig.UsedIn(1), rig.UsedIn(2), rig.UsedIn(3) });
        // preview reads every universe back
        Assert.Equal(new[] { "#FF0000", "#00FF00", "#0000FF" }, Preview.HeadColors(rig, f));
    }

    [Fact]
    public void ConflictsOnlyWithinAUniverse()
    {
        var patch = new List<PatchedFixture>
        {
            new() { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1, Universe = 2 },
            new() { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 2, Universe = 2 },
        };
        AddressConflict c = Assert.Single(Rig.Build(patch, Lib).Conflicts());
        Assert.Equal((2, 3), (c.From, c.To));
    }

    [Fact]
    public void AFullUniverseContinuesInTheNext()
    {
        var used = new[] { (1, 1, 510) };
        Assert.Equal((1, 511), Rig.NextFree(used, 2));
        Assert.Equal((2, 1), Rig.NextFree(used, 6));                     // does not fit in universe 1 anymore
        Assert.Equal((3, 1), Rig.NextFree(used, 6, fromUniverse: 3));
        var full = Enumerable.Range(1, Dmx.MaxUniverses).Select(u => (u, 1, 512)).ToArray();
        Assert.Equal((-1, -1), Rig.NextFree(full, 1));
        Assert.Equal(8, Dmx.Clamp(99));
        Assert.Equal(1, Dmx.Clamp(0));
    }

    [Fact]
    public void ArtNetAndSacnCarryEachUniverse()
    {
        var (rig, s) = TwoUniverses();
        byte[] f = FrameBuilder.Build(rig, s, 0);
        byte[] u1 = ArtNetOutput.Build(f, 0, 1, offset: 512);              // second universe of the frame
        Assert.Equal(0, u1[14]);
        Assert.Equal(new byte[] { 0, 255, 0 }, u1[18..21]);
        byte[] sacn = SacnOutput.Build(f, 7, 1, 100, new byte[16], offset: 1024);
        Assert.Equal(7, sacn[114]);
        Assert.Equal(255, sacn[126 + 511]);                                // channel 512 of universe 3: blue
    }

    [Fact]
    public void ArtNetSendsOnePacketPerUniverse()
    {
        using var rx = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)rx.Client.LocalEndPoint!).Port;
        var art = new ArtNetOutput { Host = "127.0.0.1", Port = port, Universe = 4 };
        art.Start();
        try
        {
            var (rig, s) = TwoUniverses();
            art.SetFrame(FrameBuilder.Build(rig, s, 0), rig.UsedChannelCount);
            var seen = new HashSet<int>();
            rx.Client.ReceiveTimeout = 2000;
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (seen.Count < 3)
            {
                byte[] p = rx.Receive(ref from);
                seen.Add(p[14] | (p[15] << 8));
            }
            Assert.Equal(new[] { 4, 5, 6 }, seen.OrderBy(x => x));          // base universe 4, then 5 and 6
        }
        finally { art.Stop(); }
    }

    [Fact]
    public void TheDongleSendsItsOwnUniverse()
    {
        var d = new FakeDongle();
        var o = new UdmxOutput(d.Connect, new UdmxOptions { TickMs = 1, KeepAliveMs = 20, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5 }) { MinIntervalMs = 0, Universe = 2 };
        o.Start();
        try
        {
            var (rig, s) = TwoUniverses();
            o.SetFrame(FrameBuilder.Build(rig, s, 0), rig.UsedChannelCount);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!(d.Buffer[1] == 255) && sw.ElapsedMilliseconds < 3000) Thread.Sleep(5);
            Assert.Equal(new byte[] { 0, 255, 0 }, d.Buffer[0..3]);       // universe 2 on the dongle: green
        }
        finally { o.Stop(); }
    }
}
