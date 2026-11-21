using System.Drawing;

namespace DmxLicht.Core.Effects;

/// <summary>Colour and level for one lamp in one frame. Level 0..1 is scaled by the master brightness.</summary>
public readonly record struct LampOutput(Light Color, double Level);

/// <summary>Computes one lamp's output for the current frame.</summary>
public delegate LampOutput EffectFunc(EffectFrame f, int lamp);

/// <summary>
/// Everything an effect needs for one frame, computed once and shared by all lamps.
/// Times are in milliseconds.
/// </summary>
public sealed class EffectFrame
{
    /// <summary>Running time.</summary>
    public long Ms { get; init; }
    /// <summary>Number of lamps (at least 1).</summary>
    public int N { get; init; }
    public bool UseWhite { get; init; }

    public int On { get; init; }
    public int Off { get; init; }
    public int Delay { get; init; }
    /// <summary>On + Off, at least 20.</summary>
    public int Period { get; init; }

    /// <summary>Step length, at least 40.</summary>
    public int StepMs { get; init; }
    /// <summary>Whole steps since the start: Ms / StepMs.</summary>
    public long Step { get; init; }
    /// <summary>Position within the current step, 0..1.</summary>
    public double Frac { get; init; }
    /// <summary>Time for one step across all lamps: StepMs * N.</summary>
    public double Cycle { get; init; }

    public Color ColorA { get; init; }
    public Color ColorB { get; init; }
    /// <summary>Colour B as LED values.</summary>
    public Light B { get; init; }

    internal Light[] LampA { get; init; }

    /// <summary>Sound and beat for sound-reactive effects (all zero when nothing is listening).</summary>
    public AudioFrame Audio { get; init; }

    /// <summary>Colour A for this lamp (or the lamp's own colour when "Kleur per lamp" is on).</summary>
    public Light A(int lamp) => LampA[lamp];

    public Light Rgb(Color c) => Light.FromColor(c, UseWhite);

    /// <summary>Fully saturated colour at <paramref name="hue"/> degrees.</summary>
    public Light Hue(double hue) => Rgb(ColorUtil.FromHsv(hue, 1, 1));
}
