using System.Drawing;

namespace DmxLicht.Core;

/// <summary>The fixed colour tiles: a 3x24 fan (saturated, light, warm-to-cold white) and the amber/UV specials.</summary>
public static class Palette
{
    // Declared first: static fields initialise in order and BuildFan() needs these.
    static readonly string[] HueKeys = { "red", "vermilion", "orange", "gold", "yellow", "lemon", "lime", "bright-green", "green", "spring-green", "mint", "turquoise",
                                         "cyan", "sky-blue", "azure", "cobalt", "blue", "indigo", "violet", "purple", "magenta", "fuchsia", "pink", "crimson" };
    static readonly string[] WhiteKeys = { "candle", "candle-warm", "glow", "glow-light", "amber-white", "warm-white", "warm-white-light", "soft-white", "soft-white-light", "cream", "ivory", "white",
                                           "bright-white", "daylight", "daylight-light", "fresh-white", "cold-white", "cold-white-light", "ice-white", "ice-white-light", "moonlight", "moonlight-light", "mist-blue", "steel-blue" };

    public static readonly IReadOnlyList<ColorDef> Fan = BuildFan();
    public static readonly IReadOnlyList<ColorDef> Specials = BuildSpecials();
    public static readonly IReadOnlyDictionary<string, ColorDef> ByKey = Fan.Concat(Specials).ToDictionary(c => c.Key);

    /// <summary>"Wit": all LEDs including the white LED at full.</summary>
    public static ColorDef White => Fan[59];

    static Color WhiteLadder(int i)
    {
        int[] ai = { 0, 5, 11, 17, 23 };
        Color[] ac = { Color.FromArgb(255, 147, 41), Color.FromArgb(255, 190, 130), Color.FromArgb(255, 255, 255), Color.FromArgb(201, 226, 255), Color.FromArgb(150, 180, 255) };
        for (int k = 0; k < 4; k++)
            if (i <= ai[k + 1]) return ColorUtil.Lerp(ac[k], ac[k + 1], (i - ai[k]) / (double)(ai[k + 1] - ai[k]));
        return ac[4];
    }

    static List<ColorDef> BuildFan()
    {
        string[] hn = { "Rood", "Vermiljoen", "Oranje", "Goud", "Geel", "Citroen", "Limoen", "Felgroen", "Groen", "Lentegroen", "Mintgroen", "Turquoise",
                        "Cyaan", "Hemelblauw", "Azuur", "Kobalt", "Blauw", "Indigo", "Violet", "Paars", "Magenta", "Fuchsia", "Roze", "Karmijn" };
        string[] wn = { "Kaars", "Kaars warm", "Gloed", "Gloed licht", "Amberwit", "Warm wit", "Warm wit licht", "Zacht wit", "Zacht wit licht", "Creme", "Ivoor", "Wit",
                        "Helder wit", "Daglicht", "Daglicht licht", "Fris wit", "Koud wit", "Koud wit licht", "IJswit", "IJswit licht", "Maanlicht", "Maanlicht licht", "Nevelblauw", "Staalblauw" };
        var l = new List<ColorDef>();
        for (int i = 0; i < 24; i++) l.Add(new ColorDef(HueKeys[i], hn[i], ColorUtil.FromHsv(i * 15.0, 1.0, 1.0), null));
        for (int i = 0; i < 24; i++) l.Add(new ColorDef("light-" + HueKeys[i], "Licht " + hn[i], ColorUtil.FromHsv(i * 15.0, 0.5, 1.0), null));
        for (int i = 0; i < 24; i++)
        {
            if (i == 11) l.Add(new ColorDef("white", "Wit", Color.White, Light.White));
            else l.Add(new ColorDef(WhiteKeys[i], wn[i], WhiteLadder(i), null));
        }
        return l;
    }

    // amber, UV en mengkleuren met de extra leds
    static List<ColorDef> BuildSpecials() => new()
    {
        new ColorDef("amber", "Amber", Color.FromArgb(255, 191, 0), new Light(0, 0, 0, 0, 255, 0)),
        new ColorDef("amber-light", "Amber licht", Color.FromArgb(255, 210, 120), new Light(0, 0, 0, 70, 180, 0)),
        new ColorDef("sunset-red", "Avondrood", Color.FromArgb(255, 90, 0), new Light(255, 0, 0, 0, 255, 0)),
        new ColorDef("yellow-amber", "Geel+amber", Color.FromArgb(255, 225, 0), new Light(255, 255, 0, 0, 200, 0)),
        new ColorDef("uv", "UV", Color.FromArgb(106, 0, 255), new Light(0, 0, 0, 0, 0, 255)),
        new ColorDef("uv-blue", "UV+blauw", Color.FromArgb(60, 0, 255), new Light(0, 0, 255, 0, 0, 255)),
        new ColorDef("uv-red", "UV+rood", Color.FromArgb(190, 0, 160), new Light(255, 0, 0, 0, 0, 255)),
        new ColorDef("uv-white", "UV+wit", Color.FromArgb(190, 170, 255), new Light(0, 0, 0, 200, 0, 255)),
        new ColorDef("white-amber", "Wit+amber", Color.FromArgb(255, 225, 170), new Light(0, 0, 0, 255, 255, 0)),
        new ColorDef("red-white", "Rood+wit", Color.FromArgb(255, 140, 140), new Light(255, 0, 0, 150, 0, 0)),
        new ColorDef("green-white", "Groen+wit", Color.FromArgb(150, 255, 150), new Light(0, 255, 0, 150, 0, 0)),
        new ColorDef("blue-white", "Blauw+wit", Color.FromArgb(150, 170, 255), new Light(0, 0, 255, 150, 0, 0)),
    };
}
