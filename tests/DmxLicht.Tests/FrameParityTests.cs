using System.Drawing;
using System.Text;
using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Legacy;
using DmxLicht.Core.Show;

namespace DmxLicht.Tests;

/// <summary>
/// The new engine must produce exactly the same DMX bytes as the original v1.4 for every recorded
/// scenario (all 40 effects on 6 layouts at 10 moments, plus random static-colour and fader cases).
/// </summary>
public class FrameParityTests
{
    public static IEnumerable<object[]> GoldenLines()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "golden", "frames.txt");
        int n = 0;
        foreach (string line in File.ReadLines(path))
        {
            n++;
            if (line.Length > 0) yield return new object[] { n, line };
        }
    }

    [Fact]
    public void GoldenFileCoversAllEffects()
    {
        var fx = GoldenLines().Select(o => Parse((string)o[1]).spec)
            .Where(s => s["mode"] == "1").Select(s => int.Parse(s["fx"])).Distinct().Count();
        Assert.Equal(40, fx); // the 40 effects of v1.4; newer effects have their own tests
    }

    [Theory]
    [MemberData(nameof(GoldenLines))]
    public void FrameMatchesOriginal(int lineNo, string line)
    {
        var (spec, expected) = Parse(line);
        var (rig, s) = BuildState(spec);
        byte[] f = FrameBuilder.Build(rig, s, long.Parse(spec["ms"]));
        string actual = FrameHex(f);
        Assert.True(expected == actual, $"line {lineNo}: {spec["mode"]}/{(spec.TryGetValue("fx", out var x) ? x : "-")}\n" +
                                        $"expected {expected}\nactual   {actual}");
    }

    static (Dictionary<string, string> spec, string frame) Parse(string line)
    {
        int bar = line.IndexOf('|');
        var spec = line[..bar].Split(';').Select(kv => kv.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
        return (spec, line[(bar + 1)..]);
    }

    static (Rig, ShowState) BuildState(Dictionary<string, string> d)
    {
        int[] map = d["map"].Split(',').Select(int.Parse).ToArray();
        var layout = new FixtureLayout
        {
            PresetIndex = int.Parse(d["preset"]),
            LampCount = int.Parse(d["lamps"]),
            StartAddress = int.Parse(d["start"]),
            Spacing = int.Parse(d["per"]),
            Map = new ChannelMap(map[0], map[1], map[2], map[3], map[4], map[5], map[6]),
        };
        Rig rig = LegacyImport.ToRig(layout);
        var s = new ShowState
        {
            Mode = (OutputMode)int.Parse(d["mode"]),
            BrightnessPercent = int.Parse(d["bright"]),
            UseWhiteLed = d["white"] == "1",
        };
        int lamps = layout.LampCount;

        if (s.Mode == OutputMode.Effect)
        {
            s.Effect = new EffectSettings
            {
                EffectIndex = int.Parse(d["fx"]),
                StepMs = int.Parse(d["step"]),
                OnMs = int.Parse(d["on"]),
                OffMs = int.Parse(d["off"]),
                DelayMs = int.Parse(d["delay"]),
                ColorPerLamp = d["perlamp"] == "1",
                ColorA = ColorUtil.ParseHex(d["a"], Color.Empty),
                ColorB = ColorUtil.ParseHex(d["b"], Color.Empty),
            };
            if (d.TryGetValue("effcolors", out string eff))
            {
                string[] h = eff.Split(',');
                for (int i = 0; i < h.Length; i++) s.Effect.LampColors[i] = ColorUtil.ParseHex(h[i], Color.Empty);
            }
        }
        else if (s.Mode == OutputMode.Static)
        {
            string bits = d["lampon"];
            string[] cols = d["colors"].Split(',');
            for (int i = 0; i < lamps; i++)
            {
                FixtureLook look = s.Look(rig.Fixtures[i].Patch.Id);
                look.On = bits[i] == '1';
                look.Color = cols[i][0] switch
                {
                    'f' => ColorRef.Palette(Palette.Fan[int.Parse(cols[i][1..])].Key),
                    's' => ColorRef.Palette(Palette.Specials[int.Parse(cols[i][1..])].Key),
                    _ => ColorRef.Custom(ColorUtil.ParseHex(cols[i], Color.White)),
                };
            }
        }
        else
        {
            string[] rows = d["faders"].Split(',');
            int cnt = layout.FaderChannelCount;
            for (int i = 0; i < lamps; i++)
            {
                byte[] v = s.FaderValues(rig.Fixtures[i].Patch.Id, cnt);
                for (int c = 1; c <= cnt; c++) v[c] = Convert.ToByte(rows[i].Substring((c - 1) * 2, 2), 16);
            }
        }
        return (rig, s);
    }

    static string FrameHex(byte[] f)
    {
        int last = 0;
        for (int i = 1; i <= 512; i++) if (f[i] != 0) last = i;
        var sb = new StringBuilder(last * 2);
        for (int i = 1; i <= last; i++) sb.Append(f[i].ToString("X2"));
        return sb.ToString();
    }
}
