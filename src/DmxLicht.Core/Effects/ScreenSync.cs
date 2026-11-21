using System.Drawing;

namespace DmxLicht.Core.Effects;

/// <summary>How the screen is laid over the lamps.</summary>
public enum ScreenSyncMode
{
    /// <summary>Screen split into columns, left to right over the lamps.</summary>
    Zones,
    /// <summary>All lamps the average colour of the screen.</summary>
    Average,
    /// <summary>Real ambilight: lamps around the TV take the colour of the nearest edge of the picture.</summary>
    Edges,
    /// <summary>Markers: every lamp takes the colour of its own spot on the picture (placed by the user).</summary>
    Points,
}

/// <summary>Where the colours come from.</summary>
public enum ScreenSource
{
    /// <summary>This PC's own screen.</summary>
    Screen,
    /// <summary>A webcam pointed at a TV (or any screen): only the framed area counts.</summary>
    Camera,
}

/// <summary>Settings for "follow the screen" (saved with the show).</summary>
public sealed class ScreenSyncSettings
{
    public ScreenSource Source { get; set; } = ScreenSource.Screen;
    /// <summary>Webcam name; null/empty = the first real (not virtual) camera.</summary>
    public string Camera { get; set; }
    /// <summary>
    /// The TV's corners in the camera picture, 0..1: top-left x,y, top-right x,y, bottom-right x,y, bottom-left x,y.
    /// A TV filmed at an angle is straightened by this frame.
    /// </summary>
    public double[] Corners { get; set; } = DefaultCorners();
    public static double[] DefaultCorners() => new[] { 0.1, 0.1, 0.9, 0.1, 0.9, 0.9, 0.1, 0.9 };

    public ScreenSyncMode Mode { get; set; } = ScreenSyncMode.Zones;
    /// <summary>Monitor number (0 = main screen).</summary>
    public int Monitor { get; set; }
    /// <summary>Colour boost in %, 100 = as on screen.</summary>
    public int Saturation { get; set; } = 140;
    /// <summary>0 = immediate .. 100 = very calm.</summary>
    public int Smoothing { get; set; } = 50;
    /// <summary>True: dark screen = dark lamps. False: lamps always bright in the screen's colour.</summary>
    public bool FollowBrightness { get; set; } = true;
    /// <summary>Mirror left/right (lamps hung behind the screen, seen from the front).</summary>
    public bool Mirror { get; set; }

    // ---- edges (ambilight) ----
    /// <summary>Spread the lamps over left, top and right by themselves (by the screen's proportions).</summary>
    public bool EdgeAuto { get; set; } = true;
    /// <summary>Lamps per side when not automatic.</summary>
    public int EdgeLeft { get; set; } = 2;
    public int EdgeTop { get; set; } = 4;
    public int EdgeRight { get; set; } = 2;
    public int EdgeBottom { get; set; }
    /// <summary>How deep the edge strip reaches into the picture, in % of its height/width.</summary>
    public int EdgeDepth { get; set; } = 15;
    /// <summary>Ignore black bars (letterbox/pillarbox) so the lamps take the real picture's edge.</summary>
    public bool SkipBlackBars { get; set; } = true;
    /// <summary>Order of the lamps: false = clockwise from the bottom of the left side, true = anti-clockwise.</summary>
    public bool EdgeReverse { get; set; }
    /// <summary>Which lamp is the first one of the chain (rotates the order).</summary>
    public int EdgeStart { get; set; }

    // ---- markers ----
    /// <summary>Per lamp (in lamp order) the spot it takes its colour from: x, y pairs, 0..1 over the picture.
    /// Lamps without a spot get one on a row through the middle.</summary>
    public double[] Points { get; set; }
    /// <summary>Size of the measured square around each spot, in % of the picture's width.</summary>
    public int PointSize { get; set; } = 8;
}

/// <summary>A picture shrunk to a small grid of colours (top row first).</summary>
public sealed record ScreenGrid(Color[] Cells, int Width, int Height)
{
    public static readonly ScreenGrid Empty = new(Array.Empty<Color>(), 0, 0);
    public Color this[int x, int y] => Cells[y * Width + x];

    /// <summary>One colour per column (whole height): the "zones" layout.</summary>
    public Color[] Columns()
    {
        var cols = new Color[Width];
        for (int x = 0; x < Width; x++) cols[x] = ScreenFeed.Average(this, x, x + 1, 0, Height);
        return cols;
    }

    /// <summary>Columns only (no height): what older tests and simple callers provide.</summary>
    public static ScreenGrid FromColumns(Color[] cols) => new(cols, cols.Length, cols.Length == 0 ? 0 : 1);
}

/// <summary>Which side of the picture a lamp belongs to, and its place along that side (0..1, in walking order).</summary>
public enum EdgeSide { Left, Top, Right, Bottom }

/// <summary>
/// Latest screen colours, filled by the app's sampler (smoothed already), and the layouts that turn them into lamps.
/// </summary>
public static class ScreenFeed
{
    static volatile ScreenGrid grid = ScreenGrid.Empty;
    static long updated;

    /// <summary>Set when something needs the screen; the app samples only then.</summary>
    public static volatile bool Wanted;

    public static void Publish(ScreenGrid g)
    {
        grid = g;
        Interlocked.Exchange(ref updated, Environment.TickCount64);
    }
    public static void Publish(Color[] columns) => Publish(ScreenGrid.FromColumns(columns));

    public static ScreenGrid Grid => grid;
    /// <summary>One colour per column (for previews).</summary>
    public static Color[] Zones => grid.Columns();
    public static bool Fresh => Environment.TickCount64 - Interlocked.Read(ref updated) < 2000;
    public static void Clear() => grid = ScreenGrid.Empty;

    public static LampOutput[] Render(ScreenSyncSettings set, bool useWhite, int lamps, Color[] cols) =>
        Render(set, useWhite, lamps, cols == null ? null : ScreenGrid.FromColumns(cols));

    /// <summary>Lamp colours for <paramref name="lamps"/> lamps.</summary>
    public static LampOutput[] Render(ScreenSyncSettings set, bool useWhite, int lamps, ScreenGrid g = null)
    {
        Wanted = true;
        int n = Math.Max(1, Math.Min(lamps, EffectLibrary.MaxHeads));
        var result = new LampOutput[n];
        g ??= Grid;
        if (g.Cells.Length == 0 || g.Width == 0 || g.Height == 0) { Array.Fill(result, new LampOutput(Light.White, 0)); return result; }
        set ??= new ScreenSyncSettings();

        Color[] colors = set.Mode switch
        {
            ScreenSyncMode.Average => Enumerable.Repeat(Average(g, 0, g.Width, 0, g.Height), n).ToArray(),
            ScreenSyncMode.Edges => EdgeColors(g, set, n),
            ScreenSyncMode.Points => PointColors(g, set, n),
            _ => ZoneColors(g, set, n),
        };
        for (int i = 0; i < n; i++)
        {
            (Color col, double level) = Shape(colors[i], set);
            result[i] = new LampOutput(Light.FromColor(col, useWhite), level);
        }
        return result;
    }

    static Color[] ZoneColors(ScreenGrid g, ScreenSyncSettings set, int n)
    {
        var (x0, x1, y0, y1) = set.SkipBlackBars ? Content(g) : (0, g.Width, 0, g.Height);
        int w = x1 - x0;
        var c = new Color[n];
        for (int i = 0; i < n; i++)
        {
            // lamp i covers columns [a, b): averaged, so few lamps still see the whole picture
            int lamp = set.Mirror ? n - 1 - i : i;
            int a = x0 + lamp * w / n, b = Math.Max(a + 1, x0 + (lamp + 1) * w / n);
            c[i] = Average(g, a, b, y0, y1);
        }
        return c;
    }

    /// <summary>Where lamp <paramref name="i"/> of <paramref name="n"/> measures: its marker, or a spot on a row through the middle.</summary>
    public static (double X, double Y) PointOf(ScreenSyncSettings set, int i, int n)
    {
        double[] p = set.Points;
        if (p != null && 2 * i + 1 < p.Length && double.IsFinite(p[2 * i]) && double.IsFinite(p[2 * i + 1]))
            return (Math.Clamp(p[2 * i], 0, 1), Math.Clamp(p[2 * i + 1], 0, 1));
        return ((i + 0.5) / Math.Max(1, n), 0.5);
    }

    /// <summary>The measured square around a marker, in grid cells (at least one cell; square on a 16:9 picture).</summary>
    public static (int X0, int X1, int Y0, int Y1) PointArea(ScreenGrid g, ScreenSyncSettings set, double x, double y)
    {
        double hx = Math.Clamp(set.PointSize, 1, 50) / 200.0;   // half the width, as a fraction of the picture
        double hy = hx * 16.0 / 9.0;
        int x0 = (int)Math.Floor((x - hx) * g.Width), x1 = (int)Math.Ceiling((x + hx) * g.Width);
        int y0 = (int)Math.Floor((y - hy) * g.Height), y1 = (int)Math.Ceiling((y + hy) * g.Height);
        x0 = Math.Clamp(x0, 0, g.Width - 1); x1 = Math.Clamp(x1, x0 + 1, g.Width);
        y0 = Math.Clamp(y0, 0, g.Height - 1); y1 = Math.Clamp(y1, y0 + 1, g.Height);
        return (x0, x1, y0, y1);
    }

    static Color[] PointColors(ScreenGrid g, ScreenSyncSettings set, int n)
    {
        var c = new Color[n];
        for (int i = 0; i < n; i++)
        {
            var (x, y) = PointOf(set, i, n);
            var (x0, x1, y0, y1) = PointArea(g, set, x, y);
            c[i] = Average(g, x0, x1, y0, y1);
        }
        return c;
    }

    /// <summary>Lamps per side: the user's numbers, or spread by themselves over left, top and right.</summary>
    public static (int Left, int Top, int Right, int Bottom) EdgeCounts(ScreenSyncSettings set, int lamps)
    {
        if (!set.EdgeAuto)
            return (Math.Max(0, set.EdgeLeft), Math.Max(0, set.EdgeTop), Math.Max(0, set.EdgeRight), Math.Max(0, set.EdgeBottom));
        // a 16:9 picture without lamps below: sides get 9 parts each, the top 16
        int side = (int)Math.Round(lamps * 9.0 / 34);
        if (lamps >= 3) side = Math.Max(1, side); else side = 0;
        return (side, Math.Max(0, lamps - 2 * side), side, 0);
    }

    /// <summary>
    /// Where each lamp sits around the picture, in chain order: clockwise from the bottom of the left side
    /// (left upwards, top to the right, right downwards, bottom to the left), or the reverse; then rotated by
    /// <see cref="ScreenSyncSettings.EdgeStart"/>. Lamps beyond the layout repeat it.
    /// </summary>
    public static List<(EdgeSide Side, int Index, int Count)> EdgeLayout(ScreenSyncSettings set, int lamps)
    {
        var (l, t, r, b) = EdgeCounts(set, lamps);
        var chain = new List<(EdgeSide, int, int)>();
        for (int i = 0; i < l; i++) chain.Add((EdgeSide.Left, i, l));     // index 0 = bottom of the left side
        for (int i = 0; i < t; i++) chain.Add((EdgeSide.Top, i, t));      // 0 = left end
        for (int i = 0; i < r; i++) chain.Add((EdgeSide.Right, i, r));    // 0 = top
        for (int i = 0; i < b; i++) chain.Add((EdgeSide.Bottom, i, b));   // 0 = right end
        if (set.EdgeReverse) chain.Reverse();
        if (chain.Count == 0) return chain;
        int start = ((set.EdgeStart % chain.Count) + chain.Count) % chain.Count;
        return chain.Skip(start).Concat(chain.Take(start)).ToList();
    }

    static Color[] EdgeColors(ScreenGrid g, ScreenSyncSettings set, int n)
    {
        var (x0, x1, y0, y1) = set.SkipBlackBars ? Content(g) : (0, g.Width, 0, g.Height);
        int w = x1 - x0, h = y1 - y0;
        double depth = Math.Clamp(set.EdgeDepth, 2, 50) / 100.0;
        int dx = Math.Max(1, (int)Math.Round(w * depth)), dy = Math.Max(1, (int)Math.Round(h * depth));
        var layout = EdgeLayout(set, n);
        var c = new Color[n];
        for (int i = 0; i < n; i++)
        {
            if (layout.Count == 0) { c[i] = Average(g, x0, x1, y0, y1); continue; }
            var (side, idx, count) = layout[i % layout.Count];
            // segment along the side, in walking order
            int Seg(int length, int k) => k * length / count;
            switch (side)
            {
                case EdgeSide.Left:   // walking upwards: segment 0 at the bottom
                {
                    int a = y1 - Seg(h, idx + 1), bnd = y1 - Seg(h, idx);
                    c[i] = Average(g, x0, x0 + dx, a, Math.Max(a + 1, bnd));
                    break;
                }
                case EdgeSide.Top:    // left to right
                {
                    int a = x0 + Seg(w, idx), bnd = x0 + Seg(w, idx + 1);
                    c[i] = Average(g, a, Math.Max(a + 1, bnd), y0, y0 + dy);
                    break;
                }
                case EdgeSide.Right:  // downwards
                {
                    int a = y0 + Seg(h, idx), bnd = y0 + Seg(h, idx + 1);
                    c[i] = Average(g, x1 - dx, x1, a, Math.Max(a + 1, bnd));
                    break;
                }
                default:              // bottom, right to left
                {
                    int a = x1 - Seg(w, idx + 1), bnd = x1 - Seg(w, idx);
                    c[i] = Average(g, a, Math.Max(a + 1, bnd), y1 - dy, y1);
                    break;
                }
            }
        }
        return c;
    }

    /// <summary>
    /// The part of the picture that is not black bars: rows/columns at the edges that are (almost) black are
    /// dropped, but at least half the picture always stays (a dark scene is not a bar).
    /// </summary>
    public static (int X0, int X1, int Y0, int Y1) Content(ScreenGrid g)
    {
        bool DarkRow(int y) { for (int x = 0; x < g.Width; x++) if (Max(g[x, y]) > 18) return false; return true; }
        bool DarkCol(int x) { for (int y = 0; y < g.Height; y++) if (Max(g[x, y]) > 18) return false; return true; }
        int y0 = 0, y1 = g.Height, x0 = 0, x1 = g.Width;
        while (y1 - y0 > g.Height / 2 + 1 && DarkRow(y0)) y0++;
        while (y1 - y0 > g.Height / 2 + 1 && DarkRow(y1 - 1)) y1--;
        while (x1 - x0 > g.Width / 2 + 1 && DarkCol(x0)) x0++;
        while (x1 - x0 > g.Width / 2 + 1 && DarkCol(x1 - 1)) x1--;
        // all black: keep the whole picture
        if (y1 - y0 <= g.Height / 2 + 1 && Enumerable.Range(y0, y1 - y0).All(DarkRow)) return (0, g.Width, 0, g.Height);
        return (x0, x1, y0, y1);
    }

    static int Max(Color c) => Math.Max(c.R, Math.Max(c.G, c.B));

    public static Color Average(ScreenGrid g, int x0, int x1, int y0, int y1)
    {
        x0 = Math.Clamp(x0, 0, g.Width); x1 = Math.Clamp(x1, x0, g.Width);
        y0 = Math.Clamp(y0, 0, g.Height); y1 = Math.Clamp(y1, y0, g.Height);
        long r = 0, gg = 0, b = 0; int n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++) { Color c = g[x, y]; r += c.R; gg += c.G; b += c.B; n++; }
        return n == 0 ? Color.Black : Color.FromArgb((int)(r / n), (int)(gg / n), (int)(b / n));
    }

    /// <summary>Saturation boost, then either brightness from the screen or full brightness in that hue.</summary>
    public static (Color Color, double Level) Shape(Color c, ScreenSyncSettings set)
    {
        double r = c.R, g = c.G, b = c.B;
        double max = Math.Max(r, Math.Max(g, b));
        if (max < 1) return (Color.Black, 0);
        // stretch the distance from grey: saturation x factor
        double f = Math.Clamp(set.Saturation, 0, 400) / 100.0, grey = (r + g + b) / 3;
        r = grey + (r - grey) * f; g = grey + (g - grey) * f; b = grey + (b - grey) * f;
        double lo = Math.Min(r, Math.Min(g, b));
        if (lo < 0) { r -= lo; g -= lo; b -= lo; }
        double hi = Math.Max(r, Math.Max(g, b));
        // full-colour version (max channel 255), brightness carried by the level
        Color full = Color.FromArgb((int)Math.Round(r / hi * 255), (int)Math.Round(g / hi * 255), (int)Math.Round(b / hi * 255));
        if (!set.FollowBrightness) return max < 12 ? (Color.Black, 0) : (full, 1.0);   // near-black: off instead of a random hue
        return (full, Math.Clamp(max / 255.0, 0, 1));
    }
}

/// <summary>Turns a camera picture into a grid of colours, using the framed TV area (four corners).</summary>
public static class CameraFrame
{
    /// <summary>
    /// The framed area as a <paramref name="cols"/> x <paramref name="rows"/> grid (top row first).
    /// <paramref name="pixels"/> is 24- or 32-bit BGR, rows of <paramref name="stride"/> bytes, top row first.
    /// The frame is mapped with a true perspective (homography), like a projector's keystone correction, so a TV
    /// filmed at an angle is sampled evenly; each cell averages a few samples.
    /// </summary>
    public static ScreenGrid Grid(byte[] pixels, int width, int height, int stride, int bytesPerPixel, double[] corners, int cols = 32, int rows = 18)
    {
        double[] q = corners is { Length: 8 } ? corners : ScreenSyncSettings.DefaultCorners();
        Func<double, double, (double X, double Y)> map = Perspective(q);
        var cells = new Color[cols * rows];
        const int sub = 2;   // samples per cell in each direction
        for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
            {
                long r = 0, g = 0, b = 0; int n = 0;
                for (int sy = 0; sy < sub; sy++)
                    for (int sx = 0; sx < sub; sx++)
                    {
                        double u = (cx + (sx + 0.5) / sub) / cols, v = (cy + (sy + 0.5) / sub) / rows;
                        var (x, y) = map(u, v);
                        int px = Math.Clamp((int)(x * width), 0, width - 1), py = Math.Clamp((int)(y * height), 0, height - 1);
                        int i = py * stride + px * bytesPerPixel;
                        if (i + 2 >= pixels.Length) continue;
                        b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2]; n++;
                    }
                cells[cy * cols + cx] = n == 0 ? Color.Black : Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
            }
        return new ScreenGrid(cells, cols, rows);
    }

    /// <summary>
    /// Perspective mapping from the unit square (u right, v down) to the quad TL, TR, BR, BL (Heckbert's
    /// square-to-quad). Straight lines stay straight and equal steps on the TV become equal steps in u/v.
    /// </summary>
    public static Func<double, double, (double X, double Y)> Perspective(double[] q)
    {
        double x0 = q[0], y0 = q[1], x1 = q[2], y1 = q[3], x2 = q[4], y2 = q[5], x3 = q[6], y3 = q[7];
        double sx = x0 - x1 + x2 - x3, sy = y0 - y1 + y2 - y3;
        double a, b, c = x0, d, e, f = y0, g = 0, h = 0;
        double dx1 = x1 - x2, dx2 = x3 - x2, dy1 = y1 - y2, dy2 = y3 - y2;
        double den = dx1 * dy2 - dx2 * dy1;
        if ((Math.Abs(sx) < 1e-9 && Math.Abs(sy) < 1e-9) || Math.Abs(den) < 1e-12)
        {
            // parallelogram (or degenerate): affine
            a = x1 - x0; b = x3 - x0; d = y1 - y0; e = y3 - y0;
        }
        else
        {
            g = (sx * dy2 - dx2 * sy) / den;
            h = (dx1 * sy - sx * dy1) / den;
            a = x1 - x0 + g * x1; b = x3 - x0 + h * x3;
            d = y1 - y0 + g * y1; e = y3 - y0 + h * y3;
        }
        return (u, v) =>
        {
            double w = g * u + h * v + 1;
            if (Math.Abs(w) < 1e-9) w = 1e-9;
            return ((a * u + b * v + c) / w, (d * u + e * v + f) / w);
        };
    }

    /// <summary>
    /// Finds a TV (or any bright screen) in a camera picture: the largest bright area, its four outer corners.
    /// Works best with something bright on the TV (a white or colourful picture). Returns corners 0..1
    /// (TL, TR, BR, BL) or null when nothing screen-like is found.
    /// </summary>
    public static double[] DetectScreen(byte[] pixels, int width, int height, int stride, int bytesPerPixel)
    {
        // work on a small luminance picture (max 160 wide): fast and noise-tolerant
        int step = Math.Max(1, (int)Math.Ceiling(width / 160.0));
        int w = width / step, h = height / step;
        if (w < 8 || h < 8) return null;
        var lum = new byte[w * h];
        var hist = new int[256];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * step) * stride + (x * step) * bytesPerPixel;
                if (i + 2 >= pixels.Length) continue;
                int l = (pixels[i] * 29 + pixels[i + 1] * 150 + pixels[i + 2] * 77) >> 8;
                lum[y * w + x] = (byte)l;
                hist[l]++;
            }
        // Otsu threshold between dark room and bright screen
        long total = w * h, sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += (long)i * hist[i];
        long wB = 0, sumB = 0; double best = -1; int thr = 128;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t]; if (wB == 0) continue;
            long wF = total - wB; if (wF == 0) break;
            sumB += (long)t * hist[t];
            double mB = sumB / (double)wB, mF = (sumAll - sumB) / (double)wF;
            double between = wB * (double)wF * (mB - mF) * (mB - mF);
            if (between > best) { best = between; thr = t; }
        }
        // largest 4-connected bright area
        var label = new int[w * h];
        int bestLabel = 0, bestSize = 0, next = 0;
        var stack = new Stack<int>();
        for (int s = 0; s < w * h; s++)
        {
            if (lum[s] <= thr || label[s] != 0) continue;
            next++; int size = 0;
            label[s] = next; stack.Push(s);
            while (stack.Count > 0)
            {
                int p = stack.Pop(); size++;
                int px = p % w, py = p / w;
                void Try(int q2) { if (lum[q2] > thr && label[q2] == 0) { label[q2] = next; stack.Push(q2); } }
                if (px > 0) Try(p - 1);
                if (px < w - 1) Try(p + 1);
                if (py > 0) Try(p - w);
                if (py < h - 1) Try(p + w);
            }
            if (size > bestSize) { bestSize = size; bestLabel = next; }
        }
        if (bestSize < w * h * 0.03 || bestSize > w * h * 0.97) return null;   // nothing, or everything bright
        // outer corners: extremes of x+y and x-y
        int tl = -1, br = -1, tr = -1, bl = -1;
        int sMin = int.MaxValue, sMax = int.MinValue, dMin = int.MaxValue, dMax = int.MinValue;
        for (int p = 0; p < w * h; p++)
        {
            if (label[p] != bestLabel) continue;
            int x = p % w, y = p / w, sum = x + y, diff = x - y;
            if (sum < sMin) { sMin = sum; tl = p; }
            if (sum > sMax) { sMax = sum; br = p; }
            if (diff > dMax) { dMax = diff; tr = p; }
            if (diff < dMin) { dMin = diff; bl = p; }
        }
        double X(int p) => Math.Clamp((p % w + 0.5) / w, 0, 1);
        double Y(int p) => Math.Clamp((p / w + 0.5) / h, 0, 1);
        double[] c = { X(tl), Y(tl), X(tr), Y(tr), X(br), Y(br), X(bl), Y(bl) };
        // sanity: the quad must cover most of the bright area's bounding box (a blob is not a screen)
        double area = Math.Abs((c[0] * c[3] - c[2] * c[1]) + (c[2] * c[5] - c[4] * c[3]) + (c[4] * c[7] - c[6] * c[5]) + (c[6] * c[1] - c[0] * c[7])) / 2;
        if (area * w * h < bestSize * 0.6) return null;
        return c;
    }

    /// <summary>One colour per column of the framed area (left to right).</summary>
    public static Color[] Columns(byte[] pixels, int width, int height, int stride, int bytesPerPixel, double[] corners, int columns = 32, int rows = 12) =>
        Grid(pixels, width, height, stride, bytesPerPixel, corners, columns, rows).Columns();
}
