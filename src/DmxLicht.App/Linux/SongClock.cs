using DmxLicht.Core;
using DmxLicht.Core.Engine;

namespace DmxLicht.App;

/// <summary>
/// Linux build: playing a song along with a timeline is not available (the Windows version uses Media Foundation
/// and WASAPI). Timelines run on a stopwatch; uploading a song is refused with a clear message.
/// </summary>
static class SongClock
{
    public const string NotOnLinux = "Liedjes afspelen bij een tijdlijn werkt (nog) niet op Linux / Playing songs with a timeline is not available on Linux";

    static string warned;

    public static ITimelineClock For(DmxLicht.Core.Show.Timeline t, string audioDir)
    {
        if (!string.IsNullOrEmpty(t.AudioFile) && warned != t.AudioFile)
        {
            warned = t.AudioFile;
            Log.Write("Tijdlijn met liedje (" + t.AudioFile + ") loopt zonder geluid: " + NotOnLinux);
        }
        return new StopwatchClock();
    }

    public static (double lengthMs, float[] peaks) Peaks(string path, int buckets) => throw new PlatformNotSupportedException(NotOnLinux);
}
