namespace DmxLicht.Core.Legacy;

/// <summary>
/// Where the lamps sit in the DMX universe: <see cref="LampCount"/> identical lamps, the first at
/// <see cref="StartAddress"/>, each next one <see cref="Spacing"/> channels further.
/// </summary>
public sealed class FixtureLayout
{
    public const int MaxLamps = 64;

    public int LampCount = 10;
    public int StartAddress = 1;
    public int Spacing = 8;
    public int PresetIndex = 0;
    public ChannelMap Map = new(0, 1, 2, 3, 4, 5, 6);

    public LampPreset Preset => PresetIndex >= 0 && PresetIndex < LampPresets.All.Count ? LampPresets.All[PresetIndex] : null;

    /// <summary>Channel count of the chosen lamp type; 0 for "Aangepast".</summary>
    public int PresetChannelCount
    {
        get
        {
            LampPreset p = Preset;
            return p == null || p.IsCustom ? 0 : p.ChannelCount;
        }
    }

    /// <summary>How many raw channels per lamp the Faders tab shows.</summary>
    public int FaderChannelCount
    {
        get
        {
            int t = PresetChannelCount;
            return t > 0 ? t : Math.Min(Spacing, 12);
        }
    }

    /// <summary>DMX address just before the lamp's first channel (lamp channel c = BaseAddress + c).</summary>
    public int BaseAddress(int lamp) => StartAddress - 1 + lamp * Spacing;

    /// <summary>Highest DMX channel in use, 1..512.</summary>
    public int UsedChannelCount => Math.Max(1, Math.Min(512, StartAddress - 1 + LampCount * Spacing));

    /// <summary>"3\nBlauw" style label for channel <paramref name="c"/> inside a lamp.</summary>
    public string ChannelName(int c)
    {
        string n = "kanaal";
        if (Map.Dimmer == c) n = "Dimmer";
        else if (Map.Red == c) n = "Rood";
        else if (Map.Green == c) n = "Groen";
        else if (Map.Blue == c) n = "Blauw";
        else if (Map.White == c) n = "Wit";
        else if (Map.Amber == c) n = "Amber";
        else if (Map.UV == c) n = "UV";
        else if (Preset != null && Preset.ExtraChannels.TryGetValue(c, out string extra)) n = extra;
        return c + "\n" + n;
    }
}
