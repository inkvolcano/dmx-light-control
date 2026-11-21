using System.Drawing;

namespace DmxLicht.Core;

/// <summary>Output for one lamp, per LED emitter (0..255): red, green, blue, white, amber, UV.</summary>
public readonly record struct Light(int R, int G, int B, int W, int A, int UV)
{
    public static readonly Light White = new(255, 255, 255, 255, 0, 0);
    public static readonly Light Red = new(255, 0, 0, 0, 0, 0);
    public static readonly Light Green = new(0, 255, 0, 0, 0, 0);
    public static readonly Light Blue = new(0, 0, 255, 0, 0, 0);

    /// <summary>
    /// RGB colour to LED values. With <paramref name="useWhite"/> the shared part of R, G and B
    /// moves to the white LED (brighter and cleaner pastel colours).
    /// </summary>
    public static Light FromColor(Color c, bool useWhite)
    {
        int r = c.R, g = c.G, b = c.B, w = 0;
        if (useWhite)
        {
            w = Math.Min(r, Math.Min(g, b));
            r -= w; g -= w; b -= w;
        }
        return new Light(r, g, b, w, 0, 0);
    }
}
