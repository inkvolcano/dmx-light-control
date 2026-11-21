using System.Diagnostics;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Core.Engine;

/// <summary>Where the timeline is: a stopwatch, or the position of the song that is playing.</summary>
public interface ITimelineClock : IDisposable
{
    double PositionMs { get; }
    bool Playing { get; }
    /// <summary>Length of the loaded audio, or 0.</summary>
    double LengthMs { get; }
    void Play();
    void Pause();
    void Seek(double ms);
}

public sealed class StopwatchClock : ITimelineClock
{
    readonly Stopwatch sw = new();
    double offset;
    public double PositionMs => offset + sw.Elapsed.TotalMilliseconds;
    public bool Playing => sw.IsRunning;
    public double LengthMs => 0;
    public void Play() => sw.Start();
    public void Pause() => sw.Stop();
    public void Seek(double ms) { offset = ms; sw.Reset(); if (Playing) sw.Start(); }
    public void Dispose() { }
}

/// <summary>
/// Plays a <see cref="Timeline"/>: scene clips are applied when their start is crossed (with their fade),
/// effect clips become group layers for as long as they last. Only touches layers it created itself,
/// so manual group effects keep running. <see cref="Tick"/> runs inside the engine lock, every frame.
/// </summary>
public sealed class TimelinePlayer
{
    public const string Source = "timeline";

    readonly Func<Timeline, ITimelineClock> clockFactory;
    ITimelineClock clock;
    string appliedScene;          // clip id of the scene last applied
    string appliedEffects = "";   // signature of the active effect clips; null = rebuild (also removes old layers)
    bool stopRequested;
    bool wasPlaying;

    public string TimelineId { get; private set; }
    /// <summary>The wait point playback is paused at (GO continues), or null.</summary>
    public TimelineMarker WaitingAt { get; private set; }
    public bool Playing => clock?.Playing == true;
    public double PositionMs => clock?.PositionMs ?? lastPosition;
    double lastPosition;

    public TimelinePlayer(Func<Timeline, ITimelineClock> clockFactory = null)
    {
        this.clockFactory = clockFactory ?? (_ => new StopwatchClock());
    }

    static double RegionEnd(Timeline t, double length) => Math.Min(t.EndMs is double e && e > t.StartMs ? e : length, length);

    /// <summary>
    /// Plays <paramref name="t"/>: from <paramref name="fromMs"/>, else from where it is (also = GO after a wait point),
    /// else from the start point when it is outside the start..stop region.
    /// </summary>
    public void Play(Timeline t, double? fromMs = null)
    {
        if (TimelineId != t.Id || clock == null)
        {
            clock?.Dispose();
            clock = clockFactory(t);
            TimelineId = t.Id;
            appliedScene = null; appliedEffects = null;   // first tick clears effects of a previous timeline
            Seek(fromMs ?? t.StartMs);
        }
        else if (fromMs is double f) Seek(f);
        else
        {
            // at (or just before) the end: start again; a song stops a little before its nominal length
            double pos = clock.PositionMs, end = EffectiveEnd(t);
            if (pos < t.StartMs - 1 || pos >= end - 300) Seek(t.StartMs);
        }
        WaitingAt = null;
        clock.Play();
        stopRequested = false;
    }

    public void Pause() { clock?.Pause(); wasPlaying = false; }

    /// <summary>
    /// Ends a timeline without the stop handling (for a changed song): the clock is closed now, so the
    /// next <see cref="Play"/> opens the new song instead of seeking in the old one.
    /// </summary>
    public void Unload()
    {
        clock?.Dispose();
        clock = null;
        TimelineId = null;
        wasPlaying = false;
    }

    /// <summary>Where playback really ends: the stop point, or the end of the song when that comes first.</summary>
    double EffectiveEnd(Timeline t)
    {
        double len = clock?.LengthMs ?? 0;
        double end = RegionEnd(t, Math.Max(t.LengthMs, len));
        return len > 0 ? Math.Min(end, len) : end;
    }

    public void Seek(double ms)
    {
        clock?.Seek(Math.Max(0, ms));
        lastPosition = Math.Max(0, ms);
        appliedScene = null; // re-evaluate which scene belongs here
        WaitingAt = null;
    }

    /// <summary>The points ◀/▶ jump between: start, wait/cue points, stop.</summary>
    static List<double> Points(Timeline t, double length) =>
        t.Markers.Select(m => m.AtMs).Append(t.StartMs).Append(RegionEnd(t, length)).Where(p => p >= 0).Distinct().OrderBy(p => p).ToList();

    /// <summary>Jump to the next point (keeps playing if it was).</summary>
    public void Next(Timeline t)
    {
        double pos = PositionMs, length = Math.Max(t.LengthMs, clock?.LengthMs ?? 0);
        double? next = Points(t, length).Cast<double?>().FirstOrDefault(p => p > pos + 50);
        if (next is double n) { EnsureLoaded(t); Seek(n); }
    }

    /// <summary>Jump to the previous point; within 1 s after a point, to the one before it.</summary>
    public void Previous(Timeline t)
    {
        double pos = PositionMs, length = Math.Max(t.LengthMs, clock?.LengthMs ?? 0);
        double? prev = Points(t, length).Cast<double?>().LastOrDefault(p => p < pos - 1000);
        EnsureLoaded(t);
        Seek(prev ?? t.StartMs);
    }

    void EnsureLoaded(Timeline t)
    {
        if (TimelineId == t.Id && clock != null) return;
        clock?.Dispose();
        clock = clockFactory(t);
        TimelineId = t.Id;
        appliedScene = null; appliedEffects = null;
        stopRequested = false;
    }

    /// <summary>Stops and removes the timeline's effects (the last scene stays); the next Play starts at the start point.</summary>
    public void Stop()
    {
        clock?.Pause();
        lastPosition = 0;
        WaitingAt = null;
        stopRequested = true;
    }

    /// <summary>Call every frame inside the engine lock. Returns true when it changed the state.</summary>
    public bool Tick(ShowFile show, Action<int> startFade)
    {
        if (stopRequested)
        {
            stopRequested = false;
            appliedScene = null; appliedEffects = "";
            int removed = show.State.Layers.RemoveAll(l => l.Source == Source);
            clock?.Dispose(); clock = null; TimelineId = null;
            return removed > 0;
        }
        if (clock == null || TimelineId == null) return false;
        Timeline t = show.Timelines.FirstOrDefault(x => x.Id == TimelineId);
        if (t == null) { Stop(); return false; }

        double pos = clock.PositionMs;
        double length = Math.Max(t.LengthMs, clock.LengthMs);
        double start = Math.Clamp(t.StartMs, 0, length), end = EffectiveEnd(t);
        // the song ran out by itself (the audio player stops at the end of the file): handle it as the end
        if (!clock.Playing && wasPlaying && WaitingAt == null && clock.LengthMs > 0 && pos >= end - 300)
        {
            if (t.Loop) { clock.Seek(start); clock.Play(); pos = start; appliedScene = null; }
            else pos = end;
        }
        if (clock.Playing)
        {
            // a wait point that was just passed: stop exactly there until GO (not when jumping over it)
            TimelineMarker wait = t.Markers.Where(m => m.Kind == "wait" && lastPosition < m.AtMs && pos >= m.AtMs && pos - m.AtMs < 1000)
                .OrderBy(m => m.AtMs).FirstOrDefault();
            if (wait != null)
            {
                clock.Pause();
                clock.Seek(wait.AtMs);
                pos = wait.AtMs;
                WaitingAt = wait;
            }
            else if (pos >= end)
            {
                if (t.Loop) { clock.Seek(start); pos = start; appliedScene = null; }
                else { clock.Pause(); clock.Seek(end); pos = end; }
            }
        }
        wasPlaying = clock.Playing;
        lastPosition = pos;
        bool changed = false;

        // scene: the last scene clip that has started (on any scene track)
        TimelineClip sceneClip = t.Tracks.Where(k => k.Kind == "scenes").SelectMany(k => k.Clips)
            .Where(c => c.Kind == ClipKind.Scene && c.StartMs <= pos).OrderBy(c => c.StartMs).LastOrDefault();
        if (sceneClip != null && sceneClip.Id != appliedScene)
        {
            Scene sc = show.Scenes.FirstOrDefault(s => s.Id == sceneClip.SceneId);
            appliedScene = sceneClip.Id;
            if (sc != null)
            {
                int fade = sceneClip.FadeMs >= 0 ? sceneClip.FadeMs : sc.FadeMs;
                // jumping into the middle of a clip (seek) should not fade from far away
                if (pos - sceneClip.StartMs > Math.Max(fade, 200)) fade = 0;
                if (fade > 0) startFade(fade);
                List<EffectLayer> mine = show.State.Layers.Where(l => l.Source == Source).ToList();
                SceneOps.Apply(sc.State, show.State);
                show.State.Layers.AddRange(mine);
                show.State.ActiveScene = sc.Id;
                changed = true;
            }
        }

        // effects: one layer per active effect clip, in track order
        var active = t.Tracks.Where(k => k.Kind == "effects")
            .Select(k => (track: k, clip: k.Clips.FirstOrDefault(c => c.Kind == ClipKind.Effect && c.StartMs <= pos && pos < c.EndMs && c.Effect != null)))
            .Where(x => x.clip != null).ToList();
        string signature = string.Join("|", active.Select(a => a.track.Id + ":" + a.clip.Id));
        if (signature != appliedEffects)
        {
            appliedEffects = signature;
            show.State.Layers.RemoveAll(l => l.Source == Source);
            foreach (var (track, clip) in active)
                show.State.Layers.Add(new EffectLayer { GroupId = track.Target ?? "*", Effect = SceneOps.Clone(clip.Effect), Source = Source });
            changed = true;
        }
        return changed;
    }
}
