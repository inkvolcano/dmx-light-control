using System.Text.Json.Serialization;
using DmxLicht.Core.Effects;

namespace DmxLicht.Core.Show;

/// <summary>What drives the output: fixed colours per lamp, a running effect, or raw channel faders.</summary>
public enum OutputMode { Static, Effect, Faders }

/// <summary>How the sound input drives the master brightness.</summary>
public enum AudioModulation { None, Level, Bass, BeatPulse }

/// <summary>Sound-to-light settings.</summary>
public sealed class MusicSettings
{
    /// <summary>Listen to the audio input (needed for Level/Bass and for detected beats).</summary>
    public bool Enabled { get; set; }
    /// <summary>null/"" = default microphone, "loopback" = what this PC plays, else a device id.</summary>
    public string DeviceId { get; set; } = "loopback";
    /// <summary>Effects advance on the beat instead of by their own step time.</summary>
    public bool SyncEffects { get; set; }
    /// <summary>Effect steps per beat: 0.5, 1, 2 or 4.</summary>
    public double StepsPerBeat { get; set; } = 1;
    public AudioModulation Modulation { get; set; } = AudioModulation.None;
    /// <summary>Lowest brightness while modulated (0..100 % of the master).</summary>
    public int FloorPercent { get; set; } = 15;
    /// <summary>Extra input gain on top of the automatic gain (0.5..4).</summary>
    public double Sensitivity { get; set; } = 1.0;
}

/// <summary>On/off and colour of one fixture in Static mode.</summary>
public sealed class FixtureLook
{
    public bool On { get; set; }
    public ColorRef Color { get; set; } = ColorRef.Palette("white");
}

/// <summary>Everything that decides the output apart from the patch. The UI edits this; <see cref="FrameBuilder"/> reads it.</summary>
public sealed class ShowState
{
    public OutputMode Mode { get; set; } = OutputMode.Static;

    /// <summary>Master brightness 0..100. Does not apply in Faders mode.</summary>
    public int BrightnessPercent { get; set; } = 100;
    public bool Blackout { get; set; }

    /// <summary>Move the shared part of R/G/B to the white LED (on lamps that have one).</summary>
    public bool UseWhiteLed { get; set; } = true;

    /// <summary>Per fixture id.</summary>
    public Dictionary<string, FixtureLook> Looks { get; set; } = new();

    /// <summary>Raw fader values per fixture id; index 1..footprint (index 0 unused).</summary>
    public Dictionary<string, byte[]> Faders { get; set; } = new();

    public EffectSettings Effect { get; set; } = new();

    /// <summary>Settings for the "follow the screen" effect.</summary>
    public ScreenSyncSettings ScreenSync { get; set; } = new();

    /// <summary>The user's own chases (made in the chase editor).</summary>
    public List<CustomChase> Chases { get; set; } = new();

    /// <summary>Effects running on single groups, drawn over the rest (in this order).</summary>
    public List<EffectLayer> Layers { get; set; } = new();
    /// <summary>Lamps that show their own fixed colour while an effect runs (a colour or scene picked for a selection).</summary>
    public List<string> Held { get; set; } = new();

    /// <summary>Fade time for live changes (colours, on/off, effects) in ms; 0 = instant.</summary>
    public int FadeMs { get; set; }

    /// <summary>Strobe speed 1..100 (slow..fast) used while the strobe button is held.</summary>
    public int StrobeSpeed { get; set; } = 60;

    /// <summary>Strobe button held (not saved).</summary>
    [JsonIgnore] public bool StrobeActive { get; set; }

    /// <summary>Last recalled scene (for highlighting its button); cleared by other changes.</summary>
    public string ActiveScene { get; set; }

    public MusicSettings Music { get; set; } = new();

    /// <summary>The user's own colours (hex), shown under the palette.</summary>
    public List<string> CustomColors { get; set; } = new();

    /// <summary>Fixtures flashing to be found ("identify"). Not saved.</summary>
    [JsonIgnore] public HashSet<string> Identify { get; } = new();

    public FixtureLook Look(string fixtureId) =>
        Looks.TryGetValue(fixtureId, out FixtureLook l) ? l : (Looks[fixtureId] = new FixtureLook());

    public byte[] FaderValues(string fixtureId, int footprint)
    {
        if (!Faders.TryGetValue(fixtureId, out byte[] v) || v.Length < footprint + 1)
        {
            var n = new byte[footprint + 1];
            if (v != null) Array.Copy(v, n, Math.Min(v.Length, n.Length));
            Faders[fixtureId] = v = n;
        }
        return v;
    }
}
