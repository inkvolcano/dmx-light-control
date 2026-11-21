namespace DmxLicht.Core.Legacy;

/// <summary>Which channel inside the lamp drives which emitter. 1-based; 0 = not used.</summary>
public sealed class ChannelMap
{
    public int Dimmer, Red, Green, Blue, White, Amber, UV;

    public ChannelMap() { }

    public ChannelMap(int dimmer, int red, int green, int blue, int white, int amber, int uv)
    {
        Dimmer = dimmer; Red = red; Green = green; Blue = blue; White = white; Amber = amber; UV = uv;
    }

    public ChannelMap Clone() => new(Dimmer, Red, Green, Blue, White, Amber, UV);
}

/// <summary>A known lamp type: its channel count, channel map and names for any extra channels.</summary>
public sealed class LampPreset
{
    public string Name { get; init; }
    public int ChannelCount { get; init; }
    public ChannelMap Map { get; init; }
    public IReadOnlyDictionary<int, string> ExtraChannels { get; init; } = new Dictionary<int, string>();

    public bool IsCustom => Map == null;
}

public static class LampPresets
{
    public const int CustomIndex = 4;

    public static readonly IReadOnlyList<LampPreset> All = new[]
    {
        new LampPreset
        {
            Name = "LED PAR 18W x 7 - 6 kanalen (D001): Rood, Groen, Blauw, Wit, Amber, UV",
            ChannelCount = 6, Map = new ChannelMap(0, 1, 2, 3, 4, 5, 6),
        },
        new LampPreset
        {
            Name = "LED PAR 18W x 7 - 10 kanalen (A001): Dimmer, R, G, B, W, A, UV, Strobo, Functie, Snelheid",
            ChannelCount = 10, Map = new ChannelMap(1, 2, 3, 4, 5, 6, 7),
            ExtraChannels = new Dictionary<int, string> { [8] = "Strobo", [9] = "Functie", [10] = "Snelheid" },
        },
        new LampPreset
        {
            Name = "8 kanalen (algemeen): Dimmer, R, G, B, W, A, UV, Strobo",
            ChannelCount = 8, Map = new ChannelMap(1, 2, 3, 4, 5, 6, 7),
            ExtraChannels = new Dictionary<int, string> { [8] = "Strobo" },
        },
        new LampPreset
        {
            Name = "4 kanalen (algemeen): Rood, Groen, Blauw, Wit",
            ChannelCount = 4, Map = new ChannelMap(0, 1, 2, 3, 4, 0, 0),
        },
        new LampPreset { Name = "Aangepast", ChannelCount = 0, Map = null },
    };
}
