using System.Drawing;

namespace DmxLicht.Core.Effects;

/// <summary>One row of a chase: a colour per column ("#RRGGBB", or null/empty = off).</summary>
public sealed class ChaseStep
{
    public List<string> Colors { get; set; } = new();
    /// <summary>Own duration in ms; null = the chase's step time.</summary>
    public int? DurationMs { get; set; }
    /// <summary>Brightness of this step, 0..100 %.</summary>
    public int Level { get; set; } = 100;
    /// <summary>Own crossfade into the next step (0..100 %); null = the chase's fade.</summary>
    public int? FadePercent { get; set; }
}

/// <summary>In which order the steps play.</summary>
public enum ChaseDirection { Forward, Reverse, Bounce, Random }

/// <summary>How the chase's columns are laid over the lamps.</summary>
public enum ChaseMapping
{
    /// <summary>Pattern repeats: 4 columns on 10 lamps = 1,2,3,4,1,2,3,4,1,2.</summary>
    Repeat,
    /// <summary>Columns stretched over all lamps: 4 columns on 8 lamps = 1,1,2,2,3,3,4,4.</summary>
    Stretch,
    /// <summary>Pattern runs from both ends towards the middle (mirrored).</summary>
    Mirror,
    /// <summary>Only the first lamps get the pattern, the rest stays off.</summary>
    Once,
}

/// <summary>
/// A chase made in the editor: steps × columns of colours, laid over the lamps by <see cref="Mapping"/>.
/// </summary>
public sealed class CustomChase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Chase";
    public int Columns { get; set; } = 4;
    public int StepMs { get; set; } = 500;
    /// <summary>Last part of each step that fades into the next one, 0..100 %.</summary>
    public int FadePercent { get; set; }
    public ChaseDirection Direction { get; set; } = ChaseDirection.Forward;
    public ChaseMapping Mapping { get; set; } = ChaseMapping.Repeat;
    /// <summary>Each repeat of the pattern (or each lamp further from the edge when mirrored) runs this many steps later: makes waves.</summary>
    public int RepeatOffset { get; set; }
    /// <summary>Advance on the beat (tap tempo or listening) instead of by time.</summary>
    public bool SyncToBeat { get; set; }
    public double BeatsPerStep { get; set; } = 1;
    /// <summary>Speed factor in %, 100 = as designed (also for beat sync: 200 = twice as many steps per beat).</summary>
    public int SpeedPercent { get; set; } = 100;
    /// <summary>Colour for empty cells instead of off ("" / null = off).</summary>
    public string Background { get; set; }
    /// <summary>Brightness follows the sound (needs listening on the music page).</summary>
    public bool AudioLevel { get; set; }
    public List<ChaseStep> Steps { get; set; } = new();
}

public static class ChaseRenderer
{
    /// <summary>Order of the steps within one pass (cycle number matters for random).</summary>
    public static int[] Order(CustomChase c, long cycle)
    {
        int k = c.Steps.Count;
        switch (c.Direction)
        {
            case ChaseDirection.Reverse:
                return Enumerable.Range(0, k).Reverse().ToArray();
            case ChaseDirection.Bounce:
                return k > 2 ? Enumerable.Range(0, k).Concat(Enumerable.Range(1, k - 2).Reverse()).ToArray()
                             : Enumerable.Range(0, k).ToArray();
            case ChaseDirection.Random:
                int[] o = Shuffle(k, cycle);
                if (k > 2 && o[0] == Shuffle(k, cycle - 1)[k - 1]) (o[0], o[1]) = (o[1], o[0]);
                return o;
            default:
                return Enumerable.Range(0, k).ToArray();
        }
    }

    static int[] Shuffle(int k, long seed)
    {
        int[] o = Enumerable.Range(0, k).ToArray();
        var r = new Random(unchecked((int)(seed * 2654435761L ^ 0x5bd1e995)));
        for (int i = k - 1; i > 0; i--) { int j = r.Next(i + 1); (o[i], o[j]) = (o[j], o[i]); }
        return o;
    }

    /// <summary>The step at an absolute step counter (counter 0 = first step of cycle 0; may be negative).</summary>
    static int StepAt(CustomChase c, long counter, int len)
    {
        long cycle = (long)Math.Floor(counter / (double)len);
        int within = (int)(counter - cycle * len);
        return Order(c, cycle)[within];
    }

    /// <summary>Absolute step counter and progress within that step at this moment.</summary>
    public static (long Counter, double Frac) Position(CustomChase c, long ms, AudioFrame audio)
    {
        int len = Order(c, 0).Length;
        double speed = Math.Clamp(c.SpeedPercent, 5, 1000) / 100.0;
        if (c.SyncToBeat)
        {
            double pos = Math.Max(0, audio.Beat) * speed / Math.Max(0.125, c.BeatsPerStep);
            double fl = Math.Floor(pos);
            return ((long)fl, pos - fl);
        }
        // durations are per step, so a pass takes the same time in every order
        long scaled = (long)(ms * speed);
        long total = 0;
        foreach (int i in Order(c, 0)) total += Duration(c, i);
        long cycle = scaled / total, t = scaled % total;
        int[] order = Order(c, cycle);
        int idx = 0;
        while (idx < len - 1 && t >= Duration(c, order[idx])) { t -= Duration(c, order[idx]); idx++; }
        return (cycle * len + idx, Math.Min(1, t / (double)Duration(c, order[idx])));
    }

    static int Duration(CustomChase c, int step) => Math.Max(20, c.Steps[step].DurationMs ?? c.StepMs);

    /// <summary>Which column and which repeat a lamp gets (column -1 = no part of the pattern).</summary>
    public static (int Column, int Repeat) Map(CustomChase c, int lamp, int lamps)
    {
        int cols = Math.Max(1, c.Columns);
        switch (c.Mapping)
        {
            case ChaseMapping.Stretch:
                return ((int)((long)lamp * cols / Math.Max(1, lamps)), 0);
            case ChaseMapping.Mirror:
                int j = lamp < (lamps + 1) / 2 ? lamp : lamps - 1 - lamp;
                return (j % cols, j / cols);
            case ChaseMapping.Once:
                return (lamp < cols ? lamp : -1, 0);
            default:
                return (lamp % cols, lamp / cols);
        }
    }

    public static LampOutput[] Render(CustomChase c, bool useWhite, long ms, int lamps, AudioFrame audio)
    {
        int n = Math.Max(1, Math.Min(lamps, EffectLibrary.MaxHeads));
        var result = new LampOutput[n];
        if (c.Steps.Count == 0) { Array.Fill(result, new LampOutput(Light.White, 0)); return result; }

        int len = Order(c, 0).Length;
        (long counter, double frac) = Position(c, ms, audio);
        Color? bg = ParseCell(c.Background);
        double audioGain = c.AudioLevel ? Math.Clamp(0.1 + 0.9 * audio.Level, 0, 1) : 1;

        for (int i = 0; i < n; i++)
        {
            (int col, int rep) = Map(c, i, n);
            if (col < 0) { result[i] = new LampOutput(Light.White, 0); continue; }
            long at = counter - (long)rep * c.RepeatOffset;
            ChaseStep cur = c.Steps[StepAt(c, at, len)], next = c.Steps[StepAt(c, at + 1, len)];

            // crossfade during the last part of the step
            double f = Math.Clamp(cur.FadePercent ?? c.FadePercent, 0, 100) / 100.0;
            double mix = f <= 0 || frac <= 1 - f ? 0 : (frac - (1 - f)) / f;

            Color? a = Cell(cur, col) ?? bg, b = Cell(next, col) ?? bg;
            double la = Math.Clamp(cur.Level, 0, 100) / 100.0, lb = Math.Clamp(next.Level, 0, 100) / 100.0;
            if (mix <= 0)
            {
                result[i] = a is Color ca ? new LampOutput(Light.FromColor(ca, useWhite), la * audioGain) : new LampOutput(Light.White, 0.0);
                continue;
            }
            Color from = a ?? b ?? Color.Black, to = b ?? a ?? Color.Black;
            double level = (a != null ? (1 - mix) * la : 0) + (b != null ? mix * lb : 0);
            result[i] = new LampOutput(Light.FromColor(ColorUtil.Lerp(from, to, mix), useWhite), level * audioGain);
        }
        return result;
    }

    static Color? Cell(ChaseStep s, int col) => col < s.Colors.Count ? ParseCell(s.Colors[col]) : null;

    static Color? ParseCell(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        Color c = ColorUtil.ParseHex(hex, Color.Empty);
        return c == Color.Empty ? null : c;
    }
}
