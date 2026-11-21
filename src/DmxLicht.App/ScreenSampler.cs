using System.Drawing;
using System.Runtime.InteropServices;
using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;

namespace DmxLicht.App;

/// <summary>
/// Samples the screen ~25x per second while the "follow the screen" effect runs: Windows shrinks the
/// chosen monitor to 32 x 18 pixels (cheap, done by the graphics driver), every cell is smoothed,
/// and the result goes to <see cref="ScreenFeed"/>. Idle (no capture at all) when nothing needs it.
/// Note: video from streaming services with copy protection can come out black.
/// </summary>
sealed class ScreenSampler : IDisposable
{
    const int W = 32, H = 18;
    readonly LightEngine engine;
    readonly Thread thread;
    volatile bool run = true;
    double[] smooth = new double[W * H * 3];
    string lastError;
    readonly CameraCapture camera = new();
    long idleSince = Environment.TickCount64;
    static long previewUntil;

    /// <summary>The UI is framing the TV: keep the camera on for a few seconds even without the effect.</summary>
    public static void WantPreview() => Interlocked.Exchange(ref previewUntil, Environment.TickCount64 + 4000);

    public CameraCapture Camera => camera;

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr prev);
    [DllImport("gdi32.dll", SetLastError = true)] static extern bool StretchBlt(IntPtr dst, int dx, int dy, int dw, int dh, IntPtr src, int sx, int sy, int sw, int sh, int rop);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFO info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFO
    {
        public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        public int bmiColors;
    }

    const int HALFTONE = 4, SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;

    public ScreenSampler(LightEngine engine)
    {
        this.engine = engine;
        thread = new Thread(Loop) { IsBackground = true, Name = "Screen sync", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    /// <summary>The monitors, for the settings ("1: 1920 x 1080 (hoofdscherm)").</summary>
    public static List<string> Monitors() =>
        Screen.AllScreens.Select((s, i) => $"{i + 1}: {s.Bounds.Width} x {s.Bounds.Height}" + (s.Primary ? " *" : "")).ToList();

    void Loop()
    {
        byte[] bits = new byte[W * H * 4];
        while (run)
        {
            bool want = ScreenFeed.Wanted;
            ScreenFeed.Wanted = false;
            bool preview = Environment.TickCount64 < Interlocked.Read(ref previewUntil);
            ScreenSyncSettings set = engine.Read(s => s.State.ScreenSync) ?? new ScreenSyncSettings();
            bool cam = set.Source == ScreenSource.Camera;
            if ((want && cam) || preview) idleSince = Environment.TickCount64;   // only camera use keeps the camera on
            // camera off (light off) when nobody needs it for a few seconds
            if (camera.Running && Environment.TickCount64 - idleSince > 3000) camera.Stop();
            if (!want && !preview) { Thread.Sleep(250); continue; }
            try
            {
                if (cam || preview) camera.Ensure(set.Camera);
                if (want && cam)
                {
                    var f = camera.Decode();
                    if (f is { } frame)
                        Publish(CameraFrame.Grid(frame.Pixels, frame.Width, frame.Height, frame.Stride, 3, set.Corners, W, H), set.Smoothing);
                }
                else if (want)
                {
                    Screen[] screens = Screen.AllScreens;
                    Rectangle r = (set.Monitor >= 0 && set.Monitor < screens.Length ? screens[set.Monitor] : Screen.PrimaryScreen!).Bounds;
                    Grab(r, bits, W, H);
                    Publish(ToGrid(bits), set.Smoothing);
                }
                if (lastError != null) { lastError = null; Log.Write("Scherm volgen werkt weer"); }
            }
            catch (Exception ex)
            {
                if (ex.Message != lastError) { lastError = ex.Message; Log.Write("Scherm volgen: " + ex.Message); }
            }
            Thread.Sleep(40);
        }
    }

    static void Grab(Rectangle r, byte[] bits, int W, int H)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(screen);
        IntPtr bmp = CreateCompatibleBitmap(screen, W, H);
        IntPtr old = SelectObject(mem, bmp);
        try
        {
            SetStretchBltMode(mem, HALFTONE);   // averages pixels instead of picking a few
            SetBrushOrgEx(mem, 0, 0, IntPtr.Zero);
            if (!StretchBlt(mem, 0, 0, W, H, screen, r.X, r.Y, r.Width, r.Height, SRCCOPY | CAPTUREBLT))
                throw new InvalidOperationException("scherm kopiëren mislukt (code " + Marshal.GetLastWin32Error() + ")");
            var info = new BITMAPINFO { biSize = 40, biWidth = W, biHeight = -H, biPlanes = 1, biBitCount = 32 };
            SelectObject(mem, old);
            if (GetDIBits(mem, bmp, 0, (uint)H, bits, ref info, 0) == 0) throw new InvalidOperationException("schermpixels lezen mislukt");
        }
        finally
        {
            SelectObject(mem, old);   // always: a bitmap still selected cannot be deleted (GDI leak)
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>
    /// A small picture (JPEG) of what screen sync sees: the chosen monitor, or the camera's framed TV straightened.
    /// For placing the lamp markers. Null when there is no camera picture yet.
    /// </summary>
    public byte[] Snapshot(ScreenSyncSettings set, int w = 320, int h = 180)
    {
        ScreenGrid g;
        if (set.Source == ScreenSource.Camera)
        {
            if (camera.Decode() is not { } f) return null;
            g = CameraFrame.Grid(f.Pixels, f.Width, f.Height, f.Stride, 3, set.Corners, w, h);
        }
        else
        {
            Screen[] screens = Screen.AllScreens;
            Rectangle r = (set.Monitor >= 0 && set.Monitor < screens.Length ? screens[set.Monitor] : Screen.PrimaryScreen!).Bounds;
            var bits = new byte[w * h * 4];
            Grab(r, bits, w, h);
            var cells = new Color[w * h];
            for (int k = 0; k < w * h; k++) cells[k] = Color.FromArgb(bits[k * 4 + 2], bits[k * 4 + 1], bits[k * 4]);
            g = new ScreenGrid(cells, w, h);
        }
        using var bmp = new Bitmap(g.Width, g.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var data = bmp.LockBits(new Rectangle(0, 0, g.Width, g.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            var row = new byte[data.Stride];
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++) { Color c = g[x, y]; row[x * 3] = c.B; row[x * 3 + 1] = c.G; row[x * 3 + 2] = c.R; }
                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
            }
        }
        finally { bmp.UnlockBits(data); }
        using var ms = new MemoryStream();
        var codec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
        using var p = new System.Drawing.Imaging.EncoderParameters(1);
        p.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
        bmp.Save(ms, codec, p);
        return ms.ToArray();
    }

    /// <summary>The shrunk screen (BGRA) as a grid of colours.</summary>
    static ScreenGrid ToGrid(byte[] bits)
    {
        var cells = new Color[W * H];
        for (int k = 0; k < W * H; k++) cells[k] = Color.FromArgb(bits[k * 4 + 2], bits[k * 4 + 1], bits[k * 4]);
        return new ScreenGrid(cells, W, H);
    }

    /// <summary>Exponential smoothing per cell, then to <see cref="ScreenFeed"/>.</summary>
    void Publish(ScreenGrid raw, int smoothing)
    {
        double keep = Math.Clamp(smoothing, 0, 100) / 100.0 * 0.92;
        int n = raw.Cells.Length;
        if (smooth.Length != n * 3) smooth = new double[n * 3];
        var cells = new Color[n];
        for (int i = 0; i < n; i++)
        {
            Color c = raw.Cells[i];
            int k = i * 3;
            smooth[k] = smooth[k] * keep + c.R * (1 - keep);
            smooth[k + 1] = smooth[k + 1] * keep + c.G * (1 - keep);
            smooth[k + 2] = smooth[k + 2] * keep + c.B * (1 - keep);
            cells[i] = Color.FromArgb((int)smooth[k], (int)smooth[k + 1], (int)smooth[k + 2]);
        }
        ScreenFeed.Publish(new ScreenGrid(cells, raw.Width, raw.Height));
    }

    public void Dispose() { run = false; thread.Join(500); camera.Dispose(); }
}
