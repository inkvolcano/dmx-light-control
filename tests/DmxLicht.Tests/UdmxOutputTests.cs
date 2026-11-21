using System.Collections.Concurrent;
using DmxLicht.Core.Output;

namespace DmxLicht.Tests;

/// <summary>
/// A simulated uDMX: keeps a 512-channel buffer like the real firmware and can be told to misbehave.
/// One instance stands for the physical dongle; the output opens a new "transport" to it on every connect.
/// </summary>
sealed class FakeDongle
{
    public readonly byte[] Buffer = new byte[512];
    public volatile bool Plugged = true;
    public volatile bool InUse;
    /// <summary>Range transfers longer than this are refused (USB stall), like a picky clone.</summary>
    public volatile int MaxRange = 512;
    public volatile bool RejectRanges;
    /// <summary>Faults thrown by the next transfers, one each.</summary>
    public readonly ConcurrentQueue<UdmxFault> Glitches = new();
    public int Opens, RangeCalls, SingleCalls, Resets;
    /// <summary>The next transfers hang until cancelled (like a stuck USB stack).</summary>
    public int HangNext;
    /// <summary>Hangs also ignore cancelling (only end after this many ms).</summary>
    public volatile int HardHangMs;

    public void Unplug() { Plugged = false; Array.Clear(Buffer); }  // power loss clears the dongle

    public IUdmxTransport Connect() => new Link(this);

    sealed class Link(FakeDongle d) : IUdmxTransport, IResettableTransport, ICancellableTransport
    {
        public bool ResetDevice() { Interlocked.Increment(ref d.Resets); return true; }

        volatile bool cancelled;
        public void CancelPending() => cancelled = true;

        void MaybeHang()
        {
            if (Interlocked.Decrement(ref d.HangNext) < 0) { Interlocked.Exchange(ref d.HangNext, 0); return; }
            cancelled = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int hard = d.HardHangMs;
            while (hard > 0 ? sw.ElapsedMilliseconds < hard : !cancelled && sw.ElapsedMilliseconds < 20000) Thread.Sleep(1);
            throw new UdmxException(UdmxFault.Timeout, "aborted", 995);
        }

        bool open;

        public void Open()
        {
            if (!d.Plugged) throw new UdmxException(UdmxFault.NotFound, "not found");
            if (d.InUse) throw new UdmxException(UdmxFault.InUse, "in use", 5);
            open = true;
            Interlocked.Increment(ref d.Opens);
        }

        void Check()
        {
            if (!open || !d.Plugged) throw new UdmxException(UdmxFault.Gone, "gone", 1167);
            if (d.Glitches.TryDequeue(out UdmxFault f)) throw new UdmxException(f, "glitch " + f);
        }

        public void SetRange(int start, byte[] values, int offset, int count)
        {
            Check();
            MaybeHang();
            Interlocked.Increment(ref d.RangeCalls);
            if (d.RejectRanges || count > d.MaxRange) throw new UdmxException(UdmxFault.Rejected, "stall", 31);
            Array.Copy(values, offset, d.Buffer, start, count);
        }

        public void SetSingle(int channel, byte value)
        {
            Check();
            Interlocked.Increment(ref d.SingleCalls);
            d.Buffer[channel] = value;
        }

        public void Dispose() => open = false;
    }
}

public class UdmxOutputTests
{
    static readonly UdmxOptions Fast = new()
    {
        TickMs = 1, KeepAliveMs = 40, RetryDelayMs = 1, ReconnectMinMs = 5, ReconnectMaxMs = 20,
        NotFoundPollMs = 5, ProbeAfterFrames = 100000,
    };

    static (UdmxOutput output, FakeDongle dongle) Start(UdmxOptions opt = null)
    {
        var d = new FakeDongle();
        var o = new UdmxOutput(d.Connect, opt ?? Fast) { MinIntervalMs = 0 };
        o.Start();
        return (o, d);
    }

    static byte[] Frame(int seed, int count)
    {
        var f = new byte[513];
        var r = new Random(seed);
        for (int i = 1; i <= count; i++) f[i] = (byte)r.Next(1, 256);
        return f;
    }

    static bool Matches(FakeDongle d, byte[] f, int count)
    {
        for (int i = 1; i <= count; i++) if (d.Buffer[i - 1] != f[i]) return false;
        return true;
    }

    static void WaitFor(Func<bool> cond, string what, int ms = 4000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond())
        {
            if (sw.ElapsedMilliseconds > ms) Assert.Fail("timed out waiting for: " + what);
            Thread.Sleep(2);
        }
    }

    [Fact]
    public void WatchdogCancelsAHangingTransfer()
    {
        var (o, d) = Start(new UdmxOptions { TickMs = 1, KeepAliveMs = 40, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5, ProbeAfterFrames = 100000, HangMs = 100 });
        try
        {
            WaitFor(() => o.Connected, "connect");
            Interlocked.Exchange(ref d.HangNext, 1);
            byte[] f = Frame(7, 30);
            o.SetFrame(f, 30);
            WaitFor(() => Matches(d, f, 30), "frame after cancelled hang", 3000);
            Assert.Equal(1, d.Opens);                 // cancelled and retried, no reconnect needed
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void WatchdogRestartsALoopThatStaysStuck()
    {
        var (o, d) = Start(new UdmxOptions { TickMs = 1, KeepAliveMs = 40, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5, ProbeAfterFrames = 100000, HangMs = 100 });
        try
        {
            WaitFor(() => o.Connected, "connect");
            d.HardHangMs = 5000;                      // ignores cancel
            Interlocked.Exchange(ref d.HangNext, 1);
            o.SetFrame(Frame(8, 30), 30);
            Thread.Sleep(50);
            d.HardHangMs = 0;
            byte[] f = Frame(9, 30);
            o.SetFrame(f, 30);
            WaitFor(() => Matches(d, f, 30), "frame from the restarted loop", 2500);  // long before the hang ends
            Assert.True(d.Opens >= 2);
            WaitFor(() => o.Connected, "connected again");
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void FrequentDropsSwitchToSmallerBlocks()
    {
        var (o, d) = Start(new UdmxOptions { TickMs = 1, KeepAliveMs = 5, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5, ProbeAfterFrames = 100000, SafeChunk = 64 });
        try
        {
            o.SetFrame(Frame(10, 100), 100);
            for (int i = 0; i < 3; i++)
            {
                WaitFor(() => o.Connected && d.RangeCalls > 0, "connect " + i);
                d.Unplug();
                WaitFor(() => !o.Connected, "drop " + i);
                d.Plugged = true;
            }
            WaitFor(() => o.Connected, "connect again");
            Assert.Equal("block 64", o.Diagnostics.Mode);
            byte[] f = Frame(11, 100);
            o.SetFrame(f, 100);
            WaitFor(() => Matches(d, f, 100), "frame in small blocks");
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void SafeModeIsNotProbedAwayWhileDropsContinue()
    {
        var (o, d) = Start(new UdmxOptions { TickMs = 1, KeepAliveMs = 2, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5, ProbeAfterFrames = 5, SafeChunk = 64 });
        try
        {
            for (int i = 0; i < 3; i++)
            {
                WaitFor(() => o.Connected && d.RangeCalls > 0, "connect " + i);
                d.Unplug();
                WaitFor(() => !o.Connected, "drop " + i);
                d.Plugged = true;
            }
            WaitFor(() => o.Connected, "connect again");
            for (int i = 0; i < 40; i++) { o.SetFrame(Frame(20 + i, 200), 200); Thread.Sleep(3); }
            Assert.Equal("block 64", o.Diagnostics.Mode);    // probing does not go above the safe size
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void SendsTheWholeRangeInOneTransfer()
    {
        var (o, d) = Start();
        try
        {
            byte[] f = Frame(1, 42);
            o.SetFrame(f, 42);
            WaitFor(() => Matches(d, f, 42), "frame on dongle");
            Assert.Equal("block 512", o.Diagnostics.Mode);
            Assert.True(o.Connected);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void OneGlitchIsRetriedWithoutReconnectOrSlowerMode()
    {
        var (o, d) = Start();
        try
        {
            WaitFor(() => o.Connected, "connect");
            d.Glitches.Enqueue(UdmxFault.Timeout);
            byte[] f = Frame(2, 60);
            o.SetFrame(f, 60);
            WaitFor(() => Matches(d, f, 60), "frame on dongle");
            Assert.Equal(1, d.Opens);                       // no reconnect
            Assert.Equal("block 512", o.Diagnostics.Mode);  // v1.4 would have switched to single channels for good
            Assert.True(o.Diagnostics.Recovered >= 1);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void PickyDongleGetsSmallerBlocks()
    {
        var (o, d) = Start();
        d.MaxRange = 16;
        try
        {
            byte[] f = Frame(3, 100);
            o.SetFrame(f, 100);
            WaitFor(() => Matches(d, f, 100), "frame on dongle");
            Assert.Equal("block 16", o.Diagnostics.Mode);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void DongleWithoutRangeCommandFallsBackToSingleChannels()
    {
        var (o, d) = Start();
        d.RejectRanges = true;
        try
        {
            byte[] f = Frame(4, 30);
            o.SetFrame(f, 30);
            WaitFor(() => Matches(d, f, 30), "frame on dongle");
            Assert.Equal("single", o.Diagnostics.Mode);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void LargerBlocksAreTriedAgainLater()
    {
        var (o, d) = Start(new UdmxOptions { TickMs = 1, KeepAliveMs = 5, RetryDelayMs = 1, ReconnectMinMs = 5, NotFoundPollMs = 5, ProbeAfterFrames = 20 });
        d.MaxRange = 16;
        try
        {
            o.SetFrame(Frame(5, 64), 64);
            WaitFor(() => o.Diagnostics.Mode == "block 16", "shrink to 16");
            d.MaxRange = 512;  // e.g. a USB hub that was overloaded recovers
            WaitFor(() => o.Diagnostics.Mode == "block 512", "grow back to 512");
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void UnplugAndReplugRestoresTheLatestFrame()
    {
        var (o, d) = Start();
        try
        {
            byte[] a = Frame(6, 50);
            o.SetFrame(a, 50);
            WaitFor(() => Matches(d, a, 50), "first frame");

            d.Unplug();
            byte[] b = Frame(7, 50);
            o.SetFrame(b, 50);                      // changes while unplugged
            WaitFor(() => !o.Connected, "notice unplug");
            WaitFor(() => o.Diagnostics.Fault == "NotFound", "report not found while unplugged");

            d.Plugged = true;
            WaitFor(() => Matches(d, b, 50), "latest frame after replug");
            Assert.Equal(1, o.Diagnostics.Reconnects);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void DongleInUseByAnotherProgramIsReported()
    {
        var d = new FakeDongle { InUse = true };
        var o = new UdmxOutput(d.Connect, Fast) { MinIntervalMs = 0 };
        o.Start();
        try
        {
            WaitFor(() => o.Diagnostics.Fault == "InUse", "in-use fault");
            Assert.False(o.Connected);
            d.InUse = false;                        // other program closed
            WaitFor(() => o.Connected, "connect after release");
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void RepeatedTimeoutsLeadToAReconnect()
    {
        var (o, d) = Start();
        try
        {
            WaitFor(() => o.Connected, "connect");
            for (int i = 0; i < 40; i++) d.Glitches.Enqueue(UdmxFault.Timeout);
            byte[] f = Frame(8, 20);
            o.SetFrame(f, 20);
            WaitFor(() => d.Opens >= 2, "reopen");
            WaitFor(() => Matches(d, f, 20), "frame after reopen");
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void KeepsUpWithRapidChanges()
    {
        var (o, d) = Start();
        try
        {
            byte[] f = null;
            for (int i = 0; i < 300; i++)
            {
                f = Frame(100 + i, 120);
                o.SetFrame(f, 120);
                if (i % 50 == 0) d.Glitches.Enqueue(UdmxFault.Rejected);
                Thread.Sleep(1);
            }
            WaitFor(() => Matches(d, f, 120), "final frame");
            Assert.Equal(1, d.Opens);
        }
        finally { o.Stop(); }
    }

    [Fact]
    public void RecoverForgetsLearnedModesResetsAndReopens()
    {
        var (o, d) = Start();
        d.MaxRange = 16;
        try
        {
            o.SetFrame(Frame(9, 64), 64);
            WaitFor(() => o.Diagnostics.Mode == "block 16", "learned small blocks");
            d.MaxRange = 512;                      // e.g. the dongle was just in a bad state
            o.Recover();
            WaitFor(() => d.Opens >= 2 && o.Connected, "reopened");
            byte[] f = Frame(10, 64);
            o.SetFrame(f, 64);
            WaitFor(() => Matches(d, f, 64), "frame after recover");
            Assert.Equal("block 512", o.Diagnostics.Mode);   // big blocks again right away
            Assert.Equal(1, d.Resets);                     // USB port reset was asked
        }
        finally { o.Stop(); }
    }
}
