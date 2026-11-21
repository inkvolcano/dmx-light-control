namespace DmxLicht.Core.Audio;

/// <summary>
/// A steady musical clock: "how many beats since the start" at any moment.
/// It free-runs at <see cref="Bpm"/> and is pulled into line by taps (hard) and detected beats (gently),
/// so effects keep stepping evenly even when the detector misses or doubles a beat.
/// Times are milliseconds on the caller's clock.
/// </summary>
public sealed class BeatClock
{
    readonly object sync = new();
    double bpm = 120;
    double anchorMs, anchorBeat;
    readonly List<double> taps = new();
    double lastVirtual;
    double lastAudioBeatMs = double.NegativeInfinity;

    public double Bpm { get { lock (sync) return bpm; } }

    /// <summary>When the last beat from the audio input arrived (to show "following the music").</summary>
    public double LastAudioBeatMs { get { lock (sync) return lastAudioBeatMs; } }

    double Period => 60000.0 / bpm;

    /// <summary>Beats since the start (fractional) at time <paramref name="ms"/>.</summary>
    public double Position(double ms) { lock (sync) return anchorBeat + (ms - anchorMs) / Period; }

    public void SetBpm(double value, double nowMs)
    {
        lock (sync)
        {
            value = Math.Clamp(value, 30, 300);
            anchorBeat = anchorBeat + (nowMs - anchorMs) / Period; // keep position continuous
            anchorMs = nowMs;
            bpm = value;
        }
    }

    /// <summary>Hard-sets tempo and position (a playing timeline drives the clock this way).</summary>
    public void Sync(double nowMs, double beats, double newBpm)
    {
        lock (sync)
        {
            bpm = Math.Clamp(newBpm, 30, 300);
            anchorBeat = beats;
            anchorMs = nowMs;
        }
    }

    /// <summary>A tap: sets the tempo from recent taps and puts a beat exactly here.</summary>
    public void Tap(double nowMs)
    {
        lock (sync)
        {
            if (taps.Count > 0 && nowMs - taps[^1] > 2000) taps.Clear();
            taps.Add(nowMs);
            if (taps.Count > 8) taps.RemoveAt(0);
            if (taps.Count >= 2)
                bpm = Math.Clamp(60000.0 * (taps.Count - 1) / (taps[^1] - taps[0]), 30, 300);
            anchorBeat = Math.Round(anchorBeat + (nowMs - anchorMs) / Period);
            anchorMs = nowMs;
        }
    }

    /// <summary>A beat from the audio analyser, with its tempo estimate (0 = unknown).</summary>
    public void AudioBeat(double nowMs, double estimatedBpm)
    {
        lock (sync)
        {
            lastAudioBeatMs = nowMs;
            if (estimatedBpm >= 30) bpm = bpm * 0.7 + estimatedBpm * 0.3;
            double pos = anchorBeat + (nowMs - anchorMs) / Period;
            double err = pos - Math.Round(pos);           // -0.5..0.5 beats
            if (Math.Abs(err) < 0.35) pos -= err * 0.5;   // nudge halfway towards the detected beat
            anchorBeat = pos;
            anchorMs = nowMs;
        }
    }

    /// <summary>
    /// Effect time that advances <paramref name="stepsPerBeat"/> steps of <paramref name="stepMs"/> per beat.
    /// Never goes backwards (a phase correction just slows it for a moment).
    /// </summary>
    public long VirtualMs(double nowMs, int stepMs, double stepsPerBeat)
    {
        double perBeat = stepsPerBeat * Math.Max(1, stepMs);
        double v = Position(nowMs) * perBeat;
        lock (sync)
        {
            // hold through small backward corrections; accept big jumps (step length changed)
            if (v < lastVirtual && lastVirtual - v < perBeat) v = lastVirtual;
            lastVirtual = v;
        }
        return (long)v;
    }
}
