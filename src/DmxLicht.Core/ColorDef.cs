using System.Drawing;

namespace DmxLicht.Core;

/// <summary>
/// A named colour as shown on screen. <see cref="L"/> optionally fixes the exact LED mix
/// (for amber/UV mixes that RGB cannot express); otherwise the LED mix is derived from <see cref="Show"/>.
/// </summary>
public sealed class ColorDef
{
    /// <summary>Stable id for palette colours ("red", "uv-white"); null for custom colours.</summary>
    public string Key { get; }
    /// <summary>Dutch display name (the UI translates by <see cref="Key"/>).</summary>
    public string Name { get; }
    public Color Show { get; }
    public Light? L { get; }

    public ColorDef(string name, int r, int g, int b) : this(null, name, Color.FromArgb(r, g, b), null) { }

    public ColorDef(string key, string name, Color show, Light? l)
    {
        Key = key; Name = name; Show = show; L = l;
    }

    public Light ToLight(bool useWhite) => L ?? Light.FromColor(Show, useWhite);
}

/// <summary>Reference to a colour in saved state: a palette key, or a custom hex colour.</summary>
public sealed class ColorRef
{
    public string Key { get; set; }
    public string Hex { get; set; }

    public static ColorRef Palette(string key) => new() { Key = key };
    public static ColorRef Custom(Color c) => new() { Hex = ColorUtil.Hex(c) };

    public ColorDef Resolve()
    {
        if (Key != null && DmxLicht.Core.Palette.ByKey.TryGetValue(Key, out ColorDef d)) return d;
        Color c = ColorUtil.ParseHex(Hex ?? "#FFFFFF", Color.White);
        return new ColorDef("Eigen", c.R, c.G, c.B);
    }
}
