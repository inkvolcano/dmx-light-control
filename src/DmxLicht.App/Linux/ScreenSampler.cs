using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;

namespace DmxLicht.App;

/// <summary>
/// Linux build: following the screen or a webcam is not available (the Windows version uses GDI screen capture and
/// System.Drawing for the camera picture). Same members as the Windows class, so the API answers with a clear
/// message instead of failing; the screen effects stay dark.
/// </summary>
sealed class ScreenSampler : IDisposable
{
    public const string NotOnLinux = "Scherm volgen en de webcam werken (nog) niet op Linux / Screen sync and the webcam are not available on Linux";

    public ScreenSampler(LightEngine engine) { }

    public static void WantPreview() { }

    public CameraCapture Camera { get; } = new();

    public static List<string> Monitors() => new();

    public byte[] Snapshot(ScreenSyncSettings set, int w = 320, int h = 180) => throw new PlatformNotSupportedException(NotOnLinux);

    public void Dispose() { }
}

/// <summary>Linux build: no webcam (see <see cref="ScreenSampler"/>).</summary>
sealed class CameraCapture : IDisposable
{
    public static string Status => "unsupported";
    public static string Error => ScreenSampler.NotOnLinux;

    public static List<string> Names() => new();

    public bool Running => false;
    public long Frames => 0;

    public void Ensure(string name) { }

    public (byte[] Pixels, int Width, int Height, int Stride)? Decode() => null;

    public byte[] Jpeg() => null;

    public void Stop() { }

    public void Dispose() { }
}
