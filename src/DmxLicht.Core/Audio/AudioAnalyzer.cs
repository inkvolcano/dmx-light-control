namespace DmxLicht.Core.Audio;

/// <summary>Sound levels 0..1 (auto-gained) plus tempo, as shown in the UI and used by the engine.</summary>
public readonly record struct AudioLevels(double Level, double Bass, double Mid, double High, double Bpm, long Beats);

/// <summary>
/// Turns raw mono samples into levels and beats. No audio API here: the app feeds it samples
/// (microphone or the PC's own output), the tests feed it synthetic music.
/// <list type="bullet">
/// <item>Bands with one-pole filters: bass &lt; 150 Hz, high &gt; 2 kHz, mid in between.</item>
/// <item>Automatic gain: each band is divided by its own slowly decaying peak, so quiet and loud rooms both work.</item>
/// <item>Beats: a bass-energy onset clearly above the recent average, at most ~4 per second.</item>
/// <item>Tempo: the most common spacing between recent beats (and multiples), folded into 70..180 BPM.</item>
/// </list>
/// </summary>
public sealed class AudioAnalyzer
{
    const int Hop = 512;
    readonly double dtMs;
    readonly double aLow, aHigh, aMidLow;
    double lpBass, lpMid, hpPrevOut;

    readonly float[] hopBuf = new float[Hop];
    int hopFill;
    long hopIndex;

    // auto gain
    double peakLevel = 1e-3, peakBass = 1e-3, peakMid = 1e-3, peakHigh = 1e-3;
    double level, bass, mid, high;

    // onsets
    readonly double[] bassHistory = new double[96]; // ~1 s of hops at 48 kHz
    int histPos, histCount;
    double prevBass;
    double lastOnsetMs = double.NegativeInfinity;
    readonly List<double> onsets = new();
    double bpm;
    long beats;

    /// <summary>Extra gain on top of auto-gain (1 = normal). Higher = reacts to quieter sound.</summary>
    public double Sensitivity { get; set; } = 1.0;

    /// <summary>Raised for every detected beat with its time in milliseconds (the stream's own clock).</summary>
    public event Action<double> Beat;

    public AudioAnalyzer(int sampleRate)
    {
        SampleRate = sampleRate;
        dtMs = 1000.0 * Hop / sampleRate;
        aLow = OnePole(150, sampleRate);
        aMidLow = OnePole(2000, sampleRate);
        aHigh = OnePole(2000, sampleRate);
    }

    public int SampleRate { get; }

    public AudioLevels Levels => new(level, bass, mid, high, bpm, beats);

    static double OnePole(double fc, int sr) => 1 - Math.Exp(-2 * Math.PI * fc / sr);

    /// <summary>Feed mono samples (-1..1). Can be called with any block size.</summary>
    public void Process(ReadOnlySpan<float> samples)
    {
        foreach (float s in samples)
        {
            hopBuf[hopFill++] = s;
            if (hopFill == Hop) { AnalyzeHop(); hopFill = 0; }
        }
    }

    void AnalyzeHop()
    {
        double sumAll = 0, sumBass = 0, sumMid = 0, sumHigh = 0;
        for (int i = 0; i < Hop; i++)
        {
            double x = hopBuf[i];
            lpBass += aLow * (x - lpBass);
            lpMid += aMidLow * (x - lpMid);
            double midBand = lpMid - lpBass;
            // high-pass: x minus a low-pass at 2 kHz
            double hp = x - hpPrevOut;
            hpPrevOut += aHigh * (x - hpPrevOut);
            sumAll += x * x; sumBass += lpBass * lpBass; sumMid += midBand * midBand; sumHigh += hp * hp;
        }
        double rAll = Math.Sqrt(sumAll / Hop), rBass = Math.Sqrt(sumBass / Hop), rMid = Math.Sqrt(sumMid / Hop), rHigh = Math.Sqrt(sumHigh / Hop);

        level = Gain(rAll, ref peakLevel);
        bass = Gain(rBass, ref peakBass);
        mid = Gain(rMid, ref peakMid);
        high = Gain(rHigh, ref peakHigh);

        double now = hopIndex * dtMs;
        hopIndex++;
        DetectOnset(rBass, now);
    }

    /// <summary>Level relative to a peak that decays ~2 % per hop (back down in a few seconds of quiet).</summary>
    double Gain(double v, ref double peak)
    {
        peak = Math.Max(Math.Max(v, peak * 0.995), 2e-4);
        double g = v / peak * Sensitivity;
        return g > 1 ? 1 : g;
    }

    void DetectOnset(double e, double now)
    {
        double mean = 0, var = 0;
        if (histCount > 0)
        {
            for (int i = 0; i < histCount; i++) mean += bassHistory[i];
            mean /= histCount;
            for (int i = 0; i < histCount; i++) var += (bassHistory[i] - mean) * (bassHistory[i] - mean);
            var /= histCount;
        }
        double threshold = mean + 1.5 * Math.Sqrt(var) + 0.002 / Math.Max(Sensitivity, 0.1);
        bool rising = e > prevBass * 1.15;
        if (histCount >= 16 && e > threshold && rising && now - lastOnsetMs > 280)
        {
            lastOnsetMs = now;
            beats++;
            onsets.Add(now);
            while (onsets.Count > 0 && now - onsets[0] > 10000) onsets.RemoveAt(0);
            EstimateTempo();
            Beat?.Invoke(now);
        }
        prevBass = e;
        bassHistory[histPos] = e;
        histPos = (histPos + 1) % bassHistory.Length;
        histCount = Math.Min(histCount + 1, bassHistory.Length);
    }

    void EstimateTempo()
    {
        if (onsets.Count < 4) return;
        var hist = new double[181];
        for (int i = 0; i < onsets.Count; i++)
            for (int j = i + 1; j < onsets.Count && j <= i + 4; j++)
            {
                double ioi = onsets[j] - onsets[i];
                if (ioi < 250 || ioi > 2000) continue;
                double b = 60000.0 / ioi;
                while (b < 70) b *= 2;
                while (b > 180) b /= 2;
                int k = (int)Math.Round(b);
                double w = 1.0 / (j - i); // direct neighbours count most
                for (int d = -2; d <= 2; d++)
                    if (k + d >= 70 && k + d <= 180) hist[k + d] += w * (d == 0 ? 1 : d is 1 or -1 ? 0.5 : 0.2);
            }
        int best = 0;
        for (int k = 70; k <= 180; k++) if (hist[k] > hist[best]) best = k;
        if (best == 0) return;
        // refine with the weighted average around the peak
        double sw = 0, sb = 0;
        for (int k = Math.Max(70, best - 2); k <= Math.Min(180, best + 2); k++) { sw += hist[k]; sb += hist[k] * k; }
        double est = sb / sw;
        bpm = bpm <= 0 || Math.Abs(est - bpm) > 8 ? est : bpm * 0.8 + est * 0.2;
    }
}
