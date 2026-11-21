using DmxLicht.Core.Audio;

namespace DmxLicht.Tests;

public class AudioTests
{
    const int Rate = 48000;

    /// <summary>Synthetic dance loop: a kick (decaying 55 Hz sine) on every beat, a hi-hat (noise) on every off-beat, plus noise.</summary>
    static float[] Music(double bpm, double seconds, double volume = 0.8, int seed = 1)
    {
        var r = new Random(seed);
        int n = (int)(seconds * Rate);
        var x = new float[n];
        double beat = 60.0 / bpm;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Rate;
            double sinceKick = t % beat;
            double sinceHat = (t + beat / 2) % beat;
            double kick = Math.Sin(2 * Math.PI * 55 * sinceKick) * Math.Exp(-sinceKick / 0.09);
            double hat = (r.NextDouble() * 2 - 1) * Math.Exp(-sinceHat / 0.02) * 0.25;
            double noise = (r.NextDouble() * 2 - 1) * 0.02;
            x[i] = (float)(volume * (kick + hat + noise));
        }
        return x;
    }

    static AudioAnalyzer Feed(float[] samples, out List<double> beats)
    {
        var a = new AudioAnalyzer(Rate);
        var list = new List<double>();
        a.Beat += t => list.Add(t);
        for (int i = 0; i < samples.Length; i += 960) // 20 ms blocks, like a capture callback
            a.Process(samples.AsSpan(i, Math.Min(960, samples.Length - i)));
        beats = list;
        return a;
    }

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(128)]
    [InlineData(140)]
    public void FindsTheTempo(double bpm)
    {
        AudioAnalyzer a = Feed(Music(bpm, 12), out List<double> beats);
        Assert.InRange(a.Levels.Bpm, bpm - 2, bpm + 2);
        double expected = 12 * bpm / 60;
        Assert.InRange(beats.Count, expected * 0.8, expected * 1.1);
    }

    [Fact]
    public void QuietMusicWorksThanksToAutoGain()
    {
        AudioAnalyzer a = Feed(Music(120, 12, volume: 0.02), out _);
        Assert.InRange(a.Levels.Bpm, 118, 122);
    }

    [Fact]
    public void SilenceGivesNoBeats()
    {
        var silence = new float[Rate * 5];
        var r = new Random(3);
        for (int i = 0; i < silence.Length; i++) silence[i] = (float)((r.NextDouble() * 2 - 1) * 0.0005);
        Feed(silence, out List<double> beats);
        Assert.True(beats.Count <= 2, $"{beats.Count} beats in near-silence");
    }

    [Fact]
    public void BassFollowsTheKick()
    {
        var a = new AudioAnalyzer(Rate);
        float[] m = Music(120, 4);
        a.Process(m.AsSpan(0, (int)(Rate * 3.5)));          // ... up to a kick at 3.5 s
        a.Process(m.AsSpan((int)(Rate * 3.5), Rate / 50)); // 20 ms into the kick
        double onKick = a.Levels.Bass;
        a.Process(m.AsSpan((int)(Rate * 3.52), (int)(Rate * 0.36))); // late in the beat
        Assert.True(onKick > a.Levels.Bass * 2, $"bass on kick {onKick:0.00}, later {a.Levels.Bass:0.00}");
    }

    [Fact]
    public void TapTempoSetsBpmAndPhase()
    {
        var c = new BeatClock();
        for (int i = 0; i < 4; i++) c.Tap(1000 + i * 500);
        Assert.Equal(120, c.Bpm, 3);
        Assert.Equal(0.0, c.Position(2500) % 1.0, 3);      // a beat exactly on the last tap
        Assert.Equal(0.5, c.Position(2750) % 1.0, 3);
    }

    [Fact]
    public void AudioBeatsPullTheClockIntoPhase()
    {
        var c = new BeatClock();
        c.SetBpm(120, 0);
        // the music's beats are 100 ms later than the clock thinks
        for (int i = 1; i <= 12; i++) c.AudioBeat(i * 500 + 100, 120);
        double frac = c.Position(12 * 500 + 100) % 1.0;
        Assert.True(frac < 0.05 || frac > 0.95, $"phase error {frac:0.000} beats");
    }

    [Fact]
    public void VirtualTimeStepsOncePerBeatAndNeverRunsBackwards()
    {
        var c = new BeatClock();
        c.SetBpm(100, 0);                                   // 600 ms per beat
        Assert.Equal(400, c.VirtualMs(600, 400, 1));        // one beat = one step of 400 ms
        Assert.Equal(1600, c.VirtualMs(1200, 400, 2));      // two steps per beat
        long before = c.VirtualMs(1500, 400, 2);
        c.AudioBeat(1500, 100);                             // phase correction backwards
        Assert.True(c.VirtualMs(1500, 400, 2) >= before);
    }
}
