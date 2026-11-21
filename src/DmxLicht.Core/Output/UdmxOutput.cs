using System.Diagnostics;

namespace DmxLicht.Core.Output;

/// <summary>Timings for <see cref="UdmxOutput"/> (tests use shorter ones).</summary>
public sealed class UdmxOptions
{
    public int TickMs { get; init; } = 10;
    /// <summary>Full refresh at least this often, as a safety net and USB keep-alive.</summary>
    public int KeepAliveMs { get; init; } = 500;
    public int RetryDelayMs { get; init; } = 25;
    public int ReconnectMinMs { get; init; } = 250;
    public int ReconnectMaxMs { get; init; } = 2000;
    /// <summary>Poll interval while no dongle is plugged in.</summary>
    public int NotFoundPollMs { get; init; } = 500;
    /// <summary>uDMX firmware takes up to 512 channels in one transfer.</summary>
    public int MaxChunk { get; init; } = 512;
    public int MinChunk { get; init; } = 8;
    /// <summary>After this many clean frames, try a larger block again.</summary>
    public int ProbeAfterFrames { get; init; } = 300;
    /// <summary>Failed frames in a row before the connection is reopened.</summary>
    public int FailuresBeforeReconnect { get; init; } = 3;
    /// <summary>A transfer that takes longer than this is broken off by the watchdog.</summary>
    public int HangMs { get; init; } = 3000;
    /// <summary>This many drops within <see cref="DropWindowMs"/> switch to smaller, safer blocks.</summary>
    public int DropsForSafeMode { get; init; } = 3;
    public int DropWindowMs { get; init; } = 120_000;
    public int SafeChunk { get; init; } = 64;
}

/// <summary>State of an output for the status bar and the diagnostics panel.</summary>
public sealed record OutputDiagnostics(
    string Name, bool Connected, string Fault, string Detail, DateTime? ConnectedSince,
    int Reconnects, long Frames, long Errors, long Recovered, double FramesPerSecond, string Mode,
    DateTime? LastErrorAt, string LastError);

/// <summary>
/// Sends frames to a uDMX on a background thread and keeps the link alive:
/// <list type="bullet">
/// <item>A failed transfer is retried once before anything else happens (most USB glitches are one-offs).</item>
/// <item>Sends whole ranges in one transfer (like QLC+); if the dongle refuses, the block size halves,
///   down to single channels as a last resort, and larger blocks are tried again after a while.</item>
/// <item>Unplugged / in use: the connection is dropped at once and reopened with back-off.</item>
/// <item>Only changed channels are sent, plus a full refresh every <see cref="UdmxOptions.KeepAliveMs"/>.</item>
/// </list>
/// The uDMX keeps outputting its last buffer by itself, so a short USB gap never blacks out the lamps.
/// </summary>
public sealed class UdmxOutput : IDmxOutput
{
    readonly Func<IUdmxTransport> factory;
    readonly UdmxOptions opt;
    readonly object lockObj = new();
    readonly byte[] data = new byte[513];
    readonly byte[] sent = new byte[513];
    int used = 1;
    bool dirty;

    IUdmxTransport dev;
    Thread thread;
    volatile bool run;
    volatile bool reconnectRequested;

    // watchdog: the send loop beats; a transfer that hangs is cancelled, a dead loop restarted
    long heartbeat;
    int generation, hangStage;
    Timer watchdog;
    readonly Queue<long> drops = new();

    // learned during the session, kept across reconnects
    int chunk;
    bool singleMode;
    int cleanFrames;
    bool haveSent;

    // diagnostics
    volatile bool connected;
    volatile string status = "Verbinden...";
    volatile string error;
    volatile string fault;
    DateTime? connectedSince, lastErrorAt;
    long frames, errors, recovered;
    int reconnects;
    readonly Queue<long> recentFrames = new();
    readonly Stopwatch clock = Stopwatch.StartNew();

    public UdmxOutput() : this(() => new AutoUdmxTransport(), null) { }

    public UdmxOutput(Func<IUdmxTransport> factory, UdmxOptions options = null)
    {
        this.factory = factory;
        opt = options ?? new UdmxOptions();
        chunk = opt.MaxChunk;
    }

    public string Name => "uDMX";
    public bool Connected => connected;
    public string Status => status;
    public string Error => connected ? null : error;
    public int Reconnects => reconnects;
    long opens;
    /// <summary>Successful opens so far (to see that a reconnect really happened).</summary>
    public long Opens => Interlocked.Read(ref opens);

    /// <summary>Minimum time between sends of changed frames (the "dongle speed" setting).</summary>
    public int MinIntervalMs { get; set; } = 60;

    public OutputDiagnostics Diagnostics
    {
        get
        {
            lock (recentFrames)
            {
                long now = clock.ElapsedMilliseconds;
                while (recentFrames.Count > 0 && now - recentFrames.Peek() > 2000) recentFrames.Dequeue();
                return new OutputDiagnostics(Name, connected, connected ? null : fault, connected ? null : error,
                    connectedSince, reconnects, Interlocked.Read(ref frames), Interlocked.Read(ref errors), Interlocked.Read(ref recovered),
                    recentFrames.Count / 2.0, singleMode ? "single" : "block " + chunk, lastErrorAt, lastErrorText);
            }
        }
    }
    volatile string lastErrorText;

    public void Start()
    {
        if (run) return;
        run = true;
        Beat();
        StartLoop(generation);
        watchdog = new Timer(_ => Watch(), null, 500, 500);
    }

    void StartLoop(int gen)
    {
        thread = new Thread(() => Loop(gen)) { IsBackground = true, Name = "uDMX", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    void Beat() => Interlocked.Exchange(ref heartbeat, clock.ElapsedMilliseconds);

    /// <summary>
    /// Some USB stacks let a transfer hang despite the time-out. Then nothing is sent while the status still
    /// says "connected". First the transfer is cancelled; if the loop still does not come back, a new one starts.
    /// </summary>
    void Watch()
    {
        if (!run) return;
        long age = clock.ElapsedMilliseconds - Interlocked.Read(ref heartbeat);
        if (age < opt.HangMs) { hangStage = 0; return; }
        if (hangStage == 0)
        {
            hangStage = 1;
            Log.Write("uDMX reageert " + age + " ms niet: verzending afgebroken");
            try { (dev as ICancellableTransport)?.CancelPending(); } catch { }
        }
        else if (hangStage == 1 && age >= opt.HangMs * 3)
        {
            hangStage = 2;
            Log.Write("uDMX-verzending zat vast: opnieuw gestart");
            int g = Interlocked.Increment(ref generation);
            IUdmxTransport old = dev;
            dev = null;                 // the stuck loop disposes its own device when it wakes up
            try { (old as ICancellableTransport)?.CancelPending(); } catch { }
            connected = false; connectedSince = null;
            status = "Verbinding vastgelopen - opnieuw verbinden...";
            Beat();
            StartLoop(g);
        }
    }

    public void Stop()
    {
        run = false;
        Interlocked.Increment(ref generation);   // a loop that is still stuck ends by itself and never touches a new one
        watchdog?.Dispose();
        watchdog = null;
        thread?.Join(3000);
        thread = null;
        connected = false;
        status = "Uit";
    }

    public void Reconnect() { reconnectRequested = true; Log.Write("Opnieuw verbinden gevraagd"); }

    volatile bool recoverRequested;

    /// <summary>
    /// Stronger than <see cref="Reconnect"/>: forgets what was learned about the dongle (smaller blocks,
    /// channel-by-channel), resets the USB device when the driver allows it (libusbK), and reopens it.
    /// </summary>
    public void Recover()
    {
        recoverRequested = true;
        reconnectRequested = true;
        Log.Write("USB herstellen gevraagd");
    }

    /// <summary>Which universe of the frame this dongle sends (1..8).</summary>
    public int Universe { get; set; } = 1;

    public void SetFrame(byte[] channels, int count)
    {
        lock (lockObj)
        {
            int off = (Math.Clamp(Universe, 1, 8) - 1) * 512;
            count = Math.Clamp(count - off, 1, 512);   // channels in use within this universe
            bool diff = count != used;
            for (int i = 1; i <= 512; i++)
            {
                byte v = off + i < channels.Length ? channels[off + i] : (byte)0;
                if (data[i] != v) { data[i] = v; diff = true; }
            }
            used = count;
            if (diff) dirty = true;
        }
    }

    // ------------------------------------------------------------------ loop

    void Loop(int gen)
    {
        long lastSend = long.MinValue / 2, lastFull = long.MinValue / 2;
        int failures = 0, attempt = 0;
        bool needFull = true, everConnected = false;
        string lastConnectError = null;

        while (run && gen == generation)
        {
            Beat();
            try
            {
            if (reconnectRequested)
            {
                reconnectRequested = false;
                if (recoverRequested)
                {
                    recoverRequested = false;
                    chunk = opt.MaxChunk; singleMode = false; cleanFrames = 0; haveSent = false;
                    if (dev is IResettableTransport r)
                    {
                        try { if (r.ResetDevice()) Log.Write("USB-poort van de uDMX gereset"); }
                        catch (Exception ex) { Log.Write("USB-reset niet mogelijk: " + ex.Message); }
                    }
                }
                Drop("opnieuw verbinden gevraagd");
                attempt = 0;
                Wait(opt.ReconnectMinMs);
            }

            if (dev == null)
            {
                try
                {
                    IUdmxTransport d = factory();
                    d.Open();
                    // replaced by the watchdog or stopped while Open() hung: this loop is done
                    if (gen != generation || !run) { try { d.Dispose(); } catch { } return; }
                    dev = d;
                    Interlocked.Increment(ref opens);
                    if (everConnected) Interlocked.Increment(ref reconnects);
                    everConnected = true;
                    connected = true;
                    connectedSince = DateTime.Now;
                    fault = null; error = null; lastConnectError = null;
                    needFull = true; failures = 0; attempt = 0; haveSent = false;
                    droppedJustNow = false; goneFromBus = false;
                    status = "Verbonden met uDMX";
                    Log.Write(reconnects > 0 ? "Opnieuw verbonden met uDMX (herverbonden: " + reconnects + ")" : "Verbonden met uDMX");
                }
                catch (Exception ex)
                {
                    UdmxFault f = ex is UdmxException ue ? ue.Fault : UdmxFault.Other;
                    connected = false;
                    fault = f.ToString();
                    error = ex.Message;
                    status = "Geen verbinding: " + ex.Message + " Ik probeer het opnieuw...";
                    // gone right after a working connection (Windows may need a moment before it reports "not found")
                    if (droppedJustNow && f == UdmxFault.NotFound)
                    {
                        goneFromBus = true;
                        lastConnectError = ex.Message;
                        Log.Write("uDMX is van de USB-bus verdwenen: Windows ziet hem niet meer (stekker, kabel, USB-poort, " +
                                  "stroom of een storing via de DMX-kabel). Opnieuw insteken helpt; ik maak dan vanzelf weer verbinding.");
                    }
                    else if (ex.Message != lastConnectError) { lastConnectError = ex.Message; Log.Write("Verbinden mislukt: " + ex.Message); }
                    if (f != UdmxFault.NotFound) goneFromBus = false;      // another fault: show that one
                    if (goneFromBus)
                    {
                        status = "De dongle is van de USB-bus verdwenen. Steek hem opnieuw in; ik maak vanzelf weer verbinding.";
                        error = status;
                    }
                    // keep waiting for the first "not found" while Windows is still busy with the device
                    if (f == UdmxFault.NotFound) droppedJustNow = false;
                    int wait = f == UdmxFault.NotFound ? opt.NotFoundPollMs
                             : Math.Min(opt.ReconnectMaxMs, opt.ReconnectMinMs << Math.Min(attempt++, 6));
                    Wait(wait);
                    continue;
                }
            }

            long now = clock.ElapsedMilliseconds;
            byte[] frame; int n; bool full;
            lock (lockObj)
            {
                bool keepAlive = now - lastFull >= opt.KeepAliveMs;
                bool due = dirty && now - lastSend >= MinIntervalMs;
                bool send = due || keepAlive || needFull;
                if (send) dirty = false;
                frame = send ? (byte[])data.Clone() : null;
                full = needFull || keepAlive;
                n = used;
            }

            if (frame != null)
            {
                IUdmxTransport d = dev;
                try
                {
                    SendFrame(d, frame, n, full);
                    if (gen != generation) { try { d.Dispose(); } catch { } return; }
                    lastSend = now;
                    if (full) lastFull = now;
                    needFull = false;
                    failures = 0;
                    Interlocked.Increment(ref frames);
                    lock (recentFrames) recentFrames.Enqueue(now);
                    status = "Verbonden met uDMX (" + (singleMode ? "kanaal voor kanaal" : "blokken van " + chunk) +
                             (reconnects > 0 ? ", herverbonden: " + reconnects : "") + ")";
                }
                catch (Exception ex)
                {
                    if (gen != generation) { try { d.Dispose(); } catch { } return; }   // replaced by the watchdog
                    UdmxFault f = ex is UdmxException ue ? ue.Fault : UdmxFault.Other;
                    NoteError(ex.Message);
                    lock (lockObj) dirty = true;
                    needFull = true;
                    cleanFrames = 0;
                    if (f is UdmxFault.Gone or UdmxFault.InUse or UdmxFault.NotFound || ++failures >= opt.FailuresBeforeReconnect)
                    {
                        fault = f.ToString();
                        error = ex.Message;
                        Drop(ex.Message);
                        Wait(opt.ReconnectMinMs);
                        continue;
                    }
                    Wait(opt.RetryDelayMs);
                    continue;
                }
            }
            Thread.Sleep(opt.TickMs);
            }
            catch (Exception ex) when (gen == generation)
            {
                // never let the send loop die: that would look like a dongle that silently stopped
                Log.Write("uDMX-verzendlus fout: " + ex.GetType().Name + ": " + ex.Message);
                Drop(ex.Message);
                Wait(opt.ReconnectMinMs);
            }
        }
        if (gen == generation || !run) Drop(null);   // stopped: close the dongle; replaced: leave it to the new loop
    }

    /// <summary>Set when a working connection is lost; the next attempt tells "gone from USB" apart from other faults.</summary>
    volatile bool droppedJustNow;
    /// <summary>The dongle left the USB bus after working: shown until it is back.</summary>
    volatile bool goneFromBus;

    void Drop(string reason)
    {
        if (dev != null)
        {
            try { dev.Dispose(); } catch { }
            dev = null;
            if (reason != null)
            {
                NoteDrop();
                droppedJustNow = true;
                string up = connectedSince is DateTime t ? " na " + Duration(DateTime.Now - t) : "";
                Log.Write("Verbinding met uDMX weg" + up + ": " + reason);
                status = "Verbinding verbroken - opnieuw verbinden...";
            }
        }
        connected = false;
        connectedSince = null;
    }

    void Wait(int ms)
    {
        long until = clock.ElapsedMilliseconds + ms;
        while (run && !reconnectRequested && clock.ElapsedMilliseconds < until) { Beat(); Thread.Sleep(Math.Min(opt.TickMs, 20)); }
    }

    /// <summary>While drops keep coming back, blocks stay small (safe mode); otherwise they may grow to the maximum.</summary>
    int MaxProbeChunk()
    {
        long now = clock.ElapsedMilliseconds;
        while (drops.Count > 0 && now - drops.Peek() > opt.DropWindowMs) drops.Dequeue();
        return drops.Count >= opt.DropsForSafeMode ? Math.Max(opt.MinChunk, opt.SafeChunk) : opt.MaxChunk;
    }

    /// <summary>Drops that keep coming back: send in smaller blocks, which many dongles handle more reliably.</summary>
    void NoteDrop()
    {
        long now = clock.ElapsedMilliseconds;
        drops.Enqueue(now);
        while (drops.Count > 0 && now - drops.Peek() > opt.DropWindowMs) drops.Dequeue();
        if (drops.Count >= opt.DropsForSafeMode && !singleMode && chunk > opt.SafeChunk)
        {
            chunk = Math.Max(opt.MinChunk, opt.SafeChunk);
            cleanFrames = 0;
            Log.Write("uDMX valt vaak weg (" + drops.Count + "x in " + opt.DropWindowMs / 60000 + " min): verder in kleinere blokken van " + chunk);
        }
    }

    void NoteError(string msg)
    {
        Interlocked.Increment(ref errors);
        lastErrorAt = DateTime.Now;
        lastErrorText = msg;
        Log.Write("USB-fout: " + msg + " (modus: " + (singleMode ? "kanaal voor kanaal" : "blok " + chunk) + ")");
    }

    static string Duration(TimeSpan d) =>
        d.TotalHours >= 1 ? $"{(int)d.TotalHours} u {d.Minutes} min" : d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes} min {d.Seconds} s" : $"{d.TotalSeconds:0.0} s";

    // ------------------------------------------------------------------ sending

    void SendFrame(IUdmxTransport dev, byte[] frame, int count, bool full)
    {
        count = Math.Clamp(count, 1, 512);
        int first = 1, last = count;
        bool onlyChanged = !full && haveSent;
        if (onlyChanged)
        {
            first = 0; last = 0;
            for (int i = 1; i <= count; i++)
                if (frame[i] != sent[i]) { if (first == 0) first = i; last = i; }
            if (first == 0) return; // nothing changed
        }

        // larger blocks again after a quiet period
        if (++cleanFrames >= opt.ProbeAfterFrames)
        {
            cleanFrames = 0;
            if (singleMode) { singleMode = false; chunk = opt.MinChunk; Log.Write("uDMX: probeer weer blokken"); }
            else if (chunk < MaxProbeChunk()) { chunk = Math.Min(MaxProbeChunk(), chunk * 2); Log.Write("uDMX: blokgrootte terug naar " + chunk); }
        }

        if (singleMode) SendSingles(dev, frame, first, last, onlyChanged);
        else SendRanges(dev, frame, first, last);

        Array.Copy(frame, first, sent, first, last - first + 1);
        haveSent = true;
    }

    void SendRanges(IUdmxTransport dev, byte[] frame, int first, int last)
    {
        int pos = first;
        while (pos <= last)
        {
            int len = Math.Min(chunk, last - pos + 1);
            Beat();
            try
            {
                dev.SetRange(pos - 1, frame, pos, len);
                pos += len;
                continue;
            }
            catch (UdmxException e) when (e.Fault is UdmxFault.Rejected or UdmxFault.Timeout or UdmxFault.Other)
            {
                Thread.Sleep(opt.RetryDelayMs);
                try
                {
                    dev.SetRange(pos - 1, frame, pos, len);
                    Interlocked.Increment(ref recovered);
                    pos += len;
                    continue;
                }
                catch (UdmxException e2) when (e2.Fault is UdmxFault.Rejected or UdmxFault.Timeout)
                {
                    NoteError(e2.Message);
                    cleanFrames = 0;
                    if (chunk > opt.MinChunk)
                    {
                        chunk = Math.Max(opt.MinChunk, chunk / 2);
                        Log.Write("uDMX: blokgrootte verlaagd naar " + chunk);
                        continue; // same position, smaller block
                    }
                    if (e2.Fault == UdmxFault.Rejected)
                    {
                        singleMode = true;
                        Log.Write("uDMX weigert blokken, over op kanaal voor kanaal");
                        SendSingles(dev, frame, pos, last, false);
                        return;
                    }
                    throw;
                }
            }
        }
    }

    void SendSingles(IUdmxTransport dev, byte[] frame, int first, int last, bool onlyChanged)
    {
        for (int ch = first; ch <= last; ch++)
        {
            if (onlyChanged && frame[ch] == sent[ch]) continue;
            Beat();
            try { dev.SetSingle(ch - 1, frame[ch]); }
            catch (UdmxException e) when (e.Fault is UdmxFault.Rejected or UdmxFault.Timeout or UdmxFault.Other)
            {
                Thread.Sleep(opt.RetryDelayMs);
                dev.SetSingle(ch - 1, frame[ch]);
                Interlocked.Increment(ref recovered);
            }
        }
    }
}
