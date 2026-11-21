using System.Drawing;
using DmxLicht.Core.Effects;

namespace DmxLicht.Core.Show;

/// <summary>A ready-made scene, built for whatever fixtures the show has.</summary>
public sealed class SceneTemplate
{
    public string Key { get; init; }
    public string Name { get; init; }      // Dutch
    public string NameEn { get; init; }
    /// <summary>"basic", "mood", "party", "theme".</summary>
    public string Category { get; init; }
    public string Color { get; init; }     // button colour
    public int FadeMs { get; init; } = 1500;
    /// <summary>Fixture ids in patch order to the scene's look.</summary>
    public Func<IReadOnlyList<string>, SceneState> Build { get; init; }
}

/// <summary>The built-in scene templates (Voorbeeldscènes).</summary>
public static class SceneTemplates
{
    public static readonly IReadOnlyList<SceneTemplate> All = new[]
    {
        // ---------- basis ----------
        Static("warm-white", "Warm wit", "Warm white", "basic", "warm-white"),
        Static("cold-white", "Koud wit", "Cold white", "basic", "cold-white"),
        Static("daylight", "Daglicht", "Daylight", "basic", "daylight"),
        Static("full-white", "Vol wit", "Full white", "basic", "white"),
        Static("red", "Rood", "Red", "basic", "red"),
        Static("green", "Groen", "Green", "basic", "green"),
        Static("blue", "Blauw", "Blue", "basic", "blue"),
        Static("purple", "Paars", "Purple", "basic", "purple"),
        Static("pink", "Roze", "Pink", "basic", "pink"),
        Static("amber", "Amber", "Amber", "basic", "amber"),
        Static("uv", "UV / blacklight", "UV / black light", "basic", "uv"),
        Template("all-off", "Alles uit", "All off", "basic", "#222226", ids => new SceneState { Mode = OutputMode.Static, Looks = ids.ToDictionary(id => id, _ => new FixtureLook { On = false }) }, fade: 1000),

        // ---------- sfeer ----------
        Static("candle-light", "Kaarslicht", "Candlelight", "mood", "candle", fade: 3000),
        Template("romantic", "Romantisch", "Romantic", "mood", "#B03060", ids => Alternate(ids, Hex("#B02848"), Hex("#C05018")), fade: 3000),
        Template("sunset-gradient", "Zonsondergang", "Sunset", "mood", "#FF6A20", ids => Gradient(ids, Hex("#FFB020"), Hex("#C01868")), fade: 4000),
        Template("ocean-calm", "Oceaan", "Ocean", "mood", "#0080FF", ids => Effect("ocean", step: 300), fade: 3000),
        Template("aurora-mood", "Noorderlicht", "Northern lights", "mood", "#40E0A0", ids => Effect("aurora", step: 400), fade: 3000),
        Template("forest", "Bos", "Forest", "mood", "#2E8B3A", ids => Gradient(ids, Hex("#20A040"), Hex("#80B020")), fade: 3000),
        Template("ice", "IJs", "Ice", "mood", "#A0D8FF", ids => Alternate(ids, Hex("#A0D8FF"), Hex("#4060FF")), fade: 3000),
        Template("breathe-warm", "Rustig ademen", "Slow breathing", "mood", "#FF9040", ids => Effect("breathe-wave", a: Hex("#FF9040"), step: 700), fade: 2000),
        Template("rainbow-still", "Regenboog (stil)", "Rainbow (still)", "mood", "#FF4060", ids => Rainbow(ids), fade: 2000),

        // ---------- feest ----------
        Template("party-rainbow", "Feest: regenboog", "Party: rainbow", "party", "#FF3080", ids => Effect("rainbow-smooth", on: 300, off: 300), fade: 500),
        Template("party-disco", "Disco op de maat", "Disco on the beat", "party", "#C040FF", ids => Effect("audio-beat-random"), fade: 300),
        Template("party-dancefloor", "Dansvloer", "Dance floor", "party", "#FF2050", ids => Effect("audio-beat-colors"), fade: 300),
        Template("party-vu", "VU-meter", "VU meter", "party", "#40FF60", ids => Effect("audio-vu", a: Color.FromArgb(0, 255, 0), b: Color.FromArgb(255, 0, 0)), fade: 300),
        Template("party-spectrum", "Spectrum", "Spectrum", "party", "#4060FF", ids => Effect("audio-spectrum"), fade: 300),
        Template("party-scanner", "Scanner", "Scanner", "party", "#FF2020", ids => Effect("scanner", a: Color.FromArgb(255, 0, 0), step: 120), fade: 300),
        Template("party-drop", "Strobo bij de drop", "Strobe on the drop", "party", "#FFFFFF", ids => Effect("audio-surge-strobe", a: Hex("#8020FF")), fade: 300),
        Template("party-chase", "Lopend op de maat", "Chase on the beat", "party", "#FFB000", ids => Effect("audio-beat-chase"), fade: 300),

        // ---------- thema ----------
        Template("police", "Politie", "Police", "theme", "#2040FF", ids => Effect("police", step: 250), fade: 0),
        Template("fire-brigade", "Brandweer", "Fire brigade", "theme", "#FF2020", ids => Effect("fire-brigade", step: 250), fade: 0),
        Template("christmas", "Kerst", "Christmas", "theme", "#20A040", ids => Effect("christmas", step: 1500), fade: 2000),
        Template("halloween", "Halloween", "Halloween", "theme", "#FF7000", ids => Alternate(ids, Hex("#FF6000"), Hex("#7A00FF")), fade: 2000),
        Template("kingsday", "Koningsdag", "King's Day", "theme", "#FF7A00", ids => Static(ids, "orange"), fade: 1500),
        Template("valentine", "Valentijn", "Valentine", "theme", "#FF3070", ids => Alternate(ids, Hex("#FF1040"), Hex("#FF60B0")), fade: 2000),
        Template("thunder", "Onweer", "Thunderstorm", "theme", "#5060A0", ids => Effect("lightning", step: 400), fade: 500),
        Template("campfire", "Kampvuur", "Campfire", "theme", "#FF5010", ids => Effect("fire"), fade: 2000),
        Template("nl-flag", "Rood-wit-blauw", "Red-white-blue", "theme", "#2040A0", ids => Stripes(ids, "red", "white", "blue"), fade: 1500),
    };

    public static SceneTemplate Find(string key) => All.FirstOrDefault(t => t.Key == key);

    // ---------- builders ----------

    static SceneTemplate Template(string key, string nl, string en, string cat, string color, Func<IReadOnlyList<string>, SceneState> build, int fade = 1500) =>
        new() { Key = key, Name = nl, NameEn = en, Category = cat, Color = color, FadeMs = fade, Build = build };

    static SceneTemplate Static(string key, string nl, string en, string cat, string paletteKey, int fade = 1500) =>
        Template(key, nl, en, cat, ColorUtil.Hex(Palette.ByKey[paletteKey].Show), ids => Static(ids, paletteKey), fade);

    static SceneState Static(IReadOnlyList<string> ids, string paletteKey) => new()
    {
        Mode = OutputMode.Static,
        Looks = ids.ToDictionary(id => id, _ => new FixtureLook { On = true, Color = ColorRef.Palette(paletteKey) }),
    };

    static SceneState Alternate(IReadOnlyList<string> ids, Color a, Color b) => new()
    {
        Mode = OutputMode.Static,
        Looks = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => new FixtureLook { On = true, Color = ColorRef.Custom(x.i % 2 == 0 ? a : b) }),
    };

    static SceneState Stripes(IReadOnlyList<string> ids, params string[] keys) => new()
    {
        Mode = OutputMode.Static,
        Looks = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id,
            x => new FixtureLook { On = true, Color = ColorRef.Palette(keys[Math.Min(keys.Length - 1, x.i * keys.Length / Math.Max(1, ids.Count))]) }),
    };

    static SceneState Gradient(IReadOnlyList<string> ids, Color a, Color b) => new()
    {
        Mode = OutputMode.Static,
        Looks = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id,
            x => new FixtureLook { On = true, Color = ColorRef.Custom(ColorUtil.Lerp(a, b, ids.Count <= 1 ? 0 : x.i / (double)(ids.Count - 1))) }),
    };

    static SceneState Rainbow(IReadOnlyList<string> ids) => new()
    {
        Mode = OutputMode.Static,
        Looks = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id,
            x => new FixtureLook { On = true, Color = ColorRef.Custom(ColorUtil.FromHsv(x.i * 360.0 / Math.Max(1, ids.Count), 1, 1)) }),
    };

    static SceneState Effect(string key, Color? a = null, Color? b = null, int step = 400, int on = 300, int off = 300)
    {
        int index = EffectLibrary.IndexOf(key);
        if (index < 0) throw new InvalidOperationException("unknown effect " + key);
        var e = new EffectSettings { EffectIndex = index, StepMs = step, OnMs = on, OffMs = off };
        if (a is Color ca) e.ColorA = ca;
        if (b is Color cb) e.ColorB = cb;
        return new SceneState { Mode = OutputMode.Effect, Effect = e };
    }

    static Color Hex(string hex) => ColorUtil.ParseHex(hex, Color.White);
}
