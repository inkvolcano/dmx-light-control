using System.Drawing;
using static DmxLicht.Core.Effects.EffectParams;

namespace DmxLicht.Core.Effects;

/// <summary>A named effect: which settings it uses and how it lights each lamp.</summary>
public sealed class EffectDefinition
{
    /// <summary>Stable id, used for translations ("blink", "fire").</summary>
    public string Key { get; internal set; }
    /// <summary>Dutch display name (the UI translates by <see cref="Key"/>).</summary>
    public string Name { get; }
    public EffectParams Uses { get; }
    public EffectFunc Func { get; }

    public EffectDefinition(string name, EffectParams uses, EffectFunc func)
    {
        Name = name; Uses = uses; Func = func;
    }
}

/// <summary>
/// All chases and animations. To add an effect, append an entry to <see cref="All"/>:
/// a name, the settings it uses, and a function returning colour + level (0..1) for lamp i.
/// Keep existing entries in place: settings files store the effect by its position in this list.
/// </summary>
public static class EffectLibrary
{
    /// <summary>Most heads (lamps or lamp segments) an effect runs over.</summary>
    public const int MaxHeads = 512;

    static readonly Color Cyan = Color.FromArgb(0, 255, 255);
    static readonly Color DeepBlue = Color.FromArgb(0, 0, 255);

    public static readonly IReadOnlyList<EffectDefinition> All = new EffectDefinition[]
    {
        // 0-4: aan/uit-patronen (Aan + Uit tijden)
        new("Knipperen", ColorA | OnOff, (f, i) => Lit(f.A(i), (f.Ms % f.Period) < f.On)),
        new("Lopend licht (vertraging)", ColorA | OnOff | Delay, (f, i) =>
        {
            long t = ((f.Ms - (long)i * f.Delay) % f.Period + f.Period) % f.Period;
            return Lit(f.A(i), t < f.On);
        }),
        new("Om en om (aan/uit)", ColorA | OnOff, (f, i) =>
        {
            long sh = (i % 2) * (f.Period / 2);
            long t = ((f.Ms - sh) % f.Period + f.Period) % f.Period;
            return Lit(f.A(i), t < f.On);
        }),
        new("Willekeurig knipperen", ColorA | OnOff, (f, i) =>
        {
            long cyc = f.Ms / f.Period;
            var rnd = new Random((int)((cyc * 997 + i * 131) & 0x7FFFFFFF));
            return Lit(f.A(i), (f.Ms % f.Period) < f.On && rnd.Next(2) == 0);
        }),
        new("Strobo (wit)", OnOff, (f, i) => Lit(Light.White, (f.Ms % f.Period) < f.On)),

        // 5-7: vloeiende kleur en ademen
        new("Kleurwissel", OnOff, (f, i) =>
        {
            double T = f.Period * 6.0;
            double hue = (f.Ms % (long)T) / T * 360.0;
            return new(f.Hue(hue), 1.0);
        }),
        new("Regenboog vloeiend", OnOff, (f, i) =>
        {
            double T = f.Period * 6.0;
            double hue = (f.Ms % (long)T) / T * 360.0 + i * 360.0 / f.N;
            return new(f.Hue(hue), 1.0);
        }),
        new("Ademen (vertraging)", ColorA | OnOff | Delay, (f, i) =>
        {
            long T = (long)Math.Max(600, f.Period * 2);
            double ph = (double)((((f.Ms - (long)i * f.Delay) % T) + T) % T) / T;
            return new(f.A(i), 0.5 - 0.5 * Math.Cos(2 * Math.PI * ph));
        }),

        // 8-22: stap-patronen (Stap tijd)
        new("Lopend licht >", ColorA | Step, (f, i) => Lit(f.A(i), i == (int)(f.Step % f.N))),
        new("Lopend licht <", ColorA | Step, (f, i) => Lit(f.A(i), i == f.N - 1 - (int)(f.Step % f.N))),
        new("Heen en weer", ColorA | Step, (f, i) => Lit(f.A(i), i == PingPong(f.Step, f.N))),
        new("Om en om (A / B)", ColorA | ColorB | Step, (f, i) =>
        {
            bool even = f.Step % 2 == 0;
            return new(((i % 2 == 0) == even) ? f.A(i) : f.B, 1.0);
        }),
        new("Alle lampen A / B", ColorA | ColorB | Step, (f, i) => new(f.Step % 2 == 0 ? f.A(i) : f.B, 1.0)),
        new("Opbouwen", ColorA | Step, (f, i) => Lit(f.A(i), i < f.Step % (f.N + 1))),
        new("Afbouwen", ColorA | Step, (f, i) => Lit(f.A(i), i >= f.Step % (f.N + 1))),
        new("Vullen en legen", ColorA | Step, (f, i) =>
        {
            long per2 = 2 * f.N;
            long s2 = f.Step % per2;
            long cnt = (s2 <= f.N) ? s2 : per2 - s2;
            return Lit(f.A(i), i < cnt);
        }),
        new("Buiten naar binnen", ColorA | Step, (f, i) =>
        {
            int half = (f.N + 1) / 2;
            long cnt = f.Step % (half + 1);
            int d = Math.Min(i, f.N - 1 - i);
            return Lit(f.A(i), d < cnt);
        }),
        new("Binnen naar buiten", ColorA | Step, (f, i) =>
        {
            int half = (f.N + 1) / 2;
            long cnt = f.Step % (half + 1);
            double dc = Math.Abs(i - (f.N - 1) / 2.0);
            return Lit(f.A(i), dc <= cnt - 0.5);
        }),
        new("Twee lopende lichten", ColorA | Step, (f, i) =>
            Lit(f.A(i), i == (int)(f.Step % f.N) || i == (int)((f.Step + f.N / 2) % f.N))),
        new("Komeet met staart", ColorA | Step, (f, i) =>
        {
            int head = (int)(f.Step % f.N);
            int dist = ((head - i) % f.N + f.N) % f.N;
            return new(f.A(i), dist == 0 ? 1.0 : (dist == 1 ? 0.55 : (dist == 2 ? 0.25 : 0.0)));
        }),
        new("Regenboog (stappen)", Step, (f, i) =>
        {
            long idx = ((i - f.Step) % f.N + f.N) % f.N;
            return new(f.Hue(idx * 360.0 / f.N), 1.0);
        }),
        new("Willekeurig flitsen", Step, (f, i) =>
        {
            var r = new Random((int)((f.Step * 7919 + i * 104729) & 0x7FFFFFFF));
            double lvl = (r.Next(3) == 0) ? 1.0 : 0.0; // volgorde van r.Next() aanroepen niet wijzigen
            return new(f.Hue(r.Next(360)), lvl);
        }),
        new("Willekeurige kleuren", Step, (f, i) =>
        {
            var r = new Random((int)((f.Step * 7919 + i * 104729) & 0x7FFFFFFF));
            return new(f.Hue(r.Next(360)), 1.0);
        }),

        // 23-30: golven en mengen
        new("Golf (kleur A)", ColorA | Step, (f, i) => new(f.A(i), Wave(f, i))),
        new("Kleurgolf (A naar B)", ColorA | ColorB | Step, (f, i) =>
            new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, Wave(f, i))), 1.0)),
        new("Politie (rood/blauw)", Step, (f, i) =>
        {
            int half = f.N / 2;
            bool red = (i < half) == (f.Step % 2 == 0);
            return Lit(red ? Light.Red : Light.Blue, f.Frac < 0.6);
        }),
        new("Vuur", Step, (f, i) =>
        {
            var r = new Random((int)(((f.Ms / 70) * 131 + i * 7919) & 0x7FFFFFFF));
            double hue = r.Next(0, 45);
            Light c = f.Hue(hue);
            return new(c, 0.45 + 0.55 * r.NextDouble());
        }),
        new("Ademen (golf)", ColorA | Step, (f, i) =>
            new(f.A(i), 0.5 - 0.5 * Math.Cos(2 * Math.PI * (f.Ms / (f.StepMs * 6.0) - i / (double)f.N)))),
        new("Dubbel lopend (A en B)", ColorA | ColorB | Step, (f, i) =>
        {
            if (i == (int)(f.Step % f.N)) return new(f.A(i), 1.0);
            if (i == f.N - 1 - (int)(f.Step % f.N)) return new(f.B, 1.0);
            return new(f.A(i), 0.0);
        }),
        new("Stuiterende bal", ColorA | Step, (f, i) =>
        {
            int dist = Math.Abs(i - PingPong(f.Step, f.N));
            return new(f.A(i), dist == 0 ? 1.0 : (dist == 1 ? 0.4 : (dist == 2 ? 0.15 : 0.0)));
        }),
        new("Pulse (A naar B)", ColorA | ColorB | Step, (f, i) =>
        {
            double t = 0.5 - 0.5 * Math.Cos(2 * Math.PI * f.Ms / (f.StepMs * 8.0));
            return new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, t)), 1.0);
        }),

        // 31-39: twinkels, sferen en thema's
        new("Sneeuw (witte twinkel)", Step, (f, i) =>
        {
            long cyc = f.Ms / (f.StepMs * 2L);
            double fr = (f.Ms % (f.StepMs * 2L)) / (f.StepMs * 2.0);
            var r = new Random((int)((cyc * 7919 + i * 104729) & 0x7FFFFFFF));
            return new(Light.White, (r.Next(3) == 0) ? Math.Sin(Math.PI * fr) : 0.0);
        }),
        new("Sparkle (kleur A)", ColorA | Step, (f, i) =>
        {
            long cyc = f.Ms / f.StepMs;
            var r = new Random((int)((cyc * 7919 + i * 104729) & 0x7FFFFFFF));
            return new(f.A(i), (r.Next(4) == 0) ? (1.0 - f.Frac) : 0.0);
        }),
        new("Zonsondergang", Step, (f, i) =>
        {
            double T = f.StepMs * 20.0;
            double ph = (f.Ms % (long)T) / T;
            return new(f.Hue(50.0 * (1.0 - ph)), Math.Max(0.05, 1.0 - ph * 0.95));
        }),
        new("Ambulance (blauw/wit)", Step, (f, i) =>
        {
            bool left = i < f.N / 2;
            bool ph = f.Step % 2 == 0;
            return Lit(left == ph ? Light.Blue : Light.White, f.Frac < 0.6);
        }),
        new("Kerst (rood/groen/wit)", Step, (f, i) =>
        {
            int k = (int)((i + f.Step) % 3);
            return new(k == 0 ? Light.Red : (k == 1 ? Light.Green : Light.White), 1.0);
        }),
        new("Brandweer (rood/wit)", Step, (f, i) =>
        {
            bool left = i < f.N / 2;
            bool ph = f.Step % 2 == 0;
            return Lit(left == ph ? Light.Red : Light.White, f.Frac < 0.6);
        }),
        new("Waterval (cyaan/blauw)", Step, (f, i) =>
        {
            double t = Wave(f, i);
            return new(f.Rgb(ColorUtil.Lerp(Cyan, DeepBlue, t)), 0.35 + 0.65 * t);
        }),
        new("Willekeurig A / B", ColorA | ColorB | Step, (f, i) =>
        {
            var r = new Random((int)((f.Step * 7919 + i * 104729) & 0x7FFFFFFF));
            return new(r.Next(2) == 0 ? f.A(i) : f.B, 1.0);
        }),
        new("Lopend licht regenboog", Step, (f, i) =>
        {
            if (i != (int)(f.Step % f.N)) return new(f.A(i), 0.0);
            double hue = ((f.Step / f.N) * 45L) % 360L;
            return new(f.Hue(hue), 1.0);
        }),

        // 40+: nieuw in 1.5 (niet in v1.4)
        new("Scanner", ColorA | Step, (f, i) =>
        {
            double pos = SmoothPingPong(f.Ms / (double)f.StepMs, f.N);
            return new(f.A(i), Math.Max(0.0, 1.0 - Math.Abs(i - pos) * 0.6));
        }),
        new("Theater chase", ColorA | ColorB | Step, (f, i) => new((i + f.Step) % 3 == 0 ? f.A(i) : f.B, (i + f.Step) % 3 == 0 ? 1.0 : 0.25)),
        new("Kaarslicht", Step, (f, i) =>
        {
            long slot = f.Ms / Math.Max(40, f.StepMs / 4);
            double now = Noise(slot, i), next = Noise(slot + 1, i);
            double frac = (f.Ms % Math.Max(40, f.StepMs / 4)) / (double)Math.Max(40, f.StepMs / 4);
            double n = now + (next - now) * frac;
            return new(f.Hue(22 + 14 * n), 0.55 + 0.45 * n);
        }),
        new("Bliksem", Step, (f, i) =>
        {
            long slot = f.Ms / (f.StepMs * 3L);
            double fr = (f.Ms % (f.StepMs * 3L)) / (f.StepMs * 3.0);
            var r = new Random((int)((slot * 7919) & 0x7FFFFFFF));
            bool strike = r.Next(3) == 0 && (fr < 0.06 || (fr > 0.12 && fr < 0.17));
            bool here = Noise(slot, i) > 0.3;
            return new(Light.White, strike && here ? 1.0 : 0.0);
        }),
        new("Hartslag", ColorA | Step, (f, i) =>
        {
            double ph = (f.Ms % (f.StepMs * 4L)) / (f.StepMs * 4.0);
            double beat = Math.Exp(-Math.Pow(ph / 0.04, 2)) + 0.7 * Math.Exp(-Math.Pow((ph - 0.2) / 0.04, 2));
            return new(f.A(i), Math.Min(1.0, beat));
        }),
        new("Noorderlicht", Step, (f, i) =>
        {
            double hue = 130 + 70 * Math.Sin(2 * Math.PI * (f.Ms / (f.StepMs * 20.0) + i * 0.13));
            return new(f.Hue(hue), 0.6 + 0.4 * Math.Sin(2 * Math.PI * (f.Ms / (f.StepMs * 13.0) + i * 0.21)));
        }),
        new("Oceaan", Step, (f, i) =>
        {
            double t = Wave(f, i);
            return new(f.Rgb(ColorUtil.Lerp(Color.FromArgb(0, 40, 255), Color.FromArgb(0, 220, 255), t)), 0.45 + 0.55 * t);
        }),
        new("Verloop A naar B", ColorA | ColorB, (f, i) =>
            new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, f.N <= 1 ? 0.0 : i / (double)(f.N - 1))), 1.0)),
        new("Draaiend verloop", ColorA | ColorB | Step, (f, i) =>
        {
            double t = (i / (double)f.N + f.Ms / ((double)f.StepMs * f.N)) % 1.0;
            return new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, 1.0 - Math.Abs(2 * t - 1))), 1.0);
        }),
        new("Disco", Step, (f, i) =>
        {
            var r = new Random((int)((f.Step * 104729 + i * 7919 + 17) & 0x7FFFFFFF));
            bool on = r.Next(2) == 0;
            return new(f.Hue(r.Next(12) * 30), on ? 1.0 : 0.0);
        }),
        new("Meteoor", ColorA | Step, (f, i) =>
        {
            const int tail = 5;
            double pos = (f.Ms / (double)f.StepMs) % (f.N + tail);
            double d = pos - i;
            return new(f.A(i), d >= 0 && d < tail ? Math.Pow(1 - d / tail, 2) : 0.0);
        }),
        new("Willekeurig ademen", ColorA | Step, (f, i) =>
            new(f.A(i), 0.5 - 0.5 * Math.Cos(2 * Math.PI * (f.Ms / (f.StepMs * 8.0) + Noise(0, i))))),

        // 52+: op het geluid (Beat = werkt ook met tikken, Audio = luisteren nodig)
        new("Kleurwissel op de maat", Beat, (f, i) =>
            new(f.Hue(f.Audio.BeatIndex * 137.5 % 360), 0.75 + 0.25 * Math.Exp(-f.Audio.BeatPhase * 4))),
        new("Kleur A/B op de maat", ColorA | ColorB | Beat, (f, i) =>
            new(f.Audio.BeatIndex % 2 == 0 ? f.A(i) : f.B, 0.35 + 0.65 * Math.Exp(-f.Audio.BeatPhase * 3))),
        new("Willekeurige kleur op de maat", Beat, (f, i) =>
        {
            var r = new Random((int)((f.Audio.BeatIndex * 7919 + i * 104729) & 0x7FFFFFFF));
            return new(f.Hue(r.Next(12) * 30), 0.6 + 0.4 * Math.Exp(-f.Audio.BeatPhase * 3));
        }),
        new("Lopend op de maat", Beat, (f, i) =>
        {
            long beat = f.Audio.BeatIndex;
            int head = (int)(((beat % f.N) + f.N) % f.N);
            Light c = f.Hue((beat / 4) * 90 % 360);   // new colour every bar of 4
            int behind = ((head - i) % f.N + f.N) % f.N;
            return new(c, behind == 0 ? 1.0 : behind == 1 ? 0.3 * (1 - f.Audio.BeatPhase) : 0.0);
        }),
        new("Spectrum (bas, midden, hoog)", EffectParams.Audio, (f, i) =>
        {
            AudioFrame a = f.Audio;
            double max = Math.Max(a.Bass, Math.Max(a.Mid, a.High));
            if (max < 0.01) return new(Light.Red, 0.0);
            var rgb = System.Drawing.Color.FromArgb((int)(255 * a.Bass / max), (int)(255 * a.Mid / max), (int)(255 * a.High / max));
            return new(f.Rgb(rgb), max);
        }),
        new("Banden over de lampen", EffectParams.Audio, (f, i) =>
        {
            AudioFrame a = f.Audio;
            int band = f.N < 3 ? i % 3 : i * 3 / f.N;     // links bas, midden, rechts hoog
            return band switch
            {
                0 => new(Light.Red, a.Bass),
                1 => new(Light.Green, a.Mid),
                _ => new(Light.Blue, a.High),
            };
        }),
        new("VU-meter", ColorA | ColorB | EffectParams.Audio, (f, i) =>
        {
            double lit = f.Audio.Level * f.N;               // how many lamps are on
            double t = f.N <= 1 ? 0 : i / (double)(f.N - 1);
            return new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, t)), Math.Clamp(lit - i, 0.0, 1.0));
        }),
        new("VU-meter vanuit het midden", ColorA | ColorB | EffectParams.Audio, (f, i) =>
        {
            double half = f.N / 2.0;
            double d = Math.Abs(i + 0.5 - half) / Math.Max(half, 0.5);   // 0 = midden, 1 = rand
            double lit = f.Audio.Level - d + 1.0 / Math.Max(f.N, 1);
            return new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, d)), Math.Clamp(lit * f.N / 2.0, 0.0, 1.0));
        }),
        new("Kleur op volume", ColorA | ColorB | EffectParams.Audio, (f, i) =>
            new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, f.Audio.Level)), 0.25 + 0.75 * f.Audio.Level)),
        new("Bas-flits", ColorA | ColorB | EffectParams.Audio, (f, i) =>
        {
            double b = f.Audio.Bass * f.Audio.Bass;           // only the real kicks
            return new(f.Rgb(ColorUtil.Lerp(f.ColorA, f.ColorB, b)), 0.3 + 0.7 * b);
        }),
        new("Toonhoogte naar kleur", EffectParams.Audio, (f, i) =>
        {
            AudioFrame a = f.Audio;
            double sum = a.Bass + a.Mid + a.High;
            double bright = sum < 0.01 ? 0 : Math.Clamp((0.5 * a.Mid + a.High) / sum, 0, 1);
            return new(f.Hue(240 * bright), a.Level);        // bassig = rood, helder = blauw
        }),
        new("Fonkelen op hoog", ColorA | EffectParams.Audio, (f, i) =>
        {
            var r = new Random((int)(((f.Ms / 60) * 7919 + i * 104729) & 0x7FFFFFFF));
            bool sparkle = r.NextDouble() < f.Audio.High * 0.6;
            return sparkle ? new(Light.White, 1.0) : new(f.A(i), 0.15 * f.Audio.Level);
        }),
        new("Vuur op geluid", EffectParams.Audio, (f, i) =>
        {
            var r = new Random((int)(((f.Ms / 70) * 131 + i * 7919) & 0x7FFFFFFF));
            Light c = f.Hue(r.Next(0, 45));
            return new(c, (0.15 + 0.85 * f.Audio.Level) * (0.55 + 0.45 * r.NextDouble()));
        }),
        new("Strobo bij piek", ColorA | EffectParams.Audio, (f, i) =>
            f.Audio.Surge
                ? new(Light.White, (f.Ms / 40) % 2 == 0 ? 1.0 : 0.0)
                : new(f.A(i), 0.2 + 0.8 * f.Audio.Level)),
    };

    static readonly string[] Keys =
    {
        "blink", "chase-delay", "alternate-onoff", "random-blink", "strobe-white", "color-cycle", "rainbow-smooth", "breathe-delay", "chase-right", "chase-left", "bounce", "alternate-ab", "all-ab", "build-up", "build-down", "fill-empty", "outside-in", "inside-out", "two-chasers", "comet", "rainbow-steps", "random-flash", "random-colors", "wave-a", "color-wave-ab", "police", "fire", "breathe-wave", "double-chase-ab", "bouncing-ball", "pulse-ab", "snow", "sparkle", "sunset", "ambulance", "christmas", "fire-brigade", "waterfall", "random-ab", "rainbow-chase",
        "scanner", "theater-chase", "candle", "lightning", "heartbeat", "aurora", "ocean", "gradient-ab", "gradient-rotate", "disco", "meteor", "random-breathe",
        "audio-beat-colors", "audio-beat-ab", "audio-beat-random", "audio-beat-chase", "audio-spectrum", "audio-bands", "audio-vu", "audio-vu-center",
        "audio-volume-color", "audio-bass-flash", "audio-pitch-color", "audio-sparkle", "audio-fire", "audio-surge-strobe"
    };

    static EffectLibrary()
    {
        for (int i = 0; i < All.Count; i++) All[i].Key = i < Keys.Length ? Keys[i] : "fx" + i;
    }

    public static int IndexOf(string key)
    {
        for (int i = 0; i < All.Count; i++) if (All[i].Key == key) return i;
        return -1;
    }

    /// <summary>Computes every lamp's colour and level for effect <paramref name="s"/> at time <paramref name="ms"/>.</summary>
    public static LampOutput[] Render(EffectSettings s, bool useWhite, long ms, int lamps, AudioFrame audio = default)
    {
        EffectDefinition fx = All[Math.Clamp(s.EffectIndex, 0, All.Count - 1)];
        int n = Math.Max(1, Math.Min(lamps, MaxHeads));
        bool perLamp = s.ColorPerLamp && (fx.Uses & ColorA) != 0;
        int stepMs = Math.Max(40, s.StepMs);

        var lampA = new Light[n];
        for (int i = 0; i < n; i++) lampA[i] = Light.FromColor(perLamp ? s.LampColor(i) : s.ColorA, useWhite);

        var f = new EffectFrame
        {
            Ms = ms,
            N = n,
            UseWhite = useWhite,
            On = s.OnMs,
            Off = s.OffMs,
            Delay = s.DelayMs,
            Period = Math.Max(20, s.OnMs + s.OffMs),
            StepMs = stepMs,
            Step = ms / stepMs,
            Frac = (ms % stepMs) / (double)stepMs,
            Cycle = (double)stepMs * n,
            ColorA = s.ColorA,
            ColorB = s.ColorB,
            B = Light.FromColor(s.ColorB, useWhite),
            LampA = lampA,
            Audio = audio,
        };

        var result = new LampOutput[n];
        for (int i = 0; i < n; i++) result[i] = fx.Func(f, i);
        return result;
    }

    // ---- helpers for effect functions ----

    static LampOutput Lit(Light c, bool on) => new(c, on ? 1.0 : 0.0);

    /// <summary>Sine wave 0..1 travelling across the lamps, one lamp per step.</summary>
    static double Wave(EffectFrame f, int i) => 0.5 + 0.5 * Math.Sin(2 * Math.PI * (f.Ms / f.Cycle - i / (double)f.N));

    /// <summary>Like <see cref="PingPong"/> but continuous: position 0..n-1 for a fractional step.</summary>
    static double SmoothPingPong(double step, int n)
    {
        if (n <= 1) return 0;
        double period = 2.0 * (n - 1);
        double s = step % period;
        return s < n - 1 ? s : period - s;
    }

    /// <summary>Repeatable pseudo-random 0..1 for (slot, lamp).</summary>
    static double Noise(long slot, int i)
    {
        unchecked
        {
            ulong x = (ulong)slot * 0x9E3779B97F4A7C15UL ^ (ulong)(i + 1) * 0xC2B2AE3D27D4EB4FUL;
            x ^= x >> 31; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 29;
            return (x >> 11) / (double)(1UL << 53);
        }
    }

    /// <summary>0,1,..,n-1,n-2,..,1,0,1,.. for step 0,1,2,...</summary>
    public static int PingPong(long step, int n)
    {
        if (n <= 1) return 0;
        int period = 2 * (n - 1);
        long s = step % period;
        return (int)(s < n ? s : period - s);
    }
}
