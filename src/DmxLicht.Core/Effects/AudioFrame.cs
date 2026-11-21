namespace DmxLicht.Core.Effects;

/// <summary>
/// Sound as effects see it in one frame. Levels are 0..1 (auto-gained, smoothed so lights don't flicker);
/// <see cref="Beat"/> is the musical clock in beats (works with tap tempo alone, without listening);
/// <see cref="Surge"/> is true for a moment when the music suddenly gets much louder (a drop).
/// </summary>
public readonly record struct AudioFrame(double Level, double Bass, double Mid, double High, double Beat, bool Surge)
{
    public long BeatIndex => (long)Math.Floor(Beat);
    /// <summary>Position inside the current beat, 0..1.</summary>
    public double BeatPhase => Beat - Math.Floor(Beat);
}
