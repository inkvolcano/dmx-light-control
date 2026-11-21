using DmxLicht.Core.Effects;

namespace DmxLicht.Core.Show;

public enum ClipKind { Scene, Effect }

/// <summary>A block on a track: a scene that comes in at its start, or an effect that runs for its length.</summary>
public sealed class TimelineClip
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public double StartMs { get; set; }
    public double LengthMs { get; set; } = 4000;
    public ClipKind Kind { get; set; }
    public string SceneId { get; set; }
    public EffectSettings Effect { get; set; }
    /// <summary>Scene clips: fade-in in ms; -1 = the scene's own fade time.</summary>
    public int FadeMs { get; set; } = -1;

    public double EndMs => StartMs + LengthMs;
}

/// <summary>A row of clips. Scene tracks hold scenes; effect tracks run effects on their target.</summary>
public sealed class TimelineTrack
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public string Name { get; set; }
    /// <summary>"scenes" or "effects".</summary>
    public string Kind { get; set; } = "effects";
    /// <summary>Effect tracks: "*" = all fixtures, otherwise a group id.</summary>
    public string Target { get; set; } = "*";
    public List<TimelineClip> Clips { get; set; } = new();
}

/// <summary>A point on the timeline: "wait" pauses playback until GO, "cue" is just a jump target.</summary>
public sealed class TimelineMarker
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public double AtMs { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; } = "wait";
}

public sealed class Timeline
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public string Name { get; set; } = "Timeline";
    public double LengthMs { get; set; } = 60000;
    /// <summary>Beat grid for snapping (also used as the music tempo while playing, when set).</summary>
    public double Bpm { get; set; } = 120;
    public bool Snap { get; set; } = true;
    public bool Loop { get; set; }
    /// <summary>Play region: playback starts here (Stop returns here) ...</summary>
    public double StartMs { get; set; }
    /// <summary>... and ends (or loops) here; null = the end of the timeline.</summary>
    public double? EndMs { get; set; }
    public List<TimelineMarker> Markers { get; set; } = new();

    /// <summary>File name in Instellingen\Audio, or null.</summary>
    public string AudioFile { get; set; }
    public List<TimelineTrack> Tracks { get; set; } = new();

    public static Timeline CreateDefault(string name, string scenesTrack, string effectsTrack) => new()
    {
        Name = name,
        Tracks =
        {
            new TimelineTrack { Name = scenesTrack, Kind = "scenes" },
            new TimelineTrack { Name = effectsTrack, Kind = "effects", Target = "*" },
        },
    };
}
