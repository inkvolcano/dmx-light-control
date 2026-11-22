using DmxLicht.Core.Effects;
using DmxLicht.Core.Fixtures;

namespace DmxLicht.Core.Show;

/// <summary>Reads a built frame back into on-screen colours per head, so the UI shows what the lamps do.</summary>
public static class Preview
{
    /// <summary>
    /// A short loop of an effect as on-screen colours, for the moving example on an effect tile: per frame
    /// "#RRGGBB" per lamp. Sound effects get a made-up song (a kick on every beat at 120 BPM) so they move too.
    /// </summary>
    public static string[][] EffectFrames(EffectSettings s, int lamps, int frames, int frameMs)
    {
        lamps = Math.Clamp(lamps, 1, 16); frames = Math.Clamp(frames, 1, 200); frameMs = Math.Clamp(frameMs, 10, 1000);
        var result = new string[frames][];
        for (int f = 0; f < frames; f++)
        {
            long ms = (long)f * frameMs;
            double beat = ms / 500.0, phase = beat - Math.Floor(beat);
            double kick = Math.Exp(-phase * 6), wave = 0.5 + 0.5 * Math.Sin(ms / 700.0);
            var audio = new AudioFrame(0.35 + 0.5 * kick, kick, 0.3 + 0.4 * wave, 0.2 + 0.5 * (1 - wave), beat, phase < 0.05 && (long)beat % 8 == 0);
            LampOutput[] o = EffectLibrary.Render(s, false, ms, lamps, audio);
            var row = new string[o.Length];
            for (int i = 0; i < o.Length; i++)
            {
                Light c = FrameBuilder.FoldAll(o[i].Color);
                double lv = Math.Clamp(o[i].Level, 0, 1);
                row[i] = "#" + Math.Clamp((int)Math.Round(c.R * lv), 0, 255).ToString("X2") + Math.Clamp((int)Math.Round(c.G * lv), 0, 255).ToString("X2") + Math.Clamp((int)Math.Round(c.B * lv), 0, 255).ToString("X2");
            }
            result[f] = row;
        }
        return result;
    }

    /// <summary>"#RRGGBB" per head, in <see cref="Rig.Heads"/> order.</summary>
    public static string[] HeadColors(Rig rig, byte[] frame)
    {
        var result = new string[rig.Heads.Count];
        foreach (RigFixture fx in rig.Fixtures)
        {
            int b = fx.BaseChannel;
            int Val(ChannelDef c) { int ch = b + c.Number; return ch >= 1 && ch < frame.Length ? frame[ch] : 0; }
            double globalDim = 1.0;
            ChannelDef gd = fx.Mode.Channels.LastOrDefault(c => c.Kind == ChannelKind.Dimmer && c.Cell < 0);
            if (gd != null) globalDim = Val(gd) / 255.0;

            for (int cell = 0; cell < fx.CellCount; cell++)
            {
                int r = 0, g = 0, bl = 0, w = 0, a = 0, uv = 0;
                double h = -1, s = 0, v = 0, dim = globalDim;
                foreach (ChannelDef c in fx.Mode.Channels)
                {
                    if (c.Cell >= 0 && c.Cell != cell) continue;
                    switch (c.Kind)
                    {
                        case ChannelKind.Red: r = Val(c); break;
                        case ChannelKind.Green: g = Val(c); break;
                        case ChannelKind.Blue: bl = Val(c); break;
                        case ChannelKind.White: w = Val(c); break;
                        case ChannelKind.Amber: a = Val(c); break;
                        case ChannelKind.UV: uv = Val(c); break;
                        case ChannelKind.Hue: h = Val(c) / 255.0 * 360.0; break;
                        case ChannelKind.Saturation: s = Val(c) / 255.0; break;
                        case ChannelKind.Value: v = Val(c) / 255.0; break;
                        case ChannelKind.Dimmer when c.Cell == cell: dim *= Val(c) / 255.0; break;
                    }
                }
                Light rgb;
                if (h >= 0)
                {
                    var col = ColorUtil.FromHsv(h, s, v);
                    rgb = new Light(col.R, col.G, col.B, 0, 0, 0);
                }
                else rgb = FrameBuilder.FoldAll(new Light(r, g, bl, w, a, uv));
                int R = (int)Math.Round(rgb.R * dim), G = (int)Math.Round(rgb.G * dim), B = (int)Math.Round(rgb.B * dim);
                result[fx.FirstHead + cell] = "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
            }
        }
        return result;
    }
}
