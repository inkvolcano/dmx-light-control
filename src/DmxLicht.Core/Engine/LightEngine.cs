using System.Diagnostics;
using DmxLicht.Core.Audio;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Output;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Core.Engine;

/// <summary>
/// Owns the show and runs the output loop (~40 frames/s). All reads and writes of
/// <see cref="Show"/> go through <see cref="Read{T}"/> / <see cref="Change"/>, which take the lock.
/// </summary>
public sealed class LightEngine : IDisposable
{
    readonly object sync = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly IDmxOutput output;
    Thread thread;
    volatile bool run;
    byte[] lastFrame = new byte[513];

    public ShowFile Show { get; }
    public FixtureLibrary Library { get; private set; }
    public Rig Rig { get; private set; }
    public IDmxOutput Output => output;
    public string FixturesFolder { get; }

    /// <summary>Plays timelines; driven from the output loop.</summary>
    public TimelinePlayer Timeline { get; private set; } = new();

    /// <summary>Use a different clock source for timelines (the app plays the song).</summary>
    public void UseTimelineClock(Func<Timeline, ITimelineClock> factory) => Timeline = new TimelinePlayer(factory);

    /// <summary>Runs <paramref name="action"/> on the timeline player under the engine lock.</summary>
    public void WithTimeline(Action<TimelinePlayer, ShowFile> action)
    {
        lock (sync) action(Timeline, Show);
        Changed?.Invoke();
    }

    /// <summary>Musical clock fed by taps, manual BPM and detected beats.</summary>
    public BeatClock Beat { get; } = new();

    volatile AudioSnapshot audio;
    /// <summary>Latest levels from the audio input (set by the app's audio capture).</summary>
    public void SetAudio(AudioLevels levels) => audio = new AudioSnapshot(levels, clock.ElapsedMilliseconds);
    /// <summary>Levels, or silence when no audio arrived recently (loopback sends nothing during silence).</summary>
    public AudioLevels Audio
    {
        get
        {
            AudioSnapshot a = audio;
            return a != null && clock.ElapsedMilliseconds - a.AtMs < 400 ? a.Levels : new AudioLevels(0, 0, 0, 0, a?.Levels.Bpm ?? 0, a?.Levels.Beats ?? 0);
        }
    }
    sealed record AudioSnapshot(AudioLevels Levels, long AtMs);

    /// <summary>Raised (outside the lock) after any change; the host saves and pushes state to the UI.</summary>
    public event Action Changed;

    public LightEngine(ShowFile show, IDmxOutput output, string fixturesFolder)
    {
        Show = show;
        this.output = output;
        FixturesFolder = fixturesFolder;
        Library = FixtureLibrary.Load(fixturesFolder);
        Rig = Rig.Build(show.Patch, Library, show.Groups);
    }

    public long Ms => clock.ElapsedMilliseconds;

    public T Read<T>(Func<ShowFile, T> read) { lock (sync) return read(Show); }

    // crossfade: the output fades from what fadeFrom would show to what the state shows now
    ShowState fadeFrom;        // the old look keeps animating during the fade...
    byte[] fadeFromFrame;      // ...unless a fade was already running: then we start from the light as it is now
    long fadeStart;
    int fadeMs;

    // ---------- undo / redo: snapshots of the show content ----------
    readonly LinkedList<ShowContent> undo = new();
    readonly Stack<ShowContent> redo = new();
    long lastUndoPush = long.MinValue / 2;
    const int UndoLimit = 60, UndoCoalesceMs = 800;

    public int UndoCount { get { lock (sync) return undo.Count; } }
    public int RedoCount { get { lock (sync) return redo.Count; } }

    void PushUndo()
    {
        redo.Clear();
        long now = clock.ElapsedMilliseconds;
        if (now - lastUndoPush < UndoCoalesceMs) { lastUndoPush = now; return; } // a drag = one step
        lastUndoPush = now;
        undo.AddLast(ShowContent.From(Show));
        if (undo.Count > UndoLimit) undo.RemoveFirst();
    }

    public bool Undo() => Step(undo, redo);
    public bool Redo() => Step(null, null);

    bool Step(LinkedList<ShowContent> from, Stack<ShowContent> to)
    {
        lock (sync)
        {
            if (from != null)
            {
                if (undo.Count == 0) return false;
                ShowContent prev = undo.Last!.Value;
                undo.RemoveLast();
                redo.Push(ShowContent.From(Show));
                var temp = Show.State.Layers.Where(l => l.Source != null).ToList();   // timeline/flash keep running
                prev.ApplyTo(Show);
                Show.State.Layers.AddRange(temp);
            }
            else
            {
                if (redo.Count == 0) return false;
                undo.AddLast(ShowContent.From(Show));
                var temp = Show.State.Layers.Where(l => l.Source != null).ToList();
                redo.Pop().ApplyTo(Show);
                Show.State.Layers.AddRange(temp);
            }
            lastUndoPush = long.MinValue / 2;
            Rig = Rig.Build(Show.Patch, Library, Show.Groups);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Replaces the whole show (open a show file, restore a backup); can be undone.</summary>
    public void Replace(ShowContent content)
    {
        lock (sync)
        {
            PushUndo();
            lastUndoPush = long.MinValue / 2;
            Timeline.Stop();
            content.ApplyTo(Show);
            Rig = Rig.Build(Show.Patch, Library, Show.Groups);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies a change. Set <paramref name="patchChanged"/> when the patch, groups or library changed.
    /// With <paramref name="fade"/> &gt; 0 the lamps fade to the new look over that many ms.
    /// <paramref name="undoable"/> = false for things that are not part of the show (settings, strobe, identify).
    /// </summary>
    public void Change(Action<ShowFile> change, bool patchChanged = false, int fade = 0, bool undoable = true, bool cut = false)
    {
        lock (sync)
        {
            if (undoable) PushUndo();
            if (fade > 0) StartFade(fade);
            // a deliberate change of the look without a fade (blackout, a scene without fade, ...) is immediate:
            // it must not stay hidden behind a long fade that is still running (e.g. a 30-minute schedule fade)
            else if (cut) { fadeFrom = null; fadeFromFrame = null; }
            change(Show);
            if (patchChanged) Rig = Rig.Build(Show.Patch, Library, Show.Groups);
        }
        Changed?.Invoke();
    }

    public void ReloadLibrary()
    {
        lock (sync)
        {
            Library = FixtureLibrary.Load(FixturesFolder);
            Rig = Rig.Build(Show.Patch, Library, Show.Groups);
        }
        Changed?.Invoke();
    }

    /// <summary>Latest frame and per-head preview colours (for the UI).</summary>
    public (byte[] frame, string[] heads) Snapshot()
    {
        lock (sync) return ((byte[])lastFrame.Clone(), Preview.HeadColors(Rig, lastFrame));
    }

    public void Start()
    {
        if (run) return;
        run = true;
        output.Start();
        thread = new Thread(Loop) { IsBackground = true, Name = "DMX engine" };
        thread.Start();
    }

    void Loop()
    {
        string lastErr = null;
        while (run)
        {
            try
            {
                byte[] f; int used;
                lock (sync)
                {
                    long now = clock.ElapsedMilliseconds;
                    if (Timeline.Tick(Show, StartFade)) timelineChanged = true;
                    ExpireFlash(now);
                    if (Timeline.Playing)
                    {
                        Show.Timeline tl = Show.Timelines.FirstOrDefault(x => x.Id == Timeline.TimelineId);
                        if (tl != null && tl.Bpm > 0) Beat.Sync(now, Timeline.PositionMs * tl.Bpm / 60000.0, tl.Bpm);
                    }
                    MusicSettings m = Show.State.Music;
                    long fxMs = m.SyncEffects ? Beat.VirtualMs(now, Show.State.Effect.StepMs, m.StepsPerBeat) : now;
                    double scale = MusicScale(m, now);
                    AudioFrame af = NextAudioFrame(now);
                    f = FrameBuilder.Build(Rig, Show.State, fxMs, scale, af);
                    if (fadeFrom != null || fadeFromFrame != null)
                    {
                        double t = (now - fadeStart) / (double)Math.Max(1, fadeMs);
                        if (t >= 1) { fadeFrom = null; fadeFromFrame = null; }
                        else f = Mix(fadeFromFrame ?? FrameBuilder.Build(Rig, fadeFrom, fxMs, scale, af), f, t);
                    }
                    f = ApplyTest(f, now);
                    used = Math.Max(Rig.UsedChannelCount, test?.Channels.Keys.DefaultIfEmpty(0).Max() ?? 0);
                    lastFrame = f;
                }
                output.SetFrame(f, used);
                // listeners (autosave, UI push, audio settings) run off the render thread so they never hold up the light
                if (timelineChanged) { timelineChanged = false; ThreadPool.QueueUserWorkItem(_ => Changed?.Invoke()); }
            }
            catch (Exception ex)
            {
                if (ex.Message != lastErr) { lastErr = ex.Message; Log.Write("FOUT in engine: " + ex); }
            }
            Thread.Sleep(25);
        }
    }

    bool timelineChanged;

    // ---------- flash: a temporary colour on all fixtures (notifications) ----------
    const string FlashSource = "flash";
    long flashUntil;

    /// <summary>All fixtures blink (or show solid) <paramref name="color"/> for <paramref name="ms"/>, then the look returns.</summary>
    public void Flash(System.Drawing.Color color, int ms, bool solid)
    {
        lock (sync)
        {
            Show.State.Layers.RemoveAll(l => l.Source == FlashSource);
            var e = new Effects.EffectSettings
            {
                EffectIndex = Effects.EffectLibrary.IndexOf(solid ? "gradient-ab" : "blink"),
                ColorA = color, ColorB = color, OnMs = 250, OffMs = 250,
            };
            Show.State.Layers.Add(new EffectLayer { GroupId = "*", Source = FlashSource, Effect = e });
            flashUntil = clock.ElapsedMilliseconds + ms;
        }
        Changed?.Invoke();
    }

    void ExpireFlash(long now)
    {
        if (flashUntil != 0 && now < flashUntil) return;
        flashUntil = 0;
        // also clears a flash layer that came back via undo, a backup or show.json
        if (Show.State.Layers.RemoveAll(l => l.Source == FlashSource) > 0) timelineChanged = true;
    }

    // ---------- test page: hand-set channels and "lamp by lamp" ----------
    sealed record TestState(IReadOnlyDictionary<int, byte> Channels, bool Exclusive, int WalkMs);
    volatile TestState test;
    long walkStart;

    /// <summary>Channels set by hand on the test page (1..512 -> value). Exclusive = everything else 0.</summary>
    public void SetTest(IReadOnlyDictionary<int, byte> channels, bool exclusive)
    {
        TestState t = test;
        test = channels.Count == 0 && (t == null || t.WalkMs == 0) ? null : new TestState(channels, exclusive, t?.WalkMs ?? 0);
    }

    /// <summary>Lights one fixture after the other in white, <paramref name="intervalMs"/> each; 0 = off.</summary>
    public void SetWalk(int intervalMs)
    {
        TestState t = test;
        walkStart = clock.ElapsedMilliseconds;
        if (intervalMs <= 0) test = t == null || t.Channels.Count == 0 ? null : t with { WalkMs = 0 };
        else test = (t ?? new TestState(new Dictionary<int, byte>(), false, 0)) with { WalkMs = Math.Max(300, intervalMs) };
    }

    public void ClearTest() => test = null;

    /// <summary>Test page state for the UI: channels, exclusive, and which fixture the walk shows.</summary>
    public (IReadOnlyDictionary<int, byte> channels, bool exclusive, string walkFixture) TestInfo()
    {
        TestState t = test;
        if (t == null) return (new Dictionary<int, byte>(), false, null);
        string walk = null;
        if (t.WalkMs > 0)
            lock (sync)
            {
                int n = Rig.Fixtures.Count;
                if (n > 0) walk = Rig.Fixtures[(int)((clock.ElapsedMilliseconds - walkStart) / t.WalkMs % n)].Patch.Id;
            }
        return (t.Channels, t.Exclusive, walk);
    }

    byte[] ApplyTest(byte[] f, long now)
    {
        TestState t = test;
        if (t == null) return f;
        if (t.WalkMs > 0 && Rig.Fixtures.Count > 0)
        {
            string id = Rig.Fixtures[(int)((now - walkStart) / t.WalkMs % Rig.Fixtures.Count)].Patch.Id;
            var only = new ShowState { BrightnessPercent = 100 };
            only.Looks[id] = new FixtureLook { On = true, Color = ColorRef.Palette("white") };
            f = FrameBuilder.Build(Rig, only, now);
        }
        else if (t.Exclusive) f = new byte[f.Length];
        foreach (var (ch, v) in t.Channels) if (ch >= 1 && ch < f.Length) f[ch] = v;
        return f;
    }

    /// <summary>Starts a fade from the current look (call inside the lock, before changing the state).</summary>
    void StartFade(int fade)
    {
        bool fading = fadeFrom != null || fadeFromFrame != null;
        fadeFromFrame = fading ? (byte[])lastFrame.Clone() : null;
        fadeFrom = fading ? null : SceneOps.Clone(Show.State);
        fadeStart = clock.ElapsedMilliseconds;
        fadeMs = fade;
    }

    /// <summary>Channel-wise blend from frame a to b (t = 0..1), eased.</summary>
    internal static byte[] Mix(byte[] a, byte[] b, double t)
    {
        t = t * t * (3 - 2 * t);
        var r = new byte[b.Length];   // the patch can gain a universe during a fade
        for (int i = 1; i < b.Length; i++) { int av = i < a.Length ? a[i] : 0; r[i] = (byte)Math.Round(av + (b[i] - av) * t); }
        return r;
    }

    // ---------- sound for sound-reactive effects ----------
    double sLevel, sBass, sMid, sHigh, avgLevel;
    long surgeUntil;

    /// <summary>The current sound for effects: fast attack, ~120 ms release, plus "surge" (sudden loudness, a drop).</summary>
    public AudioFrame LastAudioFrame { get; private set; }

    AudioFrame NextAudioFrame(long now)
    {
        AudioLevels a = Audio;
        const double release = 0.82;   // per 25 ms frame
        sLevel = Math.Max(a.Level, sLevel * release);
        sBass = Math.Max(a.Bass, sBass * release);
        sMid = Math.Max(a.Mid, sMid * release);
        sHigh = Math.Max(a.High, sHigh * release);
        avgLevel += (sLevel - avgLevel) * 0.008;          // ~3 s average
        if (sLevel > 0.65 && sLevel > avgLevel * 1.6 && avgLevel > 0.02) surgeUntil = now + 500;
        return LastAudioFrame = new AudioFrame(sLevel, sBass, sMid, sHigh, Beat.Position(now), now < surgeUntil);
    }

    /// <summary>Master factor from the sound input: floor + (1 - floor) x signal.</summary>
    double MusicScale(MusicSettings m, long now)
    {
        if (m.Modulation == AudioModulation.None) return 1.0;
        double v;
        if (m.Modulation == AudioModulation.BeatPulse)
        {
            double phase = Beat.Position(now);
            phase -= Math.Floor(phase);
            v = Math.Exp(-phase * 5);         // flash on the beat, fade before the next
        }
        else
        {
            if (!m.Enabled) return 1.0;
            AudioLevels a = Audio;
            v = m.Modulation == AudioModulation.Bass ? a.Bass : a.Level;
        }
        double floor = Math.Clamp(m.FloorPercent, 0, 100) / 100.0;
        return floor + (1 - floor) * v;
    }

    /// <summary>Sends an all-zero frame, waits for it to go out, then stops.</summary>
    public void Dispose()
    {
        run = false;
        thread?.Join(1000);
        try { output.SetFrame(new byte[Rig.FrameSize], Rig.UsedChannelCount); Thread.Sleep(250); } catch { }
        output.Stop();
    }
}
