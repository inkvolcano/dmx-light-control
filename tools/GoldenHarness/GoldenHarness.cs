// Golden-frame harness: compiled together with the ORIGINAL v1.4 source (C# 5, .NET Framework csc),
// it drives the original MainForm through reflection and records the DMX frame it builds for many
// scenarios. The new engine must reproduce these frames byte for byte (see tests/DmxLicht.Tests).
//
// Run tools\GoldenHarness\MakeGolden.bat to regenerate tests\DmxLicht.Tests\golden\frames.txt.
// Line format:  key=value;key=value;...|<hex of channels 1..last non-zero>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace DmxLicht
{
    static class GoldenHarness
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static MainForm form;
        static readonly Type T = typeof(MainForm);
        static Type colorDefType, modeType;
        static IList fan, specials;
        static MethodInfo buildFrame;
        static Random rng = new Random(12345);

        static readonly int[][] PresetMaps = {
            new[] { 0, 1, 2, 3, 4, 5, 6 }, new[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { 0, 1, 2, 3, 4, 0, 0 } };
        static readonly string[] MapFields = { "nDim", "nR", "nG", "nB", "nW", "nA", "nUV" };

        static object Get(string name) { return T.GetField(name, BF).GetValue(form); }
        static void Set(string name, object v) { T.GetField(name, BF).SetValue(form, v); }
        static void Num(string name, int v) { ((NumericUpDown)Get(name)).Value = v; }

        class Case
        {
            public int Mode, Lamps, Start, Per, Preset, Bright, Fx, Step, On, Off, Delay;
            public int[] Map;
            public bool White, PerLamp;
            public Color A, B;
            public Color[] EffColors = new Color[64];
            public bool[] LampOn = new bool[64];
            public string[] Colors = new string[64];
            public byte[,] Faders = new byte[64, 65];
            public long Ms;
        }

        [STAThread]
        static int Main(string[] args)
        {
            string outPath = args.Length > 0 ? args[0] : "frames.txt";
            form = new MainForm();
            Set("loading", true);
            colorDefType = T.GetNestedType("ColorDef", BF);
            modeType = T.GetNestedType("Mode", BF);
            fan = (IList)Get("fanList");
            specials = (IList)Get("specialList");
            buildFrame = T.GetMethod("BuildFrame", BF);

            List<Case> cases = new List<Case>();
            AddEffectCases(cases);
            for (int k = 0; k < 80; k++) cases.Add(RandomCase(0));
            for (int k = 0; k < 40; k++) cases.Add(RandomCase(2));

            StringBuilder sb = new StringBuilder();
            foreach (Case c in cases)
            {
                Apply(c);
                byte[] f = (byte[])buildFrame.Invoke(form, new object[] { c.Ms });
                sb.Append(Describe(c)).Append('|').Append(FrameHex(f)).Append('\n');
            }
            File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine("Wrote " + cases.Count + " frames to " + outPath);
            return 0;
        }

        // ---------------- scenarios ----------------
        static Case Layout(int preset, int lamps, int start, int per, bool white, int bright)
        {
            Case c = new Case();
            c.Preset = preset; c.Lamps = lamps; c.Start = start; c.Per = per; c.White = white; c.Bright = bright;
            c.Map = preset < 4 ? (int[])PresetMaps[preset].Clone() : RandomMap(per);
            c.A = Color.FromArgb(0, 255, 0); c.B = Color.FromArgb(255, 0, 0);
            c.Step = 400; c.On = 300; c.Off = 300; c.Delay = 150; c.Fx = 8;
            string[] defHex = { "#00FF00", "#FF0000", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF" };
            for (int i = 0; i < 64; i++)
            {
                c.EffColors[i] = Util.ParseHex(defHex[i % 6], Color.White);
                c.Colors[i] = "f59";
            }
            return c;
        }

        static int[] RandomMap(int per)
        {
            int[] m = new int[7];
            int max = Math.Min(64, Math.Max(per, 1) + 2);
            for (int i = 0; i < 7; i++) m[i] = rng.Next(3) == 0 ? 0 : rng.Next(1, max + 1);
            return m;
        }

        static Color RandomColor() { return Color.FromArgb(rng.Next(256), rng.Next(256), rng.Next(256)); }

        static void AddEffectCases(List<Case> cases)
        {
            List<Case> cfg = new List<Case>();
            Case a = Layout(0, 10, 1, 8, true, 100); cfg.Add(a);

            Case b = Layout(1, 7, 17, 10, false, 63);
            b.PerLamp = true; b.Step = 130; b.On = 90; b.Off = 210; b.Delay = 40;
            b.A = Color.FromArgb(255, 128, 0); b.B = Color.FromArgb(32, 64, 192);
            for (int i = 0; i < 64; i++) b.EffColors[i] = RandomColor();
            cfg.Add(b);

            Case c = Layout(3, 32, 3, 4, true, 5);
            c.PerLamp = true; c.Step = 1000; c.On = 1000; c.Off = 50; c.Delay = 333;
            for (int i = 0; i < 64; i++) c.EffColors[i] = RandomColor();
            cfg.Add(c);

            Case d = Layout(2, 1, 505, 8, true, 100);
            d.Step = 40; d.On = 20; d.Off = 20; d.Delay = 0; d.A = Color.White; d.B = Color.Black;
            cfg.Add(d);

            Case e = Layout(4, 13, 100, 12, false, 77);
            e.Map = new[] { 3, 9, 1, 12, 0, 5, 7 };
            e.Step = 41; e.On = 20; e.Off = 20; e.Delay = 9999; e.A = Color.FromArgb(200, 180, 160); e.B = Color.FromArgb(10, 250, 90);
            cfg.Add(e);

            Case f = Layout(0, 2, 1, 6, true, 50);
            f.PerLamp = true; f.Step = 5000; f.On = 7000; f.Off = 20; f.Delay = 10000;
            for (int i = 0; i < 64; i++) f.EffColors[i] = RandomColor();
            cfg.Add(f);

            long[] times = { 0, 1, 7, 39, 401, 1234, 9999, 65537, 1000003, 86400077 };
            foreach (Case baseCase in cfg)
                for (int fx = 0; fx < 40; fx++)
                    foreach (long ms in times)
                    {
                        Case x = Clone(baseCase);
                        x.Mode = 1; x.Fx = fx; x.Ms = ms;
                        cases.Add(x);
                    }
        }

        static Case RandomCase(int mode)
        {
            int preset = rng.Next(5);
            Case c = Layout(preset, rng.Next(1, 33), rng.Next(1, 513), rng.Next(1, 17), rng.Next(2) == 0, rng.Next(5, 101));
            c.Mode = mode;
            c.Ms = rng.Next(100000);
            for (int i = 0; i < 64; i++)
            {
                c.LampOn[i] = rng.Next(3) != 0;
                int kind = rng.Next(3);
                if (kind == 0) c.Colors[i] = "f" + rng.Next(fan.Count);
                else if (kind == 1) c.Colors[i] = "s" + rng.Next(specials.Count);
                else c.Colors[i] = Util.Hex(RandomColor());
                for (int ch = 1; ch <= 64; ch++) c.Faders[i, ch] = (byte)rng.Next(256);
            }
            return c;
        }

        static Case Clone(Case s)
        {
            Case c = (Case)typeof(Case).GetMethod("MemberwiseClone", BF).Invoke(s, null);
            c.Map = (int[])s.Map.Clone();
            c.EffColors = (Color[])s.EffColors.Clone();
            c.LampOn = (bool[])s.LampOn.Clone();
            c.Colors = (string[])s.Colors.Clone();
            c.Faders = (byte[,])s.Faders.Clone();
            return c;
        }

        // ---------------- drive the original form ----------------
        static void Apply(Case c)
        {
            ((ComboBox)Get("cmbPreset")).SelectedIndex = c.Preset;
            Num("nLamps", c.Lamps); Num("nStart", c.Start); Num("nChPer", c.Per);
            for (int i = 0; i < 7; i++) Num(MapFields[i], c.Map[i]);
            ((TrackBar)Get("tbBright")).Value = c.Bright;
            ((CheckBox)Get("chkWhite")).Checked = c.White;
            ((CheckBox)Get("chkPerLamp")).Checked = c.PerLamp;
            Num("nStep", c.Step); Num("nOn", c.On); Num("nOff", c.Off); Num("nDelay", c.Delay);
            Set("fxIdx", c.Fx);
            Set("chaseA", c.A); Set("chaseB", c.B);
            Set("mode", Enum.ToObject(modeType, c.Mode));

            Color[] eff = (Color[])Get("effColors");
            bool[] on = (bool[])Get("lampOn");
            Array defs = (Array)Get("lampDef");
            byte[,] fad = (byte[,])Get("faderVal");
            for (int i = 0; i < 64; i++)
            {
                eff[i] = c.EffColors[i];
                on[i] = c.LampOn[i];
                defs.SetValue(ColorFromToken(c.Colors[i]), i);
                for (int ch = 0; ch <= 64; ch++) fad[i, ch] = c.Faders[i, ch];
            }
        }

        static object ColorFromToken(string t)
        {
            if (t[0] == 'f') return fan[int.Parse(t.Substring(1))];
            if (t[0] == 's') return specials[int.Parse(t.Substring(1))];
            Color col = Util.ParseHex(t, Color.White);
            return Activator.CreateInstance(colorDefType, BF, null, new object[] { "Eigen", (int)col.R, (int)col.G, (int)col.B }, null);
        }

        // ---------------- output ----------------
        static string Describe(Case c)
        {
            StringBuilder s = new StringBuilder();
            s.Append("mode=").Append(c.Mode)
             .Append(";preset=").Append(c.Preset).Append(";lamps=").Append(c.Lamps).Append(";start=").Append(c.Start).Append(";per=").Append(c.Per)
             .Append(";map=").Append(string.Join(",", Array.ConvertAll(c.Map, x => x.ToString())))
             .Append(";bright=").Append(c.Bright).Append(";white=").Append(c.White ? 1 : 0).Append(";ms=").Append(c.Ms);
            if (c.Mode == 1)
            {
                s.Append(";fx=").Append(c.Fx).Append(";step=").Append(c.Step).Append(";on=").Append(c.On).Append(";off=").Append(c.Off)
                 .Append(";delay=").Append(c.Delay).Append(";perlamp=").Append(c.PerLamp ? 1 : 0)
                 .Append(";a=").Append(Util.Hex(c.A)).Append(";b=").Append(Util.Hex(c.B));
                if (c.PerLamp)
                {
                    string[] h = new string[c.Lamps];
                    for (int i = 0; i < c.Lamps; i++) h[i] = Util.Hex(c.EffColors[i]);
                    s.Append(";effcolors=").Append(string.Join(",", h));
                }
            }
            else if (c.Mode == 0)
            {
                StringBuilder bits = new StringBuilder();
                string[] cols = new string[c.Lamps];
                for (int i = 0; i < c.Lamps; i++) { bits.Append(c.LampOn[i] ? '1' : '0'); cols[i] = c.Colors[i]; }
                s.Append(";lampon=").Append(bits).Append(";colors=").Append(string.Join(",", cols));
            }
            else
            {
                string[] rows = new string[c.Lamps];
                for (int i = 0; i < c.Lamps; i++)
                {
                    StringBuilder r = new StringBuilder();
                    for (int ch = 1; ch <= 12; ch++) r.Append(c.Faders[i, ch].ToString("X2"));
                    rows[i] = r.ToString();
                }
                s.Append(";faders=").Append(string.Join(",", rows));
            }
            return s.ToString();
        }

        static string FrameHex(byte[] f)
        {
            int last = 0;
            for (int i = 1; i <= 512; i++) if (f[i] != 0) last = i;
            StringBuilder s = new StringBuilder(last * 2);
            for (int i = 1; i <= last; i++) s.Append(f[i].ToString("X2"));
            return s.ToString();
        }
    }
}
