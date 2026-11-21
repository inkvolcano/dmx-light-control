using DmxLicht.Core.Effects;
using DmxLicht.Core.Fixtures;

namespace DmxLicht.Core.Show;

/// <summary>Turns the rig plus the show state into one DMX frame.</summary>
public static class FrameBuilder
{
    /// <summary>Builds the frame at time <paramref name="ms"/>. Index 1..512 = DMX channel 1..512 of universe 1, 513.. = universe 2, and so on; index 0 is unused.</summary>
    /// <param name="masterScale">Extra factor on the master (sound-to-light); 1 = none.</param>
    /// <param name="audio">Sound and beat for sound-reactive effects.</param>
    public static byte[] Build(Rig rig, ShowState s, long ms, double masterScale = 1.0, AudioFrame audio = default)
    {
        var f = new byte[rig.FrameSize];
        double master = s.Blackout ? 0.0 : s.BrightnessPercent / 100.0;
        if (masterScale != 1.0) master *= Math.Clamp(masterScale, 0.0, 1.0);
        strobeOn = s.StrobeActive;
        strobeSpeed = Math.Clamp(s.StrobeSpeed, 1, 100);
        // software strobe for lamps without a strobe channel: 1..20 Hz, 25 % on
        double hz = 1 + (strobeSpeed - 1) * 19.0 / 99.0;
        softFlash = !strobeOn || ((ms / 1000.0 * hz) % 1.0) < 0.25;

        switch (s.Mode)
        {
            case OutputMode.Static:
                foreach (RigFixture fx in rig.Fixtures)
                {
                    if (!s.Looks.TryGetValue(fx.Patch.Id, out FixtureLook look) || !look.On)
                    {
                        // off: colours 0, but fixed channels and a shutter keep their resting value, so a fade
                        // between off and on never sweeps a shutter through its strobe range (SGM P-2)
                        WriteResting(f, fx);
                        continue;
                    }
                    Light c = look.Color.Resolve().ToLight(s.UseWhiteLed);
                    var colors = new Light[fx.CellCount];
                    var levels = new double[fx.CellCount];
                    Array.Fill(colors, c);
                    Array.Fill(levels, 1.0);
                    WriteFixture(f, rig, fx, colors, levels, master);
                }
                break;

            case OutputMode.Effect:
                if (rig.Heads.Count == 0) break;
                LampOutput[] heads = RenderEffect(s, s.Effect, ms, rig.Heads.Count, audio);
                foreach (RigFixture fx in rig.Fixtures)
                {
                    var colors = new Light[fx.CellCount];
                    var levels = new double[fx.CellCount];
                    for (int c = 0; c < fx.CellCount; c++)
                    {
                        int h = fx.FirstHead + c;
                        if (h < heads.Length) { colors[c] = heads[h].Color; levels[c] = heads[h].Level; }
                    }
                    WriteFixture(f, rig, fx, colors, levels, master);
                }
                break;

            case OutputMode.Faders:
                if (s.Blackout) break;
                foreach (RigFixture fx in rig.Fixtures)
                {
                    s.Faders.TryGetValue(fx.Patch.Id, out byte[] v);
                    int b = fx.BaseChannel;
                    for (int c = 1; c <= fx.Mode.Footprint; c++)
                        Put(f, b + c, v != null && c < v.Length ? v[c] : 0);
                }
                break;
        }

        // Group effects on top, in order: each one repaints only its group's fixtures.
        foreach (EffectLayer layer in s.Layers)
        {
            if (layer?.GroupId == null) continue;
            int[] members = layer.GroupId == "*" ? Enumerable.Range(0, rig.Fixtures.Count).ToArray()
                          : rig.Groups.TryGetValue(layer.GroupId, out int[] g) ? g : null;
            if (members == null || members.Length == 0) continue;
            int headCount = members.Sum(i => rig.Fixtures[i].CellCount);
            LampOutput[] heads = RenderEffect(s, layer.Effect, ms, headCount, audio);
            int h = 0;
            foreach (int i in members)
            {
                RigFixture fx = rig.Fixtures[i];
                var colors = new Light[fx.CellCount];
                var levels = new double[fx.CellCount];
                for (int c = 0; c < fx.CellCount; c++, h++)
                    if (h < heads.Length) { colors[c] = heads[h].Color; levels[c] = heads[h].Level; }
                WriteFixture(f, rig, fx, colors, levels, master);
            }
        }

        // "Identify": blink the fixture white at full, whatever else is going on.
        if (s.Identify.Count > 0)
        {
            bool lit = (ms % 500) < 250;
            foreach (RigFixture fx in rig.Fixtures)
            {
                if (!s.Identify.Contains(fx.Patch.Id)) continue;
                var colors = new Light[fx.CellCount];
                var levels = new double[fx.CellCount];
                Array.Fill(colors, Light.White);
                Array.Fill(levels, lit ? 1.0 : 0.0);
                WriteFixture(f, rig, fx, colors, levels, 1.0);
            }
        }
        return f;
    }

    /// <summary>
    /// Writes one fixture. Intensity goes to a dimmer channel when the lamp has one (single-segment lamps:
    /// level x master; multi-segment lamps: master, with each segment's level on its colours);
    /// otherwise it scales the colour channels.
    /// </summary>
    /// <summary>A library effect, or the user's own chase when <see cref="EffectSettings.ChaseId"/> is set.</summary>
    static LampOutput[] RenderEffect(ShowState s, EffectSettings e, long ms, int heads, AudioFrame audio)
    {
        if (e.ScreenSync) return ScreenFeed.Render(s.ScreenSync, s.UseWhiteLed, heads);
        if (e.ChaseId != null)
        {
            CustomChase c = s.Chases?.FirstOrDefault(x => x.Id == e.ChaseId);
            if (c != null) return ChaseRenderer.Render(c, s.UseWhiteLed, ms, heads, audio);
        }
        return EffectLibrary.Render(e, s.UseWhiteLed, ms, heads, audio);
    }

    [ThreadStatic] static bool strobeOn, softFlash;
    [ThreadStatic] static int strobeSpeed;

    static void WriteFixture(byte[] f, Rig rig, RigFixture fx, Light[] colors, double[] levels, double master)
    {
        FixtureMode mode = fx.Mode;
        if (strobeOn && !softFlash && !mode.Channels.Any(c => c.Kind == ChannelKind.Strobe)) master = 0;
        int b = fx.BaseChannel;
        int cells = mode.CellCount;
        bool globalDim = mode.Channels.Any(c => c.Kind == ChannelKind.Dimmer && c.Cell < 0);

        for (int i = 0; i < levels.Length; i++) levels[i] = Math.Clamp(levels[i], 0.0, 1.0);

        foreach (ChannelDef ch in mode.Channels)
        {
            int cell = Math.Clamp(ch.Cell < 0 ? 0 : ch.Cell, 0, cells - 1);
            double lvl = levels[cell];
            switch (ch.Kind)
            {
                case ChannelKind.Dimmer:
                    double k = ch.Cell >= 0 ? (globalDim ? lvl : lvl * master)
                             : (cells == 1 ? lvl * master : master);
                    Put(f, b + ch.Number, (int)Math.Round(255 * k));
                    break;

                case ChannelKind.Red:
                case ChannelKind.Green:
                case ChannelKind.Blue:
                case ChannelKind.White:
                case ChannelKind.Amber:
                case ChannelKind.UV:
                    Light e = rig.FoldMissingEmitters ? Fold(colors[cell], mode, cell) : colors[cell];
                    Put(f, b + ch.Number, (int)Math.Round(Emitter(e, ch.Kind) * ColorScale(mode, cell, cells, globalDim, lvl, master)));
                    break;

                case ChannelKind.Hue:
                case ChannelKind.Saturation:
                case ChannelKind.Value:
                    Light rgb = FoldAll(colors[cell]);
                    var (h, s, v) = ToHsv(rgb.R, rgb.G, rgb.B);
                    double val = ch.Kind == ChannelKind.Hue ? h * 255.0
                               : ch.Kind == ChannelKind.Saturation ? s * 255.0
                               : v * 255.0 * ColorScale(mode, cell, cells, globalDim, lvl, master);
                    Put(f, b + ch.Number, (int)Math.Round(val));
                    break;

                case ChannelKind.Fixed:
                    Put(f, b + ch.Number, ch.Default);
                    break;

                case ChannelKind.Strobe:
                    Put(f, b + ch.Number, strobeOn
                        ? (int)Math.Round(ch.RangeMin + (ch.RangeMax - ch.RangeMin) * (strobeSpeed - 1) / 99.0)
                        : ch.Default);
                    break;
            }
        }
    }

    static double ColorScale(FixtureMode mode, int cell, int cells, bool globalDim, double lvl, double master)
    {
        bool cellDim = mode.Channels.Any(c => c.Kind == ChannelKind.Dimmer && c.Cell == cell && c.Cell >= 0);
        if (cellDim) return 1.0;
        if (globalDim) return cells == 1 ? 1.0 : lvl;
        return lvl * master;
    }

    static int Emitter(Light c, ChannelKind k) => k switch
    {
        ChannelKind.Red => c.R,
        ChannelKind.Green => c.G,
        ChannelKind.Blue => c.B,
        ChannelKind.White => c.W,
        ChannelKind.Amber => c.A,
        _ => c.UV,
    };

    /// <summary>Mixes white, amber and UV from R/G/B when the lamp has no LED of its own for them.</summary>
    internal static Light Fold(Light c, FixtureMode mode, int cell)
    {
        int r = c.R, g = c.G, b = c.B, w = c.W, a = c.A, uv = c.UV;
        if (w > 0 && !mode.Has(ChannelKind.White, cell)) { r += w; g += w; b += w; w = 0; }
        if (a > 0 && !mode.Has(ChannelKind.Amber, cell)) { r += a; g += (int)Math.Round(a * 0.75); a = 0; }
        if (uv > 0 && !mode.Has(ChannelKind.UV, cell)) { r += (int)Math.Round(uv * 0.4); b += uv; uv = 0; }
        return new Light(Math.Min(r, 255), Math.Min(g, 255), Math.Min(b, 255), w, a, uv);
    }

    internal static Light FoldAll(Light c)
    {
        int r = c.R + c.W + c.A + (int)Math.Round(c.UV * 0.4);
        int g = c.G + c.W + (int)Math.Round(c.A * 0.75);
        int b = c.B + c.W + c.UV;
        return new Light(Math.Min(r, 255), Math.Min(g, 255), Math.Min(b, 255), 0, 0, 0);
    }

    /// <summary>RGB 0..255 to hue/saturation/value 0..1.</summary>
    internal static (double h, double s, double v) ToHsv(int r, int g, int b)
    {
        double R = r / 255.0, G = g / 255.0, B = b / 255.0;
        double max = Math.Max(R, Math.Max(G, B)), min = Math.Min(R, Math.Min(G, B)), d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == R) h = ((G - B) / d) % 6;
            else if (max == G) h = (B - R) / d + 2;
            else h = (R - G) / d + 4;
            h /= 6; if (h < 0) h += 1;
        }
        return (h, max == 0 ? 0 : d / max, max);
    }

    /// <summary>Only the fixed and strobe/shutter channels of a fixture that is off, at their resting values.</summary>
    static void WriteResting(byte[] f, RigFixture fx)
    {
        int b = fx.BaseChannel;
        foreach (ChannelDef ch in fx.Mode.Channels)
            if (ch.Kind is ChannelKind.Fixed or ChannelKind.Strobe && ch.Default != 0) Put(f, b + ch.Number, ch.Default);
    }

    static void Put(byte[] f, int ch, int v)
    {
        if (ch < 1 || ch >= f.Length) return;
        f[ch] = (byte)Math.Clamp(v, 0, 255);
    }
}
