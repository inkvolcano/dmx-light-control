using System.Text.Json;
using DmxLicht.Core.Storage;
using static DmxLicht.Core.Fixtures.ChannelKind;

namespace DmxLicht.Core.Fixtures;

/// <summary>
/// All known lamp types: the built-in ones below plus any JSON files in "Instellingen\Fixtures"
/// (same format as <see cref="FixtureType"/>; a user file with a built-in id replaces the built-in).
/// </summary>
public sealed class FixtureLibrary
{
    readonly List<FixtureType> types;

    public IReadOnlyList<FixtureType> Types => types;

    public FixtureLibrary(IEnumerable<FixtureType> types) { this.types = types.ToList(); }

    public FixtureType Find(string id) => types.FirstOrDefault(t => t.Id == id);

    /// <summary>Built-in types plus user types from <paramref name="folder"/> (if it exists).</summary>
    public static FixtureLibrary Load(string folder)
    {
        var all = BuiltIn().ToDictionary(t => t.Id);
        if (folder != null && Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    FixtureType t = JsonSerializer.Deserialize<FixtureType>(File.ReadAllText(file), Json.Options);
                    if (t?.Id == null || t.Modes.Count == 0) continue;
                    t.Custom = true;
                    foreach (FixtureMode m in t.Modes)
                        if (m.Footprint < 1) m.Footprint = m.Channels.Select(c => c.Number).DefaultIfEmpty(1).Max();
                    all[t.Id] = t;
                }
                catch (Exception ex) { Log.Write("Lamptype niet geladen (" + Path.GetFileName(file) + "): " + ex.Message); }
            }
        }
        return new FixtureLibrary(all.Values);
    }

    // ------------------------------------------------------------------ built-in types

    const string FunctionNote = "Houd op 0-50, anders draait de lamp eigen programma's / keep at 0-50, otherwise the lamp runs its own programs";

    public static IEnumerable<FixtureType> BuiltIn()
    {
        // LED_Par_2026.pdf
        yield return new FixtureType
        {
            Id = "ledpar-18x7-rgbwauv",
            Manufacturer = "Generic",
            Model = "LED PAR 18W x 7",
            Category = "par",
            Colors = "RGBWA+UV",
            Modes =
            {
                Mode("6ch", "d001", C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A"), C(UV, "UV")),
                Mode("10ch", "A001", C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A"), C(UV, "UV"),
                     S(1, 255), F("Function", note: FunctionNote), F("Speed")),
            },
        };

        // Expolite_Tourled_42_cm_ip33_en.pdf (14 x 3W tri-colour RGB)
        yield return new FixtureType
        {
            Id = "expolite-tourled-42cm",
            Manufacturer = "Expolite",
            Model = "TourLED 42 CM IP33",
            Category = "par",
            Colors = "RGB",
            Notes = "TOUR mode: CH10 (ID) must be 0 unless ID addressing is used.",
            Modes =
            {
                Mode("TOUR 10ch", "TOUR", C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"),
                     F("Macro"), S(11, 255), F("Auto"), F("Auto speed"), F("Dim curve"), F("ID", note: "0 = alle ID's / all IDs")),
                Mode("Arc.1 3ch", "Arc.1", C(Red, "R"), C(Green, "G"), C(Blue, "B")),
                Mode("Ar1.d 4ch", "Ar1.d", C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B")),
                Mode("Ar1.s 5ch", "Ar1.s", C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), S(11, 255)),
                Mode("HSV 3ch", "HSV", C(Hue, "H"), C(Saturation, "S"), C(Value, "V")),
            },
        };

        // Stairville xBrick Full-Colour 16x3W.pdf
        yield return new FixtureType
        {
            Id = "stairville-xbrick-16x3",
            Manufacturer = "Stairville",
            Model = "xBrick Full-Colour 16x3W",
            Category = "bar",
            Colors = "RGB",
            Modes =
            {
                Mode("5ch", "5CH", C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(Dimmer, "Dim"), S(1, 255)),
                Mode("13ch (4 segments)", "13CH",
                     C(Red, "R1", 0), C(Green, "G1", 0), C(Blue, "B1", 0),
                     C(Red, "R2", 1), C(Green, "G2", 1), C(Blue, "B2", 1),
                     C(Red, "R3", 2), C(Green, "G3", 2), C(Blue, "B3", 2),
                     C(Red, "R4", 3), C(Green, "G4", 3), C(Blue, "B4", 3),
                     C(Dimmer, "Dim")),
            },
        };

        // SGM-P2.pdf (user manual) + DMX_Protocol_P_2_Rev_6.pdf (sgmlighting.com): 18 x 10W RGBW, 3 panels
        // Shutter: 0-7 closed (dark!), 8-15 open, 16-151 strobe fast->slow, ..., 245-255 open.
        // CTC: 0 = ~5600K, 1-4 = no colour-temperature correction, 5-255 = 2000K-10000K.
        // 16-bit modes: the fine channel is held at 0 (8-bit is plenty for this app).
        yield return new FixtureType
        {
            Id = "sgm-p2",
            Manufacturer = "SGM",
            Model = "P-2 (RGBW, 3 panels)",
            Category = "wash",
            Colors = "RGBW",
            Notes = "Factory default: 6ch CTC mode, address 1. 'Calibrated' modes mix white in by themselves; RAW modes give direct RGBW. " +
                    "The shutter channel must be open (it is held open here), otherwise the lamp stays dark.",
            Modes =
            {
                Mode("6ch CTC (calibrated)", "6CH CTC", P2Shutter(), C(Dimmer, "Dim"), P2Ctc(), C(Red, "R"), C(Green, "G"), C(Blue, "B")),
                Mode("3ch RGB (calibrated)", "3CH", C(Red, "R"), C(Green, "G"), C(Blue, "B")),
                Mode("4ch RGBW (RAW)", "4CH RAW", C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W")),
                Mode("6ch RAW", "6CH RAW", P2Shutter(), C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W")),
                Mode("9ch (3 panels, calibrated)", "9CH",
                     C(Red, "R1", 0), C(Green, "G1", 0), C(Blue, "B1", 0),
                     C(Red, "R2", 1), C(Green, "G2", 1), C(Blue, "B2", 1),
                     C(Red, "R3", 2), C(Green, "G3", 2), C(Blue, "B3", 2)),
                Mode("10ch RGBW 16 bit", "10CH",
                     C(Dimmer, "Dim"), Fine("Dim"), C(Red, "R"), Fine("R"), C(Green, "G"), Fine("G"),
                     C(Blue, "B"), Fine("B"), C(White, "W"), Fine("W")),
                Mode("10ch CTC 16 bit (calibrated)", "10CH CTC",
                     P2Shutter(), C(Dimmer, "Dim"), Fine("Dim"), P2Ctc(),
                     C(Red, "R"), Fine("R"), C(Green, "G"), Fine("G"), C(Blue, "B"), Fine("B")),
                Mode("12ch (3 panels, CTC)", "12CH",
                     P2Shutter(), C(Dimmer, "Dim"), P2Ctc(),
                     C(Red, "R1", 0), C(Green, "G1", 0), C(Blue, "B1", 0),
                     C(Red, "R2", 1), C(Green, "G2", 1), C(Blue, "B2", 1),
                     C(Red, "R3", 2), C(Green, "G3", 2), C(Blue, "B3", 2)),
                Mode("15ch (3 panels RGBW, RAW)", "15CH RAW",
                     P2Shutter(), C(Dimmer, "Dim"), P2Ctc(),
                     C(Red, "R1", 0), C(Green, "G1", 0), C(Blue, "B1", 0), C(White, "W1", 0),
                     C(Red, "R2", 1), C(Green, "G2", 1), C(Blue, "B2", 1), C(White, "W2", 1),
                     C(Red, "R3", 2), C(Green, "G3", 2), C(Blue, "B3", 2), C(White, "W3", 2)),
                Mode("22ch (3 panels, 16 bit)", "22CH",
                     P2Shutter(), C(Dimmer, "Dim"), Fine("Dim"), P2Ctc(),
                     C(Red, "R1", 0), Fine("R1"), C(Green, "G1", 0), Fine("G1"), C(Blue, "B1", 0), Fine("B1"),
                     C(Red, "R2", 1), Fine("R2"), C(Green, "G2", 1), Fine("G2"), C(Blue, "B2", 1), Fine("B2"),
                     C(Red, "R3", 2), Fine("R3"), C(Green, "G3", 2), Fine("G3"), C(Blue, "B3", 2), Fine("B3")),
            },
        };

        // Generic types for anything else
        yield return Generic("generic-dimmer", "Dimmer", "W", Mode("1ch", null, C(Dimmer, "Dim")));
        yield return Generic("generic-rgb", "RGB", "RGB",
            Mode("3ch", null, C(Red, "R"), C(Green, "G"), C(Blue, "B")),
            Mode("4ch Dim", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B")),
            Mode("5ch Dim+Strobe", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), S(1, 255)));
        yield return Generic("generic-rgbw", "RGBW", "RGBW",
            Mode("4ch", null, C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W")),
            Mode("5ch Dim", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W")),
            Mode("6ch Dim+Strobe", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), S(1, 255)));
        yield return Generic("generic-rgba", "RGBA", "RGBA",
            Mode("4ch", null, C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(Amber, "A")));
        yield return Generic("generic-rgbwa", "RGBWA", "RGBWA",
            Mode("5ch", null, C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A")),
            Mode("6ch Dim", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A")));
        yield return Generic("generic-rgbwauv", "RGBWA+UV", "RGBWA+UV",
            Mode("6ch", null, C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A"), C(UV, "UV")),
            Mode("7ch Dim", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A"), C(UV, "UV")),
            Mode("8ch Dim+Strobe", null, C(Dimmer, "Dim"), C(Red, "R"), C(Green, "G"), C(Blue, "B"), C(White, "W"), C(Amber, "A"), C(UV, "UV"), S(1, 255)));
    }

    static FixtureType Generic(string id, string model, string colors, params FixtureMode[] modes)
    {
        var t = new FixtureType { Id = id, Manufacturer = "Generic", Model = model, Category = "generic", Colors = colors };
        t.Modes.AddRange(modes);
        return t;
    }

    /// <summary>Numbers the channels 1..n in the order given.</summary>
    static FixtureMode Mode(string name, string code, params ChannelDef[] channels)
    {
        for (int i = 0; i < channels.Length; i++) channels[i].Number = i + 1;
        return new FixtureMode { Name = name, DisplayCode = code, Footprint = channels.Length, Channels = channels.ToList() };
    }

    static ChannelDef C(ChannelKind kind, string label, int cell = -1) => new() { Kind = kind, Label = label, Cell = cell };

    static ChannelDef F(string label, byte value = 0, string note = null) => new() { Kind = Fixed, Label = label, Default = value, Note = note };

    /// <summary>The lamp's strobe channel: 0 = off, min..max = slow..fast (ranges from the manuals).</summary>
    static ChannelDef S(byte min, byte max) => new() { Kind = ChannelKind.Strobe, Label = "Strobe", Default = 0, RangeMin = min, RangeMax = max };

    /// <summary>Low byte of a 16-bit value; held at 0 (the coarse channel carries the value).</summary>
    static ChannelDef Fine(string of) => new() { Kind = Fixed, Label = of + " fine", Default = 0 };

    /// <summary>SGM shutter: off = open (250), strobe 151 (slow) .. 16 (fast). 0-7 would close the shutter.</summary>
    static ChannelDef P2Shutter() => new()
    {
        Kind = ChannelKind.Strobe, Label = "Shutter", Default = 250, RangeMin = 151, RangeMax = 16,
        Note = "0-7 = dicht (donker) / closed (dark)",
    };

    /// <summary>SGM colour temperature correction: 2 = none, so the colours come out as sent.</summary>
    static ChannelDef P2Ctc() => new() { Kind = Fixed, Label = "CTC", Default = 2, Note = "1-4 = geen correctie; 5-255 = 2000K-10000K" };
}
