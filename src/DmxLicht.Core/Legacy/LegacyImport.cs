using System.Drawing;
using System.Text.Json;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Core.Legacy;

/// <summary>v1.4 compatibility: its "N identical lamps" layout as a rig, and importing its settings.</summary>
public static class LegacyImport
{
    /// <summary>
    /// The v1.4 layout as a rig of identical fixtures, rendered exactly like v1.4 did
    /// (colours a lamp has no LED for are dropped, not mixed).
    /// </summary>
    public static Rig ToRig(FixtureLayout lay)
    {
        FixtureMode mode = ModeFromMap(lay.Map, lay.FaderChannelCount, "v1.4");
        var type = new FixtureType { Id = "v14-layout", Manufacturer = "v1.4", Model = "Lamp", Modes = { mode } };
        var list = new List<(PatchedFixture, FixtureType, FixtureMode)>();
        int lamps = Math.Min(lay.LampCount, FixtureLayout.MaxLamps);
        for (int i = 0; i < lamps; i++)
            list.Add((new PatchedFixture { Id = "lamp" + i, Name = "Lamp " + (i + 1), TypeId = type.Id, Mode = mode.Name, Address = lay.BaseAddress(i) + 1 }, type, mode));
        return new Rig(list) { FoldMissingEmitters = false };
    }

    /// <summary>Channels in v1.4's write order: Dimmer, R, G, B, W, A, UV.</summary>
    public static FixtureMode ModeFromMap(ChannelMap m, int footprint, string name)
    {
        var mode = new FixtureMode { Name = name, Footprint = footprint };
        void Add(int n, ChannelKind k, string label) { if (n > 0) mode.Channels.Add(new ChannelDef { Number = n, Kind = k, Label = label }); }
        Add(m.Dimmer, ChannelKind.Dimmer, "Dim");
        Add(m.Red, ChannelKind.Red, "R");
        Add(m.Green, ChannelKind.Green, "G");
        Add(m.Blue, ChannelKind.Blue, "B");
        Add(m.White, ChannelKind.White, "W");
        Add(m.Amber, ChannelKind.Amber, "A");
        Add(m.UV, ChannelKind.UV, "UV");
        return mode;
    }

    /// <summary>
    /// Reads v1.4's DmxLicht_v1.4.ini and eigen_kleuren.txt from <paramref name="settingsDir"/>.
    /// A "custom" lamp type is written to <paramref name="fixturesDir"/> so the patch can refer to it.
    /// </summary>
    public static ShowFile TryImport(string settingsDir, string fixturesDir)
    {
        string ini = Path.Combine(settingsDir, "DmxLicht_v1.4.ini");
        if (!File.Exists(ini)) return null;
        try
        {
            var d = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(ini))
            {
                int p = line.IndexOf('=');
                if (p > 0) d[line[..p]] = line[(p + 1)..];
            }
            int I(string k, int def) => d.TryGetValue(k, out string v) && int.TryParse(v, out int x) ? x : def;

            var lay = new FixtureLayout
            {
                LampCount = Math.Clamp(I("lamps", 10), 1, 64),
                StartAddress = Math.Clamp(I("start", 1), 1, 512),
                Spacing = Math.Clamp(I("per", 8), 1, 64),
                PresetIndex = Math.Clamp(I("preset", 0), 0, LampPresets.CustomIndex),
                Map = new ChannelMap(I("dim", 0), I("r", 1), I("g", 2), I("b", 3), I("w", 4), I("a", 5), I("uv", 6)),
            };

            (string typeId, string mode) = lay.PresetIndex switch
            {
                0 => ("ledpar-18x7-rgbwauv", "6ch"),
                1 => ("ledpar-18x7-rgbwauv", "10ch"),
                2 => ("generic-rgbwauv", "8ch Dim+Strobe"),
                3 => ("generic-rgbw", "4ch"),
                _ => (null, null),
            };
            if (typeId == null)
            {
                FixtureMode m = ModeFromMap(lay.Map, Math.Max(lay.Spacing, lay.Map.Dimmer), "v1.4");
                var custom = new FixtureType { Id = "v14-aangepast", Manufacturer = "v1.4", Model = "Aangepaste lamp", Colors = "", Modes = { m } };
                Directory.CreateDirectory(fixturesDir);
                File.WriteAllText(Path.Combine(fixturesDir, custom.Id + ".json"), JsonSerializer.Serialize(custom, Json.Options));
                (typeId, mode) = (custom.Id, m.Name);
            }

            var show = new ShowFile();
            for (int i = 0; i < lay.LampCount; i++)
                show.Patch.Add(new PatchedFixture { Name = "Lamp " + (i + 1), TypeId = typeId, Mode = mode, Address = Math.Clamp(lay.BaseAddress(i) + 1, 1, 512) });

            ShowState s = show.State;
            s.BrightnessPercent = Math.Clamp(I("bright", 100), 5, 100);
            s.UseWhiteLed = I("white", 1) == 1;
            s.Effect.EffectIndex = Math.Clamp(I("fx", 8), 0, 39);
            s.Effect.StepMs = I("step", 400); s.Effect.OnMs = I("on", 300); s.Effect.OffMs = I("off", 300); s.Effect.DelayMs = I("delay", 150);
            s.Effect.ColorPerLamp = I("perlamp", 0) == 1;
            if (d.TryGetValue("chaseA", out string ca)) s.Effect.ColorA = ColorUtil.ParseHex(ca, s.Effect.ColorA);
            if (d.TryGetValue("chaseB", out string cb)) s.Effect.ColorB = ColorUtil.ParseHex(cb, s.Effect.ColorB);
            if (d.TryGetValue("effcolors", out string ec))
                s.Effect.LampColors = ec.Split(',').Select(h => ColorUtil.ParseHex(h, Color.White)).ToArray();
            show.Settings.DongleSpeed = Math.Clamp(I("dongle", 1), 0, 2);

            string customFile = Path.Combine(settingsDir, "eigen_kleuren.txt");
            if (File.Exists(customFile))
                s.CustomColors = File.ReadAllLines(customFile).Select(l => l.Trim()).Where(l => l.StartsWith('#') && l.Length == 7).ToList();

            Log.Write("Instellingen van v1.4 overgenomen (" + lay.LampCount + " lampen)");
            return show;
        }
        catch (Exception ex)
        {
            Log.Write("v1.4-instellingen overnemen mislukt: " + ex.Message);
            return null;
        }
    }
}
