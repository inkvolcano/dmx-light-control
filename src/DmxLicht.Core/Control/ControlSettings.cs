namespace DmxLicht.Core.Control;

/// <summary>A MIDI note or controller linked to a command.</summary>
public sealed class MidiMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>"note" or "cc".</summary>
    public string Type { get; set; } = "note";
    /// <summary>1..16, or 0 = any channel.</summary>
    public int Channel { get; set; }
    public int Number { get; set; }
    /// <summary>Command line, e.g. "scene Rood". For a CC with a value command ("master", "bpm") the value is added.</summary>
    public string Command { get; set; } = "";
}

/// <summary>External control: OSC, MIDI and the HTTP trigger key.</summary>
public sealed class ControlSettings
{
    public bool OscEnabled { get; set; }
    public int OscPort { get; set; } = 8000;
    public bool MidiEnabled { get; set; }
    public string MidiDevice { get; set; }
    public List<MidiMapping> MidiMappings { get; set; } = new();
    /// <summary>Needed in trigger URLs from other devices (?key=...). Generated once.</summary>
    public string TriggerKey { get; set; } = Guid.NewGuid().ToString("N")[..10];
}
