namespace DmxLicht.Core.Fixtures;

/// <summary>What a DMX channel of a lamp does.</summary>
public enum ChannelKind
{
    /// <summary>Intensity. On a whole-fixture channel (Cell = -1) it dims every cell.</summary>
    Dimmer,
    Red, Green, Blue, White, Amber, UV,
    /// <summary>HSV modes: hue, saturation, value.</summary>
    Hue, Saturation, Value,
    /// <summary>Any channel the engine does not drive: macros, programs, speeds, IDs. Held at <see cref="ChannelDef.Default"/>.</summary>
    Fixed,
    /// <summary>The lamp's own strobe: <see cref="ChannelDef.Default"/> = off, <see cref="ChannelDef.RangeMin"/>..<see cref="ChannelDef.RangeMax"/> = slow..fast.</summary>
    Strobe,
}

/// <summary>One channel in a fixture mode.</summary>
public sealed class ChannelDef
{
    /// <summary>1-based offset from the fixture's start address.</summary>
    public int Number { get; set; }
    public ChannelKind Kind { get; set; }
    /// <summary>Segment (pixel) this channel belongs to; -1 = the whole fixture.</summary>
    public int Cell { get; set; } = -1;
    /// <summary>Value sent for <see cref="ChannelKind.Fixed"/> channels (and Strobe when off).</summary>
    public byte Default { get; set; }
    /// <summary>Strobe: slowest value.</summary>
    public byte RangeMin { get; set; } = 1;
    /// <summary>Strobe: fastest value.</summary>
    public byte RangeMax { get; set; } = 255;
    /// <summary>Short label for faders and the patch sheet, e.g. "Strobe".</summary>
    public string Label { get; set; }
    /// <summary>Optional warning shown in the UI, e.g. "keep at 0-50 or the lamp runs its own programs".</summary>
    public string Note { get; set; }
}

/// <summary>One DMX personality of a fixture, e.g. "6ch" or "13ch (4 segments)".</summary>
public sealed class FixtureMode
{
    public string Name { get; set; }
    /// <summary>Number of DMX addresses the fixture occupies in this mode.</summary>
    public int Footprint { get; set; }
    /// <summary>Channels in the order they are written (later channels win when two share an address).</summary>
    public List<ChannelDef> Channels { get; set; } = new();
    /// <summary>Text shown on the lamp's display for this mode, e.g. "d001".</summary>
    public string DisplayCode { get; set; }

    public int CellCount => Math.Max(1, Channels.Where(c => c.Cell >= 0).Select(c => c.Cell + 1).DefaultIfEmpty(1).Max());

    public bool Has(ChannelKind kind, int cell) => Channels.Any(c => c.Kind == kind && (c.Cell == cell || c.Cell < 0));

    public string ChannelLabel(int number)
    {
        ChannelDef d = Channels.LastOrDefault(c => c.Number == number);
        return d?.Label ?? "";
    }
}

/// <summary>A lamp model with its DMX modes.</summary>
public sealed class FixtureType
{
    public string Id { get; set; }
    public string Manufacturer { get; set; }
    public string Model { get; set; }
    /// <summary>"par", "bar", "dimmer", "generic"...</summary>
    public string Category { get; set; } = "generic";
    /// <summary>Emitters, for display: e.g. "RGBWA+UV".</summary>
    public string Colors { get; set; }
    public List<FixtureMode> Modes { get; set; } = new();
    public string Notes { get; set; }
    /// <summary>True for types loaded from the user's Fixtures folder.</summary>
    public bool Custom { get; set; }

    public FixtureMode Mode(string name) => Modes.FirstOrDefault(m => m.Name == name) ?? Modes.FirstOrDefault();
}
