using DmxLicht.Core.Fixtures;

namespace DmxLicht.Core.Show;

/// <summary>Reads a built frame back into on-screen colours per head, so the UI shows what the lamps do.</summary>
public static class Preview
{
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
