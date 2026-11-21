using System.Drawing;
using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class ScreenSyncTests
{
    static readonly Color[] LeftRedRightBlue = Enumerable.Range(0, 32).Select(i => i < 16 ? Color.FromArgb(200, 0, 0) : Color.FromArgb(0, 0, 100)).ToArray();

    [Fact]
    public void ZonesGoLeftToRightAndMirror()
    {
        var set = new ScreenSyncSettings { Saturation = 100 };
        LampOutput[] o = ScreenFeed.Render(set, false, 4, LeftRedRightBlue);
        Assert.Equal((255, 0, 0), (o[0].Color.R, o[0].Color.G, o[0].Color.B));     // full colour...
        Assert.Equal(200 / 255.0, o[0].Level, 3);                                     // ...brightness from the screen
        Assert.Equal((0, 0, 255), (o[3].Color.R, o[3].Color.G, o[3].Color.B));
        set.Mirror = true;
        Assert.Equal(255, ScreenFeed.Render(set, false, 4, LeftRedRightBlue)[3].Color.R);
        // more lamps than columns, fewer lamps than columns: still the whole screen
        Assert.Equal(40, ScreenFeed.Render(set, false, 40, LeftRedRightBlue).Length);
        LampOutput one = ScreenFeed.Render(new ScreenSyncSettings { Saturation = 100 }, false, 1, LeftRedRightBlue)[0];
        Assert.Equal((100, 0, 50), (one.Color.R * 100 / 255, one.Color.G, one.Color.B * 100 / 255));   // the average of the two halves
    }

    [Fact]
    public void AverageSaturationAndBrightnessOptions()
    {
        var set = new ScreenSyncSettings { Mode = ScreenSyncMode.Average, Saturation = 100, FollowBrightness = false };
        LampOutput[] o = ScreenFeed.Render(set, false, 3, LeftRedRightBlue);
        Assert.All(o, x => Assert.Equal(1.0, x.Level));                              // always bright
        Assert.All(o, x => Assert.Equal(o[0].Color, x.Color));
        // greyish orange gets more colourful with a boost
        (Color plain, _) = ScreenFeed.Shape(Color.FromArgb(180, 140, 120), new ScreenSyncSettings { Saturation = 100 });
        (Color boosted, _) = ScreenFeed.Shape(Color.FromArgb(180, 140, 120), new ScreenSyncSettings { Saturation = 250 });
        Assert.True(boosted.B < plain.B);
        // a black screen: lamps off, also without brightness following
        Assert.Equal(0, ScreenFeed.Shape(Color.FromArgb(4, 4, 4), set).Level);
        Assert.All(ScreenFeed.Render(set, false, 2, Array.Empty<Color>()), x => Assert.Equal(0, x.Level));
    }

    [Fact]
    public void RunsAsEffectAndByCommand()
    {
        var show = new ShowFile { Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } } };
        using var e = new LightEngine(show, new CaptureOutput(), null);
        Assert.Equal("ok", Commands.Run(e, "effect screen"));
        Assert.True(e.Read(s => s.State.Effect.ScreenSync && s.State.Mode == OutputMode.Effect));
        Assert.Null(Commands.Validate(e, "effect scherm"));
        Assert.Equal("ok", Commands.Run(e, "effect fire"));
        Assert.False(e.Read(s => s.State.Effect.ScreenSync));

        Rig rig = Rig.Build(show.Patch, new FixtureLibrary(FixtureLibrary.BuiltIn()));
        var st = new ShowState { Mode = OutputMode.Effect };
        st.Effect.ScreenSync = true;
        st.ScreenSync.Saturation = 100;
        ScreenFeed.Publish(Enumerable.Repeat(Color.FromArgb(0, 255, 0), 32).ToArray());
        Assert.Equal(new byte[] { 0, 255, 0 }, FrameBuilder.Build(rig, st, 0)[1..4]);
        Assert.True(ScreenFeed.Wanted);
        ScreenFeed.Clear();
    }

    /// <summary>A 100x60 "camera picture": grey room, with a TV (red left half, blue right half) at x 30..70, y 20..50.</summary>
    static byte[] Room(out int w, out int h)
    {
        w = 100; h = 60;
        var px = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                bool tv = x >= 30 && x < 70 && y >= 20 && y < 50;
                (byte r, byte g, byte b) = !tv ? ((byte)90, (byte)90, (byte)90) : x < 50 ? ((byte)220, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)220);
                px[i] = b; px[i + 1] = g; px[i + 2] = r;
            }
        return px;
    }

    [Fact]
    public void CameraFrameReadsOnlyTheFramedTv()
    {
        byte[] px = Room(out int w, out int h);
        double[] tv = { 0.30, 0.34, 0.70, 0.34, 0.70, 0.82, 0.30, 0.82 };
        Color[] cols = CameraFrame.Columns(px, w, h, w * 3, 3, tv, 8);
        Assert.All(cols.Take(4), c => Assert.Equal((220, 0, 0), (c.R, c.G, c.B)));    // left half of the TV: red
        Assert.All(cols.Skip(4), c => Assert.Equal((0, 0, 220), (c.R, c.G, c.B)));    // right half: blue, no grey room
        // the whole picture as frame: the grey room comes in at the edges
        Color[] whole = CameraFrame.Columns(px, w, h, w * 3, 3, new double[] { 0, 0, 1, 0, 1, 1, 0, 1 }, 8);
        Assert.Equal((90, 90, 90), (whole[0].R, whole[0].G, whole[0].B));
        // a TV filmed at an angle (frame is a trapezoid): still left red, right blue
        double[] skew = { 0.32, 0.36, 0.68, 0.40, 0.69, 0.78, 0.31, 0.82 };
        Color[] s = CameraFrame.Columns(px, w, h, w * 3, 3, skew, 4);
        Assert.True(s[0].R > 200 && s[0].B == 0);
        Assert.True(s[3].B > 200 && s[3].R == 0);
        // broken input does not crash
        Assert.Equal(4, CameraFrame.Columns(px, w, h, w * 3, 3, null, 4).Length);
        Assert.Equal(4, CameraFrame.Columns(new byte[10], w, h, w * 3, 3, tv, 4).Length);
    }

    /// <summary>32x18 picture: left edge red, right edge blue, top edge green, bottom edge yellow, middle grey; optional black bars.</summary>
    static ScreenGrid Tv(int bars = 0)
    {
        var cells = new Color[32 * 18];
        for (int y = 0; y < 18; y++)
            for (int x = 0; x < 32; x++)
            {
                int yy = y - bars, h = 18 - 2 * bars;
                if (yy < 0 || yy >= h) { cells[y * 32 + x] = Color.Black; continue; }
                // each spot gets the colour of its nearest edge (like a real picture fading to its sides)
                double l = (x + 0.5) / 32, r = 1 - l, t = (yy + 0.5) / h, b = 1 - t;
                double m = Math.Min(Math.Min(l, r), Math.Min(t, b));
                cells[y * 32 + x] = m == l ? Color.FromArgb(220, 0, 0) : m == r ? Color.FromArgb(0, 0, 220)
                    : m == t ? Color.FromArgb(0, 220, 0) : Color.FromArgb(220, 220, 0);
            }
        return new ScreenGrid(cells, 32, 18);
    }

    /// <summary>The dominant colour of a lamp (a corner lamp mixes a little of the neighbouring edge, as it should).</summary>
    static string Hue(LampOutput o)
    {
        if (o.Level == 0) return "off";
        int r = o.Color.R, g = o.Color.G, b = o.Color.B;
        if (r > 180 && g > 180 && b < 120) return "Y";
        return r >= g && r >= b ? "R" : g >= b ? "G" : "B";
    }

    [Fact]
    public void EdgesGoAroundTheTvClockwise()
    {
        var set = new ScreenSyncSettings { Mode = ScreenSyncMode.Edges, Saturation = 100, EdgeDepth = 10 };
        Assert.Equal((3, 4, 3, 0), ScreenFeed.EdgeCounts(set, 10));          // auto: left, top, right
        string Order(ScreenSyncSettings s, ScreenGrid g) => string.Concat(ScreenFeed.Render(s, false, 10, g).Select(Hue));
        Assert.Equal("RRRGGGGBBB", Order(set, Tv()));                          // up the left, along the top, down the right
        set.EdgeReverse = true;
        Assert.Equal("BBBGGGGRRR", Order(set, Tv()));
        set.EdgeReverse = false; set.EdgeStart = 3;                            // the chain starts at the top-left lamp
        Assert.Equal("GGGGBBBRRR", Order(set, Tv()));

        // own counts, with lamps along the bottom too
        set = new ScreenSyncSettings { Mode = ScreenSyncMode.Edges, Saturation = 100, EdgeDepth = 10, EdgeAuto = false, EdgeLeft = 2, EdgeTop = 3, EdgeRight = 2, EdgeBottom = 3 };
        Assert.Equal("RRGGGBBYYY", Order(set, Tv()));
        var layout = ScreenFeed.EdgeLayout(set, 10);
        Assert.Equal((EdgeSide.Left, 0, 2), layout[0]);
        Assert.Equal((EdgeSide.Bottom, 2, 3), layout[9]);
    }

    [Fact]
    public void BlackBarsAreSkipped()
    {
        var set = new ScreenSyncSettings { Mode = ScreenSyncMode.Edges, Saturation = 100, EdgeDepth = 10, EdgeAuto = false, EdgeLeft = 0, EdgeTop = 2, EdgeRight = 0, EdgeBottom = 2 };
        ScreenGrid film = Tv(bars: 3);                                          // letterbox: 3 black rows top and bottom
        Assert.Equal((0, 32, 3, 15), ScreenFeed.Content(film));
        Assert.Equal("GGYY", string.Concat(ScreenFeed.Render(set, false, 4, film).Select(Hue)));
        set.SkipBlackBars = false;
        Assert.All(ScreenFeed.Render(set, false, 4, film), o => Assert.Equal(0, o.Level));   // the bars themselves: dark
        // a dark scene is not a bar: an all-black picture keeps its full size
        var dark = new ScreenGrid(Enumerable.Repeat(Color.Black, 32 * 18).ToArray(), 32, 18);
        Assert.Equal((0, 32, 0, 18), ScreenFeed.Content(dark));
    }

    [Fact]
    public void CameraGridFeedsTheSameLayouts()
    {
        byte[] px = Room(out int w, out int h);
        ScreenGrid g = CameraFrame.Grid(px, w, h, w * 3, 3, new[] { 0.30, 0.34, 0.70, 0.34, 0.70, 0.82, 0.30, 0.82 }, 16, 9);
        var set = new ScreenSyncSettings { Mode = ScreenSyncMode.Edges, Saturation = 100, EdgeAuto = false, EdgeLeft = 1, EdgeTop = 0, EdgeRight = 1, EdgeBottom = 0 };
        Assert.Equal("RB", string.Concat(ScreenFeed.Render(set, false, 2, g).Select(Hue)));
    }

    [Fact]
    public void PerspectiveMapsCornersAndKeepsLinesStraight()
    {
        double[] q = { 0.20, 0.30, 0.80, 0.20, 0.85, 0.90, 0.15, 0.75 };   // keystoned TV
        var map = CameraFrame.Perspective(q);
        void Near((double X, double Y) p, double x, double y) { Assert.InRange(p.X, x - 1e-9, x + 1e-9); Assert.InRange(p.Y, y - 1e-9, y + 1e-9); }
        Near(map(0, 0), 0.20, 0.30); Near(map(1, 0), 0.80, 0.20); Near(map(1, 1), 0.85, 0.90); Near(map(0, 1), 0.15, 0.75);
        // the middle of the TV is where the diagonals cross (true perspective, not the average of the corners)
        (double X, double Y) c = map(0.5, 0.5);
        double Cross(double ax, double ay, double bx, double by, double px, double py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        Assert.InRange(Cross(q[0], q[1], q[4], q[5], c.X, c.Y), -1e-9, 1e-9);   // on diagonal TL-BR
        Assert.InRange(Cross(q[2], q[3], q[6], q[7], c.X, c.Y), -1e-9, 1e-9);   // on diagonal TR-BL
        // a rectangle stays a plain linear mapping
        var rect = CameraFrame.Perspective(new[] { 0.1, 0.2, 0.9, 0.2, 0.9, 0.8, 0.1, 0.8 });
        Near(rect(0.25, 0.5), 0.3, 0.5);
    }

    /// <summary>A dark 320x180 room with a bright, keystoned TV.</summary>
    static byte[] RoomWithTv(double[] q, out int w, out int h)
    {
        w = 320; h = 180;
        var px = new byte[w * h * 3];
        bool Inside(double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = 3; i < 4; j = i++)
            {
                double xi = q[i * 2], yi = q[i * 2 + 1], xj = q[j * 2], yj = q[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
        var r = new Random(5);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                bool tv = Inside((x + 0.5) / w, (y + 0.5) / h);
                byte v = tv ? (byte)(190 + r.Next(60)) : (byte)(20 + r.Next(40));   // bright picture, dim noisy room
                px[i] = v; px[i + 1] = (byte)(tv ? 200 : v); px[i + 2] = v;
            }
        // a small bright lamp in the room must not win
        for (int y = 10; y < 18; y++) for (int x = 290; x < 300; x++) { int i = (y * w + x) * 3; px[i] = px[i + 1] = px[i + 2] = 255; }
        return px;
    }

    [Fact]
    public void FindsTheTvInTheCameraPicture()
    {
        double[] tv = { 0.25, 0.30, 0.72, 0.22, 0.78, 0.80, 0.20, 0.72 };
        byte[] px = RoomWithTv(tv, out int w, out int h);
        double[] found = CameraFrame.DetectScreen(px, w, h, w * 3, 3);
        Assert.NotNull(found);
        for (int i = 0; i < 8; i++) Assert.InRange(found[i], tv[i] - 0.03, tv[i] + 0.03);
        // nothing bright: no guess
        var dark = new byte[w * h * 3];
        Assert.Null(CameraFrame.DetectScreen(dark, w, h, w * 3, 3));
    }

    [Fact]
    public void CommandsAreValidatedForTheSchedule()
    {
        var show = new ShowFile();
        show.Scenes.Add(new Scene { Id = "s", Name = "Sfeer" });
        using var e = new LightEngine(show, new CaptureOutput(), null);
        Assert.Null(Commands.Validate(e, "scene Sfeer"));
        Assert.StartsWith("scene not found", Commands.Validate(e, "scene Feest"));
        Assert.StartsWith("unknown command", Commands.Validate(e, "dance"));
        Assert.Null(Commands.Validate(e, "off"));
        Assert.StartsWith("unknown colour", Commands.Validate(e, "color plaid"));
        Assert.StartsWith("timeline not found", Commands.Validate(e, "timeline play Show"));
    }
    [Fact]
    public void MarkersTakeTheColourOfTheirOwnSpot()
    {
        ScreenGrid tv = Tv();
        // lamp 0 on the left edge (red), lamp 1 at the top (green), lamp 2 on the right (blue), lamp 3 at the bottom (yellow)
        var set = new ScreenSyncSettings
        {
            Mode = ScreenSyncMode.Points, FollowBrightness = false, Saturation = 100, PointSize = 4,
            Points = new[] { 0.02, 0.5, 0.5, 0.04, 0.98, 0.5, 0.5, 0.96 },
        };
        LampOutput[] o = ScreenFeed.Render(set, false, 4, tv);
        Assert.Equal("R", Hue(o[0]));
        Assert.Equal("G", Hue(o[1]));
        Assert.Equal("B", Hue(o[2]));
        Assert.Equal("Y", Hue(o[3]));

        // moving a marker moves where the lamp looks
        set.Points = new[] { 0.98, 0.5 };
        Assert.Equal("B", Hue(ScreenFeed.Render(set, false, 1, tv)[0]));
    }

    [Fact]
    public void LampsWithoutMarkerGetASpotOnARow()
    {
        var set = new ScreenSyncSettings { Mode = ScreenSyncMode.Points, Points = new[] { 0.1, 0.2 } };
        Assert.Equal((0.1, 0.2), ScreenFeed.PointOf(set, 0, 3));
        Assert.Equal((0.5, 0.5), ScreenFeed.PointOf(set, 1, 3));        // no marker: middle of its share of the row
        Assert.Equal((2.5 / 3, 0.5), ScreenFeed.PointOf(set, 2, 3));
        // broken values never crash and stay on the picture
        set.Points = new[] { double.NaN, 7.0, -3, 0.5 };
        Assert.Equal((0.5 / 2, 0.5), ScreenFeed.PointOf(set, 0, 2));
        Assert.Equal((0.0, 0.5), ScreenFeed.PointOf(set, 1, 2));
        var (x0, x1, y0, y1) = ScreenFeed.PointArea(Tv(), new ScreenSyncSettings { PointSize = 1 }, 1.0, 1.0);
        Assert.True(x1 - x0 >= 1 && y1 - y0 >= 1 && x1 <= 32 && y1 <= 18);
        Assert.Equal(5, ScreenFeed.Render(set, true, 5, Tv()).Length);
    }
}
