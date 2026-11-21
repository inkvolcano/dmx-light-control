using System.Drawing;

namespace DmxLicht.Core.Effects;

/// <summary>Which settings an effect actually uses (the UI only enables those).</summary>
[Flags]
public enum EffectParams
{
    None = 0,
    ColorA = 1,
    ColorB = 2,
    Step = 4,
    OnOff = 8,
    Delay = 16,
    /// <summary>Reacts to the sound itself: needs "Listen" on the music page.</summary>
    Audio = 32,
    /// <summary>Follows the beat: works with tap tempo or listening.</summary>
    Beat = 64,
}

/// <summary>The chosen effect and its settings.</summary>
public sealed class EffectSettings
{
    public int EffectIndex { get; set; } = 8;
    /// <summary>When set, the user's own chase with this id runs instead of a library effect.</summary>
    public string ChaseId { get; set; }
    /// <summary>Follow the PC screen's colours (ambilight) instead of a library effect.</summary>
    public bool ScreenSync { get; set; }
    public int StepMs { get; set; } = 400;
    public int OnMs { get; set; } = 300;
    public int OffMs { get; set; } = 300;
    public int DelayMs { get; set; } = 150;
    public Color ColorA { get; set; } = Color.FromArgb(0, 255, 0);
    public Color ColorB { get; set; } = Color.FromArgb(255, 0, 0);

    /// <summary>Use <see cref="LampColors"/> instead of colour A (only for effects that use colour A).</summary>
    public bool ColorPerLamp { get; set; }
    /// <summary>Colour per head; heads beyond the list cycle through the default colours.</summary>
    public Color[] LampColors { get; set; } = DefaultLampColors();

    static readonly string[] DefHex = { "#00FF00", "#FF0000", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF" };

    public Color LampColor(int head) =>
        LampColors != null && head < LampColors.Length ? LampColors[head] : ColorUtil.ParseHex(DefHex[head % 6], Color.White);

    public static Color[] DefaultLampColors()
    {
        var c = new Color[64];
        for (int i = 0; i < c.Length; i++) c[i] = ColorUtil.ParseHex(DefHex[i % 6], Color.White);
        return c;
    }
}
