using System.Text.Json;
using System.Text.RegularExpressions;

namespace DmxLicht.Core.Fixtures;

/// <summary>
/// Converts an Open Fixture Library fixture (the "OFL JSON" download from open-fixture-library.org)
/// into a <see cref="FixtureType"/>. Handles intensity, RGBWA+UV colour channels, strobe ranges,
/// fine channels, and pixel matrices (each pixel becomes a segment). Anything else becomes a
/// fixed channel held at its default or "open/no function" value.
/// </summary>
public static class OflImport
{
    public static FixtureType Convert(string json, string fileName = null)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        string name = Str(root, "name") ?? Path.GetFileNameWithoutExtension(fileName ?? "fixture");
        string manufacturer = Str(root, "manufacturerKey") ?? Str(root, "manufacturer") ?? "OFL";

        var available = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("availableChannels", out JsonElement ac))
            foreach (JsonProperty p in ac.EnumerateObject())
            {
                available[p.Name] = p.Value;
                if (p.Value.TryGetProperty("fineChannelAliases", out JsonElement fines))
                    foreach (JsonElement f in fines.EnumerateArray()) available[f.GetString()] = default; // fine = fixed 0
            }

        // pixel matrix: keys in order
        var pixelKeys = new List<string>();
        if (root.TryGetProperty("matrix", out JsonElement matrix))
        {
            if (matrix.TryGetProperty("pixelKeys", out JsonElement pk))
                foreach (JsonElement z in pk.EnumerateArray()) foreach (JsonElement y in z.EnumerateArray()) foreach (JsonElement x in y.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.String) pixelKeys.Add(x.GetString());
            if (pixelKeys.Count == 0 && matrix.TryGetProperty("pixelCount", out JsonElement pc))
            {
                int[] n = pc.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                int count = n.Aggregate(1, (a, b) => a * Math.Max(1, b));
                for (int i = 1; i <= count; i++) pixelKeys.Add(i.ToString());
            }
        }
        var templates = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("templateChannels", out JsonElement tc))
            foreach (JsonProperty p in tc.EnumerateObject()) templates[p.Name] = p.Value;

        var type = new FixtureType
        {
            Id = "ofl-" + Slug(manufacturer + "-" + name),
            Manufacturer = Title(manufacturer),
            Model = name,
            Category = root.TryGetProperty("categories", out JsonElement cats) && cats.EnumerateArray().Any(c => c.GetString() == "Pixel Bar") ? "bar" : "par",
            Notes = "Imported from Open Fixture Library",
        };

        if (root.TryGetProperty("modes", out JsonElement modes))
            foreach (JsonElement m in modes.EnumerateArray())
            {
                var mode = new FixtureMode { Name = Str(m, "name") ?? "Mode", DisplayCode = Str(m, "shortName") };
                int number = 0;
                foreach (JsonElement ch in m.GetProperty("channels").EnumerateArray())
                {
                    if (ch.ValueKind == JsonValueKind.Null) { number++; continue; }  // unused address
                    if (ch.ValueKind == JsonValueKind.String) { mode.Channels.Add(Channel(++number, ch.GetString(), -1, available)); continue; }
                    if (ch.ValueKind == JsonValueKind.Object && Str(ch, "insert") == "matrixChannels")
                    {
                        List<string> keys = ch.GetProperty("repeatFor").ValueKind == JsonValueKind.Array
                            ? ch.GetProperty("repeatFor").EnumerateArray().Select(e => e.GetString()).ToList()
                            : pixelKeys;
                        var tpl = ch.GetProperty("templateChannels").EnumerateArray().Select(e => e.GetString()).ToList();
                        bool perPixel = Str(ch, "channelOrder") != "perChannel";
                        var order = perPixel
                            ? keys.SelectMany((k, i) => tpl.Select(t => (k, i, t)))
                            : tpl.SelectMany(t => keys.Select((k, i) => (k, i, t)));
                        foreach (var (key, cell, t) in order)
                        {
                            string chName = t.Replace("$pixelKey", key);
                            templates.TryGetValue(t, out JsonElement def);
                            var dict = new Dictionary<string, JsonElement>(available, StringComparer.OrdinalIgnoreCase) { [chName] = def };
                            mode.Channels.Add(Channel(++number, chName, cell, dict));
                        }
                    }
                }
                mode.Footprint = Math.Max(number, 1);
                if (mode.Channels.Count > 0) type.Modes.Add(mode);
            }

        var kinds = type.Modes.SelectMany(m => m.Channels).Select(c => c.Kind).ToHashSet();
        type.Colors = string.Concat(new[] { (ChannelKind.Red, "R"), (ChannelKind.Green, "G"), (ChannelKind.Blue, "B"), (ChannelKind.White, "W"), (ChannelKind.Amber, "A") }
            .Where(k => kinds.Contains(k.Item1)).Select(k => k.Item2)) + (kinds.Contains(ChannelKind.UV) ? "+UV" : "");
        if (type.Colors == "") type.Colors = kinds.Contains(ChannelKind.Dimmer) ? "W" : null;
        if (type.Modes.Count == 0) throw new InvalidDataException("No modes found in this OFL file");
        return type;
    }

    static ChannelDef Channel(int number, string name, int cell, Dictionary<string, JsonElement> available)
    {
        var def = new ChannelDef { Number = number, Label = Short(name), Cell = cell, Kind = ChannelKind.Fixed };
        if (!available.TryGetValue(name, out JsonElement ch) || ch.ValueKind != JsonValueKind.Object) return def; // fine channel or unknown
        if (ch.TryGetProperty("defaultValue", out JsonElement dv) && dv.ValueKind == JsonValueKind.Number) def.Default = (byte)Math.Clamp(dv.GetInt32(), 0, 255);

        List<JsonElement> caps = ch.TryGetProperty("capability", out JsonElement one) ? new() { one }
            : ch.TryGetProperty("capabilities", out JsonElement many) ? many.EnumerateArray().ToList() : new();
        if (caps.Count == 0) return def;

        if (caps.Count == 1)
        {
            string t = Str(caps[0], "type");
            if (t == "Intensity") { def.Kind = ChannelKind.Dimmer; return def; }
            if (t == "ColorIntensity")
            {
                def.Kind = Str(caps[0], "color") switch
                {
                    "Red" => ChannelKind.Red, "Green" => ChannelKind.Green, "Blue" => ChannelKind.Blue,
                    "White" or "Warm White" or "Cold White" => ChannelKind.White,
                    "Amber" or "Yellow" => ChannelKind.Amber, "UV" => ChannelKind.UV,
                    _ => ChannelKind.Fixed,
                };
                return def;
            }
        }

        // strobe: a ShutterStrobe range with a strobe effect; the open/off range is the default
        JsonElement strobe = caps.FirstOrDefault(c => Str(c, "type") == "ShutterStrobe" && Str(c, "shutterEffect") is "Strobe" or "RandomStrobe" && c.TryGetProperty("dmxRange", out _));
        if (strobe.ValueKind == JsonValueKind.Object && caps.All(c => Str(c, "type") is "ShutterStrobe" or "NoFunction"))
        {
            def.Kind = ChannelKind.Strobe;
            int[] r = strobe.GetProperty("dmxRange").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            def.RangeMin = (byte)Math.Clamp(r[0], 0, 255);
            def.RangeMax = (byte)Math.Clamp(r[1], 0, 255);
            JsonElement open = caps.FirstOrDefault(c => Str(c, "shutterEffect") == "Open" || Str(c, "type") == "NoFunction");
            def.Default = open.ValueKind == JsonValueKind.Object ? (byte)open.GetProperty("dmxRange")[0].GetInt32() : (byte)0;
            return def;
        }

        // anything else: hold at the "no function"/"open" range, so the lamp follows DMX
        JsonElement idle = caps.FirstOrDefault(c => Str(c, "type") is "NoFunction" || Str(c, "shutterEffect") == "Open");
        if (idle.ValueKind == JsonValueKind.Object && !ch.TryGetProperty("defaultValue", out _))
            def.Default = (byte)idle.GetProperty("dmxRange")[0].GetInt32();
        string comment = string.Join(", ", caps.Take(4).Select(c => Str(c, "comment") ?? Str(c, "type")).Where(x => x != null));
        if (comment.Length > 0) def.Note = comment;
        return def;
    }

    static string Str(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string Short(string name) => name.Length <= 14 ? name : name[..14];
    static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    static string Title(string s) => string.Join(" ", s.Split('-', ' ').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
