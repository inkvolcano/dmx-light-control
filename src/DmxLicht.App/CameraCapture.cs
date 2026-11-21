using System.Drawing;
using System.Drawing.Imaging;
using DmxLicht.Core;
using FlashCap;

namespace DmxLicht.App;

/// <summary>
/// One webcam, opened only while needed (the camera light is off otherwise). Picks a small format
/// (about 320 x 240, enough for colours, cheap to decode). Keeps the latest picture for sampling and
/// for the framing preview in the UI.
/// </summary>
sealed class CameraCapture : IDisposable
{
    CaptureDevice device;
    string openName, requestedName;
    long retryAfter;
    byte[] latest;                // encoded picture as FlashCap delivers it (BMP or JPEG)
    long frames;
    readonly object sync = new();

    public static string Status { get; private set; } = "off";
    public static string Error { get; private set; }

    static readonly string[] VirtualHints = { "ndi", "virtual", "virtuele", "obs", "droidcam", "snap camera", "stream" };

    /// <summary>Webcams the user can choose (DirectShow; virtual cameras last).</summary>
    public static List<string> Names()
    {
        try
        {
            return new CaptureDevices().EnumerateDescriptors()
                .Where(d => d.DeviceType == DeviceTypes.DirectShow && d.Characteristics.Length > 0)
                .OrderBy(d => IsVirtual(d.Name)).Select(d => d.Name).Distinct().ToList();
        }
        catch (Exception ex) { Error = ex.Message; return new List<string>(); }
    }

    static bool IsVirtual(string name) => VirtualHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));

    public bool Running => device != null;
    public long Frames => Interlocked.Read(ref frames);

    /// <summary>Opens <paramref name="name"/> (or the first real camera) unless it is already running.</summary>
    public void Ensure(string name)
    {
        lock (sync)
        {
            if (device != null && (name ?? "") == (requestedName ?? "")) return;   // what was asked is running (maybe via fallback)
            if (device == null && Environment.TickCount64 < retryAfter && (name ?? "") == (requestedName ?? "")) return;
            StopLocked();
            requestedName = name;
            try
            {
                var all = new CaptureDevices().EnumerateDescriptors().Where(d => d.Characteristics.Length > 0).ToList();
                CaptureDeviceDescriptor d =
                    all.FirstOrDefault(x => x.DeviceType == DeviceTypes.DirectShow && !string.IsNullOrEmpty(name) && x.Name == name)
                    ?? all.FirstOrDefault(x => x.DeviceType == DeviceTypes.DirectShow && !IsVirtual(x.Name))
                    ?? all.FirstOrDefault(x => x.DeviceType == DeviceTypes.DirectShow)
                    ?? all.FirstOrDefault();
                if (d == null) throw new InvalidOperationException("geen webcam gevonden");
                VideoCharacteristics ch = Pick(d.Characteristics);
                if (ch == null) throw new InvalidOperationException("webcam heeft geen bruikbaar beeldformaat");
                device = d.OpenAsync(ch, scope =>
                {
                    byte[] img = scope.Buffer.ExtractImage();
                    Volatile.Write(ref latest, img);
                    Interlocked.Increment(ref frames);
                }).GetAwaiter().GetResult();
                device.StartAsync().GetAwaiter().GetResult();
                openName = d.Name;
                retryAfter = 0;
                Status = "running"; Error = null;
                Log.Write($"Webcam aan: {d.Name} ({ch.Width}x{ch.Height} {ch.PixelFormat})");
            }
            catch (Exception ex)
            {
                StopLocked();
                retryAfter = Environment.TickCount64 + 3000;   // do not re-enumerate cameras every frame
                Status = "error";
                if (Error != ex.Message) Log.Write("Webcam: " + ex.Message);
                Error = ex.Message;
            }
        }
    }

    /// <summary>Smallest uncompressed format of at least 320 wide (JPEG as fallback), at least 10 fps.</summary>
    static VideoCharacteristics Pick(VideoCharacteristics[] all)
    {
        var usable = all.Where(c => c.PixelFormat != PixelFormats.Unknown && c.Width >= 320 && c.Width <= 1280).ToList();
        if (usable.Count == 0) usable = all.Where(c => c.PixelFormat != PixelFormats.Unknown).ToList();
        return usable.OrderBy(c => c.PixelFormat == PixelFormats.JPEG ? 1 : 0)
                     .ThenBy(c => (double)c.FramesPerSecond < 10 ? 1 : 0)
                     .ThenBy(c => c.Width * c.Height).FirstOrDefault();
    }

    /// <summary>The latest picture decoded to 24-bit BGR, or null.</summary>
    public (byte[] Pixels, int Width, int Height, int Stride)? Decode()
    {
        byte[] img = Volatile.Read(ref latest);
        if (img == null) return null;
        using var bmp = new Bitmap(new MemoryStream(img));
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var px = new byte[Math.Abs(data.Stride) * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
            return (px, bmp.Width, bmp.Height, Math.Abs(data.Stride));
        }
        finally { bmp.UnlockBits(data); }
    }

    /// <summary>The latest picture as JPEG (for framing the TV in the UI).</summary>
    public byte[] Jpeg()
    {
        byte[] img = Volatile.Read(ref latest);
        if (img == null) return null;
        using var bmp = new Bitmap(new MemoryStream(img));
        using var ms = new MemoryStream();
        ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
        bmp.Save(ms, codec, p);
        return ms.ToArray();
    }

    public void Stop() { lock (sync) StopLocked(); }

    void StopLocked()
    {
        if (device == null) return;
        try { device.StopAsync().GetAwaiter().GetResult(); } catch { }
        try { device.Dispose(); } catch { }
        device = null;
        Volatile.Write(ref latest, null);
        if (Status == "running") { Status = "off"; Log.Write("Webcam uit"); }
    }

    public void Dispose() => Stop();
}
