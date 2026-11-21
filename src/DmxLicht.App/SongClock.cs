using System.Diagnostics;
using DmxLicht.Core;
using DmxLicht.Core.Engine;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DmxLicht.App;

/// <summary>
/// Plays a song (mp3, wav, m4a... via Media Foundation) and is the timeline's clock while it plays.
/// The reader position moves in buffer-sized jumps, so the position is interpolated with a stopwatch
/// and corrected for the output latency.
/// </summary>
sealed class SongClock : ITimelineClock
{
    const int LatencyMs = 100;
    readonly MediaFoundationReader reader;
    readonly WasapiOut output;
    readonly Stopwatch since = new();
    readonly object sync = new();
    double lastReaderMs = -1, anchorMs;

    public SongClock(string path)
    {
        reader = new MediaFoundationReader(path);
        output = new WasapiOut(AudioClientShareMode.Shared, LatencyMs);
        output.Init(reader);
    }

    public double LengthMs => reader.TotalTime.TotalMilliseconds;
    public bool Playing => output.PlaybackState == PlaybackState.Playing;

    public double PositionMs
    {
        get
        {
            lock (sync)
            {
                double r = reader.CurrentTime.TotalMilliseconds;
                if (!Playing) return Math.Max(0, r - (lastReaderMs < 0 ? 0 : LatencyMs));
                if (r != lastReaderMs) { lastReaderMs = r; anchorMs = r - LatencyMs; since.Restart(); }
                // never run more than one buffer ahead of what the reader has delivered
                return Math.Max(0, Math.Min(anchorMs + since.Elapsed.TotalMilliseconds, r));
            }
        }
    }

    public void Play() { output.Play(); }
    public void Pause() { output.Pause(); }

    public void Seek(double ms)
    {
        lock (sync)
        {
            reader.CurrentTime = TimeSpan.FromMilliseconds(Math.Clamp(ms, 0, LengthMs));
            lastReaderMs = -1;
        }
    }

    public void Dispose()
    {
        try { output.Stop(); } catch { }
        output.Dispose();
        reader.Dispose();
    }

    /// <summary>Song if the timeline has one (and it opens), else a stopwatch.</summary>
    public static ITimelineClock For(DmxLicht.Core.Show.Timeline t, string audioDir)
    {
        if (string.IsNullOrEmpty(t.AudioFile)) return new StopwatchClock();
        string path = Path.Combine(audioDir, t.AudioFile);
        try { return new SongClock(path); }
        catch (Exception ex)
        {
            Log.Write("Liedje niet af te spelen (" + t.AudioFile + "): " + ex.Message);
            return new StopwatchClock();
        }
    }

    /// <summary>Peak per bucket (0..1) for drawing the waveform, plus the length.</summary>
    public static (double lengthMs, float[] peaks) Peaks(string path, int buckets)
    {
        using var r = new MediaFoundationReader(path);
        ISampleProvider sp = r.ToSampleProvider();
        int ch = sp.WaveFormat.Channels;
        long totalFrames = (long)(r.TotalTime.TotalSeconds * sp.WaveFormat.SampleRate);
        long perBucket = Math.Max(1, totalFrames / buckets);
        var peaks = new float[buckets];
        var buf = new float[sp.WaveFormat.SampleRate * ch];
        long frame = 0;
        int n;
        while ((n = sp.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i += ch, frame++)
            {
                int b = (int)Math.Min(buckets - 1, frame / perBucket);
                float v = Math.Abs(buf[i]);
                if (v > peaks[b]) peaks[b] = v;
            }
        }
        float max = Math.Max(0.01f, peaks.Max());
        for (int i = 0; i < peaks.Length; i++) peaks[i] = MathF.Round(peaks[i] / max, 2);
        return (r.TotalTime.TotalMilliseconds, peaks);
    }
}
