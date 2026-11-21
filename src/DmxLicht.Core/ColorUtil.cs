using System.Drawing;

namespace DmxLicht.Core;

public static class ColorUtil
{
    public static Color FromHsv(double h, double s, double v)
    {
        h = h % 360.0; if (h < 0) h += 360.0;
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    public static string Hex(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

    public static Color ParseHex(string s, Color def)
    {
        try
        {
            s = s.Trim().TrimStart('#');
            if (s.Length != 6) return def;
            return Color.FromArgb(Convert.ToInt32(s.Substring(0, 2), 16), Convert.ToInt32(s.Substring(2, 2), 16), Convert.ToInt32(s.Substring(4, 2), 16));
        }
        catch { return def; }
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Black or white, whichever reads best on top of <paramref name="c"/>.</summary>
    public static Color Contrast(Color c)
    {
        double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        return lum > 140 ? Color.Black : Color.White;
    }
}
