using System.Drawing;
using static DmxLicht.Core.Effects.EffectParams;

namespace DmxLicht.Core.Effects;

/// <summary>
/// The second effect collection (100 effects, added in 2.2). Appended after the classic list in
/// <see cref="EffectLibrary.All"/>, so effect numbers in existing shows stay the same. Each entry has a stable key
/// (translations, commands), a Dutch name, a category for the filter chips and, for fixed colour schemes, a swatch
/// (hex colours) for the little bar on the effect card.
/// Speed: "Step" effects scale with the step time (f.StepMs), on/off effects with the on and off times.
/// </summary>
static class MoreEffects
{
    static EffectDefinition E(string key, string cat, string name, EffectParams uses, EffectFunc fn, string swatch = null) =>
        new(name, uses, fn) { Key = key, Category = cat, Swatch = swatch };

    // a few fixed colours
    static readonly Color WarmWhite = C(255, 170, 90), ColdWhite = C(200, 225, 255), Amber = C(255, 140, 0),
        Orange = C(255, 100, 0), Purple = C(140, 0, 255), Gold = C(255, 180, 30), Pink = C(255, 60, 150);

    public static readonly EffectDefinition[] List =
    {
        // ---------------------------------------------------------------- aan/uit (Aan + Uit tijden)
        E("double-blink", "onoff", "Dubbel knipperen", ColorA | OnOff, (f, i) =>
        {
            double u = (f.Ms % f.Period) / (double)Math.Max(1, f.On);
            return Lit(f.A(i), u < 1 && (u < 0.35 || (u >= 0.6 && u < 0.95)));
        }),
        E("triple-flash", "onoff", "Drie keer flitsen", ColorA | OnOff, (f, i) =>
        {
            double u = (f.Ms % f.Period) / (double)Math.Max(1, f.On);
            return Lit(f.A(i), u < 1 && Fr(u * 3) < 0.55);
        }),
        E("blink-thirds", "onoff", "Knipperen in drieën", ColorA | OnOff, (f, i) =>
            Lit(f.A(i), Mod(f.Ms - (i % 3) * (f.Period / 3), f.Period) < f.On)),
        E("random-strobe", "onoff", "Willekeurige strobo", OnOff, (f, i) =>
        {
            long cyc = f.Ms / f.Period;
            long t = f.Ms % f.Period - (long)(Rnd(cyc, i) * f.Period * 0.8);
            return Lit(Light.White, t >= 0 && t < Math.Clamp(f.On, 20, 80));
        }, "#fff,#222,#fff,#222"),
        E("strobe-ab", "onoff", "Strobo A / B", ColorA | ColorB | OnOff, (f, i) =>
        {
            bool even = (f.Ms / f.Period) % 2 == 0;
            return Lit(even ? f.A(i) : f.B, f.Ms % f.Period < f.On);
        }),
        E("strobe-rainbow", "onoff", "Regenboog-strobo", OnOff, (f, i) =>
            Lit(f.Hue((f.Ms / f.Period) * 47 % 360), f.Ms % f.Period < f.On)),
        E("morse-sos", "onoff", "Morse SOS", ColorA | OnOff, (f, i) =>
        {
            const string sos = "10101" + "000" + "11101110111" + "000" + "10101" + "0000000";
            long unit = Math.Max(60, f.On);
            return Lit(f.A(i), sos[(int)Mod(f.Ms / unit, sos.Length)] == '1');
        }),
        E("blink-pairs", "onoff", "Paren knipperen", ColorA | OnOff, (f, i) =>
            Lit(f.A(i), Mod(f.Ms - ((i / 2) % 2) * (f.Period / 2), f.Period) < f.On)),
        E("blink-sweep-ab", "onoff", "Knippergolf A / B", ColorA | ColorB | OnOff | Delay, (f, i) =>
        {
            long t = f.Ms - (long)i * f.Delay;
            long cyc = FloorDiv(t, f.Period);
            return Lit(cyc % 2 == 0 ? f.A(i) : f.B, Mod(t, f.Period) < f.On);
        }),
        E("countdown", "onoff", "Aftellen", ColorA | OnOff, (f, i) =>
        {
            long cyc = f.Ms / f.Period;
            long count = f.N - cyc % (f.N + 1);
            return Lit(f.A(i), i < count && f.Ms % f.Period < f.On);
        }),

        // ---------------------------------------------------------------- lopend (Stap tijd)
        E("chase-gap", "chase", "Lopend gat", ColorA | Step, (f, i) => Lit(f.A(i), i != (int)(f.Step % f.N))),
        E("chase-a-over-b", "chase", "A loopt over B", ColorA | ColorB | Step, (f, i) =>
            i == (int)(f.Step % f.N) ? new(f.A(i), 1.0) : new(f.B, 0.35)),
        E("chase-pairs", "chase", "Lopend in paren", ColorA | Step, (f, i) => Lit(f.A(i), Mod(i - f.Step, f.N) < 2)),
        E("zipper", "chase", "Rits", ColorA | ColorB | Step, (f, i) =>
        {
            int p = EffectLibrary.PingPong(f.Step, (f.N + 1) / 2);
            if (i == p) return new(f.A(i), 1.0);
            if (i == f.N - 1 - p) return new(f.B, 1.0);
            return new(f.A(i), 0.0);
        }),
        E("stacking", "chase", "Stapelen", ColorA | Step, (f, i) =>
        {
            long total = (long)f.N * (f.N + 1) / 2 + 3;     // all runs, then a short pause when full
            long s = f.Step % total;
            int k = 0, pos = -1;
            while (k < f.N)
            {
                int run = f.N - k;
                if (s < run) { pos = (int)s; break; }
                s -= run; k++;
            }
            bool stacked = i >= f.N - k;
            return Lit(f.A(i), stacked || i == pos);
        }),
        E("jumping", "chase", "Springend licht", ColorA | Step, (f, i) =>
        {
            int target = Math.Min(f.N - 1, (int)(Rnd(f.Step, 0) * f.N));
            return new(f.A(i), i == target ? 1.0 - 0.6 * f.Frac : 0.0);
        }),
        E("searchlight", "chase", "Zoeklicht", ColorA | Step, (f, i) =>
        {
            double pos = Fr(T(f, f.N)) * f.N;
            return new(f.A(i), Gauss(WrapDist(i, pos, f.N), 0.8));
        }),
        E("two-scanners", "chase", "Twee scanners", ColorA | ColorB | Step, (f, i) =>
        {
            double p1 = EffectLibrary.SmoothPingPong(T(f), f.N), p2 = f.N - 1 - p1;
            double l1 = Gauss(i - p1, 0.7), l2 = Gauss(i - p2, 0.7);
            return new(LerpLight(f.A(i), f.B, l2 / (l1 + l2 + 1e-9)), Math.Max(l1, l2));
        }),
        E("accelerating", "chase", "Versnellend lopen", ColorA | Step, (f, i) =>
        {
            double u = Fr(T(f, 2.0 * f.N));
            double pos = u * u * f.N;
            return new(f.A(i), Math.Max(0, 1 - Math.Abs(i - pos) * 0.8));
        }),
        E("gravity-ball", "chase", "Zwaartekrachtbal", ColorA | Step, (f, i) =>
        {
            double u = Fr(T(f, Math.Max(2, f.N)));
            double h = 1 - Math.Pow(2 * u - 1, 2);          // up and down like a thrown ball
            return new(f.A(i), Gauss(i - h * (f.N - 1), 0.6));
        }),
        E("train", "chase", "Treintje", ColorA | ColorB | Step, (f, i) =>
        {
            long m = Mod(i - f.Step, 6);
            return m == 2 ? new(f.B, 1.0) : new(f.A(i), m < 2 ? 1.0 : 0.0);
        }),
        E("snake", "chase", "Slang", ColorA | Step, (f, i) =>
        {
            long len = 1 + (f.Step / f.N) % Math.Max(1, f.N - 1);
            long behind = Mod(f.Step % f.N - i, f.N);
            return new(f.A(i), behind < len ? 1.0 - 0.7 * behind / (len + 1.0) : 0.0);
        }),
        E("color-fill", "chase", "Kleur vullen", ColorA | ColorB | Step, (f, i) =>
        {
            long cyc = f.Step / f.N, k = f.Step % f.N;
            bool aNow = cyc % 2 == 0;
            return new(i <= k == aNow ? f.A(i) : f.B, 1.0);
        }),
        E("rings", "chase", "Ringen uit het midden", Step, (f, i) =>
        {
            double d = Math.Abs(i - (f.N - 1) / 2.0);
            return new(f.Hue((f.Step - Math.Floor(d)) * 45), 1.0);
        }),
        E("wanderer", "chase", "Dwalend licht", ColorA | Step, (f, i) =>
            new(f.A(i), Gauss(i - SN(T(f, 4), 0) * (f.N - 1), 0.8))),
        E("even-odd-run", "chase", "Eerst even, dan oneven", ColorA | Step, (f, i) =>
        {
            int evens = (f.N + 1) / 2;
            int idx = (int)(f.Step % f.N);
            int target = idx < evens ? idx * 2 : (idx - evens) * 2 + 1;
            return Lit(f.A(i), i == target);
        }),
        E("running-pulse", "chase", "Lopende hartslag", ColorA | Step, (f, i) =>
        {
            double d = T(f) % (f.N + 4) - i;
            return new(f.A(i), Math.Min(1, Gauss(d, 0.35) + 0.6 * Gauss(d - 1.2, 0.35)));
        }),
        E("ladder", "chase", "Ladder", ColorA | Step, (f, i) =>
        {
            long s = f.Step % (f.N + 4);
            return Lit(f.A(i), s < f.N ? i <= s : (s - f.N) % 2 == 0);
        }),

        // ---------------------------------------------------------------- vloeiend
        E("breathe-ab", "smooth", "Ademen A en B", ColorA | ColorB | Step, (f, i) =>
        {
            double u = T(f, 8);
            return new(Math.Floor(u) % 2 == 0 ? f.A(i) : f.B, Sm(Fr(u)));
        }),
        E("breathe-alternate", "smooth", "Om en om ademen", ColorA | Step, (f, i) => new(f.A(i), Sm(T(f, 6) + (i % 2) * 0.5))),
        E("interference", "smooth", "Interferentie", ColorA | Step, (f, i) =>
        {
            double t = T(f, 4);
            return new(f.A(i), 0.5 + 0.25 * Math.Sin(2 * Math.PI * (t + i * 0.31)) + 0.25 * Math.Sin(2 * Math.PI * (-1.37 * t + i * 0.17)));
        }),
        E("plasma", "smooth", "Plasma", Step, (f, i) =>
        {
            double t = T(f, 10) * 2 * Math.PI;
            return new(f.Hue(180 + 120 * (Math.Sin(i * 0.55 + t) + Math.Sin(i * 0.21 - 1.3 * t))), 1.0);
        }),
        E("lava-lamp", "smooth", "Lavalamp", Step, (f, i) =>
        {
            double t = T(f, 12);
            return new(f.Hue(330 + 60 * SN(t * 0.7 + i * 0.35, 1)), 0.4 + 0.6 * SN(t + i * 0.2, 2));
        }, "#f06,#f40,#c0f"),
        E("pastel-cycle", "smooth", "Pastel kleurwissel", Step, (f, i) => new(Hsv(f, T(f, 30) * 360 + i * 20, 0.35), 1.0),
            "#fbb,#ffb,#bfb,#bbf"),
        E("rainbow-breathe", "smooth", "Ademende regenboog", Step, (f, i) =>
            new(f.Hue(i * 360.0 / f.N + T(f, 40) * 360), 0.15 + 0.85 * Sm(T(f, 8)))),
        E("rainbow-mirror", "smooth", "Gespiegelde regenboog", Step, (f, i) =>
        {
            double d = Math.Abs(i - (f.N - 1) / 2.0);
            return new(f.Hue(d * 288 / Math.Max(1, f.N / 2.0) - T(f, 20) * 360), 1.0);
        }),
        E("gradient-breathe", "smooth", "Ademend verloop", ColorA | ColorB | Step, (f, i) =>
            new(Mix(f, f.ColorA, f.ColorB, Pos(f, i)), 0.2 + 0.8 * Sm(T(f, 8)))),
        E("mirror-wave-ab", "smooth", "Spiegelgolf A / B", ColorA | ColorB | Step, (f, i) =>
        {
            double d = Math.Abs(Pos(f, i) - 0.5) * 2;
            return new(Mix(f, f.ColorA, f.ColorB, Sm(d * 0.75 - T(f, 8))), 1.0);
        }),
        E("sawtooth", "smooth", "Zaagtand", ColorA | Step, (f, i) => new(f.A(i), Fr(T(f, 4) - i / (double)f.N))),
        E("swell", "smooth", "Aanzwellen", ColorA | Step, (f, i) =>
        {
            double u = Fr(T(f, 24));
            return new(f.A(i), u < 0.92 ? Math.Pow(u / 0.92, 2) : 0.0);
        }),
        E("glow-points", "smooth", "Gloeiende punten", ColorA | Step, (f, i) =>
            new(f.A(i), Sm(T(f, 8) * (0.5 + Rnd(0, i)) + Rnd(1, i)))),
        E("white-temperature", "smooth", "Warm en koud wit", Step, (f, i) =>
            new(Mix(f, WarmWhite, ColdWhite, Sm(T(f, 30) + Pos(f, i) * 0.5)), 1.0), "#fa5,#fff,#cdf"),
        E("complementary", "smooth", "Complementair", ColorA | Step, (f, i) =>
            new(Mix(f, f.ColorA, Shift(f.ColorA, 180), Sm(T(f, 8) + Pos(f, i))), 1.0)),
        E("three-color-flow", "smooth", "Drie kleuren stromen", ColorA | ColorB | Step, (f, i) =>
        {
            Color c3 = Shift(f.ColorA, 120);
            double x = Fr(Pos(f, i) - T(f, 12)) * 3;
            int seg = (int)x;
            double k = Sm(0.5 * (x - seg));                 // ease from one colour to the next
            Color from = seg == 0 ? f.ColorA : seg == 1 ? f.ColorB : c3, to = seg == 0 ? f.ColorB : seg == 1 ? c3 : f.ColorA;
            return new(Mix(f, from, to, k), 1.0);
        }),
        E("shimmer", "smooth", "Glinsteren", ColorA | Step, (f, i) => new(f.A(i), 0.75 + 0.25 * SN(T(f, 0.5), i))),

        // ---------------------------------------------------------------- kleur
        E("random-pastels", "color", "Willekeurige pastels", Step, (f, i) => new(Hsv(f, Rnd(f.Step, i) * 360, 0.35), 1.0),
            "#fbb,#bfb,#bbf,#ffb"),
        E("random-fade-colors", "color", "Overvloeiende willekeurige kleuren", Step, (f, i) =>
        {
            Color a = ColorUtil.FromHsv(Rnd(f.Step, i) * 360, 1, 1), b = ColorUtil.FromHsv(Rnd(f.Step + 1, i) * 360, 1, 1);
            return new(Mix(f, a, b, 0.5 - 0.5 * Math.Cos(Math.PI * f.Frac)), 1.0);
        }),
        E("random-ab-off", "color", "Willekeurig A, B of uit", ColorA | ColorB | Step, (f, i) =>
        {
            int r = (int)(Rnd(f.Step, i) * 3);
            return r == 0 ? new(f.A(i), 1.0) : r == 1 ? new(f.B, 1.0) : new(f.A(i), 0.0);
        }),
        E("color-steps", "color", "Kleurstappen", Step, (f, i) => new(f.Hue(f.Step % 6 * 60), 1.0)),
        E("rgb-chase", "color", "RGB lopend", Step, (f, i) =>
        {
            long k = Mod(i + f.Step, 3);
            return new(k == 0 ? Light.Red : k == 1 ? Light.Green : Light.Blue, 1.0);
        }, "#f00,#0f0,#00f"),
        E("cmy-flow", "color", "Cyaan, magenta, geel", Step, (f, i) =>
        {
            double x = Fr((i - T(f)) / 3.0) * 3;
            int seg = (int)x;
            double[] hues = { 180, 300, 420 };
            return new(f.Hue(hues[seg] + (hues[(seg + 1) % 3] + (seg == 2 ? 360 : 0) - hues[seg]) * (x - seg)), 1.0);
        }, "#0ff,#f0f,#ff0"),
        E("color-pairs", "color", "Kleurparen", Step, (f, i) =>
            new(f.Hue(f.Step * 30 + (i / 2) * 60 + (i % 2) * 180), 1.0)),
        E("halves-ab", "color", "Helft A, helft B", ColorA | ColorB | Step, (f, i) =>
            new(i < f.N / 2.0 == (f.Step % 2 == 0) ? f.A(i) : f.B, 1.0)),
        E("color-blocks", "color", "Kleurblokken", Step, (f, i) => new(f.Hue(Rnd(f.Step, i / 3) * 360), 1.0)),
        E("warm-colors", "color", "Warme kleuren", Step, (f, i) => new(f.Hue(-30 + 80 * SN(T(f, 3), i)), 1.0), "#f03,#f60,#fc0"),
        E("cool-colors", "color", "Koele kleuren", Step, (f, i) => new(f.Hue(160 + 100 * SN(T(f, 3), i)), 1.0), "#0fc,#08f,#60f"),
        E("color-flash", "color", "Kleurflits", Step, (f, i) => new(f.Hue(Rnd(f.Step, 0) * 360), Math.Exp(-f.Frac * 4))),
        E("duotone", "color", "Duotoon", ColorA | ColorB | Step, (f, i) =>
            new(Mix(f, f.ColorA, f.ColorB, Math.Clamp((SN(T(f, 3), i) - 0.35) / 0.3, 0, 1)), 1.0)),

        // ---------------------------------------------------------------- sfeer & thema
        E("police-beacon", "theme", "Politie (zwaailicht)", Step, (f, i) =>
        {
            double th = 2 * Math.PI * T(f, 2), ph = 2 * Math.PI * i / f.N;
            double r = Math.Pow(Math.Max(0, Math.Cos(th - ph)), 6), b = Math.Pow(Math.Max(0, Math.Cos(th + Math.PI - ph)), 6);
            return new(r >= b ? Light.Red : Light.Blue, Math.Max(r, b));
        }, "#f00,#000,#00f,#000"),
        E("police-fast", "theme", "Politie (snel flitsen)", Step, (f, i) =>
        {
            bool flash = f.Frac < 0.6 && Fr(f.Frac / 0.6 * 3) < 0.5;
            bool left = i < f.N / 2.0, even = f.Step % 2 == 0;
            return Lit(left ? Light.Red : Light.Blue, flash && left == even);
        }, "#f00,#000,#00f"),
        E("amber-beacon", "theme", "Oranje zwaailicht", Step, (f, i) =>
            new(f.Rgb(Amber), Math.Pow(Math.Max(0, Math.Cos(2 * Math.PI * (T(f, 2) - i / (double)f.N))), 6)), "#f80,#000,#f80"),
        E("traffic-light", "theme", "Verkeerslicht", Step, (f, i) =>
        {
            long s = f.Step % 10;
            return new(s < 5 ? Light.Green : s == 5 ? f.Rgb(Amber) : Light.Red, 1.0);
        }, "#0f0,#fa0,#f00"),
        E("halloween", "theme", "Halloween", Step, (f, i) =>
        {
            if (Rnd(f.Step / 3, 99) < 0.08 && f.Frac < 0.3) return new(f.Rgb(C(0, 255, 60)), 1.0);
            return new(Mix(f, Orange, Purple, SN(T(f, 4), i)), 0.5 + 0.5 * SN(T(f, 2), i + 50));
        }, "#f60,#80f,#0f4"),
        E("valentine", "theme", "Valentijn", Step, (f, i) =>
        {
            double ph = Fr(T(f, 4));
            double beat = Math.Exp(-Math.Pow(ph / 0.05, 2)) + 0.7 * Math.Exp(-Math.Pow((ph - 0.22) / 0.05, 2));
            return new(Mix(f, Pink, C(255, 0, 30), Pos(f, i)), 0.35 + 0.65 * Math.Min(1, beat));
        }, "#f39,#f03,#f39"),
        E("kings-day", "theme", "Koningsdag", Step, (f, i) =>
        {
            if (Rnd(f.Step, i) < 0.08) return new(Light.White, 1.0);
            return new(Mix(f, C(255, 90, 0), C(255, 170, 0), Sm(Pos(f, i) - T(f, 6))), 1.0);
        }, "#f50,#fa0,#fff,#f50"),
        E("dutch-flag", "theme", "Nederlandse vlag", Step, (f, i) =>
        {
            int seg = (int)(Fr(i / (double)Math.Max(f.N, 3) + T(f, 2.0 * f.N)) * 3);
            return new(seg == 0 ? f.Rgb(C(200, 0, 20)) : seg == 1 ? Light.White : f.Rgb(C(0, 40, 160)), 1.0);
        }, "#c01,#fff,#028"),
        E("easter", "theme", "Pasen", Step, (f, i) =>
        {
            Color[] c = { C(255, 240, 120), C(150, 255, 150), C(200, 150, 255) };
            double x = Fr(Pos(f, i) * 1.5 + T(f, 16)) * 3;
            int seg = (int)x;
            return new(Mix(f, c[seg], c[(seg + 1) % 3], x - seg), 1.0);
        }, "#ff8,#9f9,#c9f"),
        E("sinterklaas", "theme", "Sinterklaas", Step, (f, i) =>
        {
            bool red = Mod(i + f.Step, 2) == 0;
            if (!red && Rnd(f.Step, i) < 0.15) return new(Light.White, 1.0);
            return new(f.Rgb(red ? C(220, 0, 0) : Gold), 1.0);
        }, "#d00,#fb2,#d00"),
        E("mirror-ball", "theme", "Discobal", Step, (f, i) =>
        {
            double d = Fr(i * 0.618 + T(f, 6));
            return new(Light.White, d < 0.12 ? 1 - d / 0.12 : 0.0);
        }, "#222,#fff,#222,#fff,#222"),
        E("footlights", "theme", "Voetlicht", Step, (f, i) =>
            new(f.Rgb(C(255, 150, 60)), 0.75 + 0.25 * Sm(T(f, 10) + Pos(f, i) * 0.5)), "#f96,#fb7,#f96"),
        E("neon-sign", "theme", "Neonreclame", ColorA | Step, (f, i) =>
        {
            bool buzzing = Rnd(f.Ms / 2000, i) < 0.15;
            return new(f.A(i), buzzing && Rnd(f.Ms / 50, i) < 0.5 ? 0.1 : 1.0);
        }),
        E("fake-tv", "theme", "Nep-tv", Step, (f, i) =>
        {
            long scene = (long)Math.Floor(T(f, 6));
            double hue = Rnd(scene, 1) < 0.5 ? 180 + 60 * Rnd(scene, 2) : 20 + 40 * Rnd(scene, 3);
            double flicker = 0.4 + 0.6 * SN(f.Ms / 120.0, 0);
            return new(Hsv(f, hue, 0.2 + 0.4 * Rnd(scene, 4)), flicker * (0.9 + 0.1 * Rnd(f.Ms / 120, i)));
        }, "#8af,#fff,#fc8"),
        E("alarm", "theme", "Alarm", Step, (f, i) =>
        {
            if (f.Step % 2 == 0) return Lit(Light.Red, (f.Ms / 60) % 2 == 0);
            return new(Light.Red, Math.Pow(Math.Max(0, Math.Cos(2 * Math.PI * (f.Frac - i / (double)f.N))), 6));
        }, "#f00,#000,#f00"),

        // ---------------------------------------------------------------- natuur
        E("sunrise", "nature", "Zonsopgang", Step, (f, i) =>
        {
            double u = Math.Clamp(Fr(T(f, 40)) - Pos(f, i) * 0.05, 0, 1);
            Color c = u < 0.5 ? ColorUtil.Lerp(C(120, 0, 0), C(255, 120, 0), u / 0.5) : ColorUtil.Lerp(C(255, 120, 0), C(255, 220, 170), (u - 0.5) / 0.5);
            return new(f.Rgb(c), 0.05 + 0.95 * Math.Min(1, u * 1.3));
        }, "#700,#f70,#fdb"),
        E("thunderstorm", "nature", "Onweer", Step, (f, i) =>
        {
            long slot = f.Ms / (f.StepMs * 4L);
            double fr = (f.Ms % (f.StepMs * 4L)) / (f.StepMs * 4.0);
            bool strike = Rnd(slot, 0) < 0.35 && (fr < 0.03 || (fr > 0.08 && fr < 0.12) || (fr > 0.2 && fr < 0.23));
            if (strike && Rnd(slot, i) > 0.25) return new(Light.White, 1.0);
            return new(f.Rgb(C(40, 50, 90)), 0.25 + 0.1 * SN(T(f, 2), i));
        }, "#235,#fff,#235"),
        E("rain", "nature", "Regen", Step, (f, i) =>
        {
            double x = T(f) + Rnd(5, i);
            long slot = (long)Math.Floor(x);
            return Rnd(slot, i) < 0.3 ? new(f.Rgb(C(180, 210, 255)), Math.Exp(-Fr(x) * 6)) : new(f.Rgb(C(0, 20, 80)), 0.08);
        }, "#014,#bdf,#014"),
        E("forest", "nature", "Bos", Step, (f, i) =>
        {
            Color leaf = ColorUtil.Lerp(C(0, 90, 10), C(60, 200, 40), SN(T(f, 6) + i * 0.3, 5));
            double sun = Math.Clamp((SN(T(f, 3), i + 100) - 0.75) * 4, 0, 1);
            return new(Mix(f, leaf, C(255, 230, 120), sun), 1.0);
        }, "#050,#3c2,#fe7"),
        E("underwater", "nature", "Onder water", Step, (f, i) =>
        {
            Color baseC = ColorUtil.Lerp(C(0, 60, 140), C(0, 170, 200), SN(T(f, 8) + Pos(f, i), 3));
            double caustic = Math.Pow(Sm(i * 0.37 + T(f, 3)) * Sm(i * 0.23 - T(f, 4.3)), 3);
            return new(Mix(f, baseC, Color.White, caustic * 0.8), 1.0);
        }, "#048,#0ac,#fff"),
        E("campfire", "nature", "Kampvuur", Step, (f, i) =>
        {
            double e = SN(f.Ms / 90.0, i);
            double flare = Rnd(f.Ms / 400, i) > 0.9 ? 1 : 0;
            return new(f.Hue(8 + 30 * e + 10 * flare), Math.Min(1, 0.35 + 0.55 * e + 0.1 * flare));
        }, "#a10,#f60,#fa0"),
        E("starry-night", "nature", "Sterrenhemel", Step, (f, i) =>
        {
            if (Rnd(i, 77) >= 0.35) return new(f.Rgb(C(0, 10, 60)), 0.3);
            return new(Mix(f, C(0, 10, 60), Color.White, 0.6 + 0.4 * SN(T(f, 2), i)), 1.0);
        }, "#013,#fff,#013"),
        E("fireflies", "nature", "Vuurvliegjes", Step, (f, i) =>
        {
            double x = T(f, 6) + Rnd(3, i);
            bool active = Rnd((long)Math.Floor(x), i) < 0.4;
            return new(f.Rgb(C(170, 255, 40)), active ? Sm(Fr(x)) : 0.0);
        }, "#000,#af2,#000"),
        E("desert-heat", "nature", "Woestijnhitte", Step, (f, i) =>
            new(Mix(f, C(255, 120, 30), C(255, 200, 120), SN(T(f, 2) + Pos(f, i) * 2, i % 3)), 0.75 + 0.25 * SN(T(f), i)), "#f72,#fc7"),
        E("tide", "nature", "Eb en vloed", Step, (f, i) =>
        {
            double h = Sm(T(f, 30)) * f.N;
            if (i < h - 1) return new(f.Rgb(C(0, 60, 255)), 0.9);
            if (i < h) return new(Mix(f, C(0, 60, 255), Color.White, 0.7), 1.0);
            return new(f.Rgb(C(0, 60, 255)), 0.0);
        }, "#03f,#fff,#000"),
        E("wind-grass", "nature", "Wind door het gras", Step, (f, i) =>
        {
            double gust = Math.Pow(Sm(Pos(f, i) - T(f, 8)), 2);
            return new(Mix(f, C(40, 160, 20), C(150, 255, 90), gust), 0.4 + 0.6 * gust * (0.6 + 0.4 * SN(T(f, 2), i)));
        }, "#2a1,#9f5"),
        E("lava-flow", "nature", "Lavastroom", Step, (f, i) =>
        {
            double v = SN(Pos(f, i) * 3 - T(f, 10), 9);
            return new(Mix(f, C(80, 0, 0), C(255, 110, 0), v), 0.3 + 0.7 * v);
        }, "#500,#f60,#500"),

        // ---------------------------------------------------------------- feest
        E("fireworks", "party", "Vuurwerk", Step, (f, i) =>
        {
            double x = T(f, 3);
            long slot = (long)Math.Floor(x);
            double age = Fr(x), centre = Rnd(slot, 0) * (f.N - 1), d = Math.Abs(i - centre);
            if (age < 0.08 && d < 1) return new(Light.White, 1.0);
            double ring = Gauss(d - age * f.N * 0.6, 0.8);
            return new(f.Hue(Rnd(slot, 1) * 360), ring * Math.Pow(1 - age, 1.5));
        }, "#000,#f3c,#ff3,#3cf,#000"),
        E("confetti", "party", "Confetti", Step, (f, i) =>
        {
            double x = T(f) + Rnd(2, i);
            long slot = (long)Math.Floor(x);
            return new(f.Hue(Rnd(slot, i) * 360), Rnd(slot + 7, i) < 0.6 ? Math.Exp(-Fr(x) * 3) : 0.0);
        }),
        E("strobe-chase", "party", "Strobo lopend", Step, (f, i) =>
            Lit(Light.White, i == (int)(f.Step % f.N) && (f.Ms / 35) % 2 == 0), "#000,#fff,#000"),
        E("rave", "party", "Rave", Step, (f, i) =>
            (f.Ms / 45) % 3 == 0 ? new(Light.White, 1.0) : new(f.Hue(Rnd(f.Ms / 80, i) * 360), 0.8)),
        E("laser", "party", "Laser", ColorA | Step, (f, i) =>
            new(f.A(i), Gauss(i - EffectLibrary.SmoothPingPong(T(f, 0.25), f.N), 0.25))),
        E("party-wave", "party", "Feestgolf", Step, (f, i) =>
            new(f.Hue(Pos(f, i) * 120 + f.Step * 40), f.Frac < 0.1 ? 1.0 : 0.6 + 0.4 * Sm(Pos(f, i) - T(f, 2)))),
        E("polka-dots", "party", "Stippen", ColorA | ColorB | Step, (f, i) =>
        {
            double x = Fr((i + T(f, 2)) / 3.0);
            return new(LerpLight(f.B, f.A(i), Gauss(Math.Min(x, 1 - x) * 3, 0.35)), 1.0);
        }),
        E("build-up-drop", "party", "Opbouw naar de drop", Step, (f, i) =>
        {
            long s = f.Step % 32;
            if (s >= 28) return new(f.Hue(i * 360.0 / f.N + f.Ms / 4.0), 1.0);   // the drop
            double u = s / 28.0;
            bool flash = Fr(T(f) * (1 + u * 12)) < 0.25;
            return new(Light.White, flash ? 0.3 + 0.7 * u : 0.0);
        }, "#222,#fff,#f3c,#3cf"),

        // ---------------------------------------------------------------- geluid
        E("audio-beat-strobe", "sound", "Strobo op de maat", Beat, (f, i) => Lit(Light.White, f.Audio.BeatPhase < 0.12)),
        E("audio-beat-bounce", "sound", "Heen en weer op de maat", ColorA | Beat, (f, i) =>
            new(f.A(i), i == EffectLibrary.PingPong(Math.Max(0, f.Audio.BeatIndex), f.N) ? 1 - 0.5 * f.Audio.BeatPhase : 0.0)),
        E("audio-beat-build", "sound", "Opbouwen op de maat", ColorA | Beat, (f, i) =>
        {
            long k = Mod(f.Audio.BeatIndex, f.N + 1);
            return Lit(f.A(i), k == f.N || i < k);
        }),
        E("audio-bass-center", "sound", "Baspuls uit het midden", EffectParams.Audio, (f, i) =>
        {
            double d = Math.Abs(Pos(f, i) - 0.5) * 2;
            return new(f.Hue(280 - 200 * f.Audio.Mid), Math.Clamp((f.Audio.Bass - d) * 4, 0, 1));
        }),
        E("audio-high-confetti", "sound", "Kleurfonkels op hoog", EffectParams.Audio, (f, i) =>
        {
            long slot = f.Ms / 50;
            return Rnd(slot, i) < f.Audio.High * 0.7 ? new(f.Hue(Rnd(slot, i + 999) * 360), 1.0) : new(Light.White, 0.0);
        }),
        E("audio-beat-alternate", "sound", "Om en om op de maat", ColorA | ColorB | Beat, (f, i) =>
            new((i % 2 == 0) == (f.Audio.BeatIndex % 2 == 0) ? f.A(i) : f.B, 0.3 + 0.7 * Math.Exp(-f.Audio.BeatPhase * 3))),
        E("audio-mid-wave", "sound", "Golf op de zang", ColorA | EffectParams.Audio, (f, i) =>
            new(f.A(i), f.Audio.Mid * Sm(Pos(f, i) - f.Ms / 2000.0))),
    };

    // ---------------------------------------------------------------- helpers

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    static LampOutput Lit(Light c, bool on) => new(c, on ? 1.0 : 0.0);
    /// <summary>Time in steps (or in units of <paramref name="steps"/> steps), continuous.</summary>
    static double T(EffectFrame f, double steps = 1) => f.Ms / (f.StepMs * steps);
    static double Fr(double x) => x - Math.Floor(x);
    /// <summary>Smooth 0 → 1 → 0 over one unit of x (repeats).</summary>
    static double Sm(double x) => 0.5 - 0.5 * Math.Cos(2 * Math.PI * x);
    /// <summary>Lamp position 0..1 along the row.</summary>
    static double Pos(EffectFrame f, int i) => f.N <= 1 ? 0.5 : i / (double)(f.N - 1);
    static double Gauss(double d, double w) => Math.Exp(-(d * d) / (2 * w * w));
    static double WrapDist(double a, double b, int n) { double d = Math.Abs(a - b) % n; return Math.Min(d, n - d); }
    static long Mod(long a, long n) => ((a % n) + n) % n;
    static long FloorDiv(long a, long n) => (long)Math.Floor(a / (double)n);
    static double Rnd(long slot, int i) => EffectLibrary.Noise(slot, i);
    /// <summary>Smooth noise: random values per whole x, eased in between.</summary>
    static double SN(double x, int i)
    {
        long s = (long)Math.Floor(x);
        double a = Rnd(s, i), b = Rnd(s + 1, i), k = 0.5 - 0.5 * Math.Cos(Math.PI * (x - s));
        return a + (b - a) * k;
    }
    static Light Hsv(EffectFrame f, double h, double s = 1, double v = 1) =>
        f.Rgb(ColorUtil.FromHsv(h, Math.Clamp(s, 0, 1), Math.Clamp(v, 0, 1)));
    static Light Mix(EffectFrame f, Color a, Color b, double t) => f.Rgb(ColorUtil.Lerp(a, b, t));
    static Light LerpLight(Light a, Light b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int L(int x, int y) => (int)Math.Round(x + (y - x) * t);
        return new Light(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B), L(a.W, b.W), L(a.A, b.A), L(a.UV, b.UV));
    }
    /// <summary>The same colour with its hue turned by <paramref name="degrees"/>.</summary>
    static Color Shift(Color c, double degrees)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0, min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0;
        double s = max <= 0 ? 0 : (max - min) / max;
        return ColorUtil.FromHsv(c.GetHue() + degrees, s, max);
    }
}
