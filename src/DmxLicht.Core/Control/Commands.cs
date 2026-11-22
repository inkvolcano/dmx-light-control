using System.Drawing;
using System.Globalization;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;

namespace DmxLicht.Core.Control;

/// <summary>
/// One command language for everything that controls the show from outside:
/// HTTP trigger URLs, OSC, MIDI mappings and the Companion module.
/// <code>
/// scene Rood | scene 2        blackout [on|off|toggle]     master 0..100
/// on [group] | off [group]    color red|#FF8800 [group]    effect fire|12 [group]
/// stop                        tap | bpm 128                strobe on|off
/// flash green [seconds] [blink|solid]                       fade 1.5
/// timeline play|go|pause|stop|toggle|next|prev [name] · timeline seek &lt;seconds&gt;
/// </code>
/// </summary>
public static class Commands
{
    public static readonly string[] All =
    {
        "scene", "blackout", "master", "on", "off", "color", "effect", "stop", "tap", "bpm", "strobe", "flash", "fade", "timeline", "recover",
    };

    /// <summary>
    /// Checks a command line without running it (for the schedule editor): null when it would work,
    /// else what is wrong ("scene not found: X", "unknown command: Y").
    /// </summary>
    public static string Validate(LightEngine engine, string line)
    {
        var parts = Split(line ?? "");
        if (parts.Count == 0) return "empty command";
        string verb = parts[0].ToLowerInvariant(), a0 = parts.Count > 1 ? parts[1] : null;
        if (verb is "colour") verb = "color";
        if (verb is "brightness" or "usb") return null;
        if (!All.Contains(verb)) return "unknown command: " + parts[0];
        return verb switch
        {
            "scene" => engine.Read(s => Find(s.Scenes, a0, x => x.Name)) == null ? "scene not found: " + a0 : null,
            "color" => ParseColor(a0) == null ? "unknown colour: " + a0 : null,
            "flash" => a0 != null && ParseColor(a0) == null ? "unknown colour: " + a0 : null,
            "effect" => FindEffect(a0) >= 0 || IsScreen(a0) || engine.Read(s => Find(s.State.Chases, a0, x => x.Name)) != null ? null : "effect not found: " + a0,
            "timeline" when parts.Count > 2 && a0 is "play" or "go" => engine.Read(s => Find(s.Timelines, parts[2], x => x.Name)) == null ? "timeline not found: " + parts[2] : null,
            _ => null,
        };
    }

    /// <summary>"3" or "3.0" (OSC sends numbers as floats) as a whole number.</summary>
    static bool WholeNumber(string key, out int n)
    {
        n = 0;
        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return true;
        if (double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d == Math.Floor(d) && d is >= 1 and <= int.MaxValue)
        { n = (int)d; return true; }
        return false;
    }

    /// <summary>"effect screen" = follow the PC screen (ambilight).</summary>
    public static bool IsScreen(string name) => name != null && (name.Equals("screen", StringComparison.OrdinalIgnoreCase) || name.Equals("scherm", StringComparison.OrdinalIgnoreCase));

    /// <summary>Fade for this command instead of its own (scene fade, transition time); null = normal.</summary>
    [ThreadStatic] static int? fadeOverride;
    static int Fade(int normal) => fadeOverride ?? normal;

    /// <summary>
    /// Runs a command line with its own fade: e.g. the schedule's "over 30 minutes from dark to warm white".
    /// Works for scene, on/off, colour, effect, stop, blackout and master.
    /// </summary>
    public static string Run(LightEngine engine, string line, int? fadeMs)
    {
        int? old = fadeOverride;
        fadeOverride = fadeMs is int f && f > 0 ? f : null;
        try { return Run(engine, line); }
        finally { fadeOverride = old; }
    }

    /// <summary>Runs a command line like "scene Rood". Returns "ok" or an error text.</summary>
    public static string Run(LightEngine engine, string line)
    {
        var parts = Split(line);
        return parts.Count == 0 ? "empty command" : Run(engine, parts[0], parts.Skip(1).ToList());
    }

    public static string Run(LightEngine engine, string command, IReadOnlyList<string> args)
    {
        string a0 = args.Count > 0 ? args[0] : null, a1 = args.Count > 1 ? args[1] : null;
        try
        {
            switch (command.Trim().ToLowerInvariant())
            {
                case "scene":
                {
                    Scene sc = engine.Read(s => Find(s.Scenes, a0, x => x.Name));
                    if (sc == null) return "scene not found: " + a0;
                    engine.Change(s => { SceneOps.Apply(sc.State, s.State, s.Patch.Select(p => p.Id)); s.State.ActiveScene = sc.Id; }, fade: Fade(sc.FadeMs), cut: true);
                    return "ok";
                }
                case "blackout":
                    engine.Change(s => s.State.Blackout = a0 == null || a0 == "toggle" ? !s.State.Blackout : Bool(a0), fade: Fade(0), cut: true);
                    return "ok";
                case "master":
                case "brightness":
                {
                    double v = Number(a0);
                    if (v <= 1 && a0?.Contains('.') == true) v *= 100;   // OSC faders send 0..1
                    engine.Change(s => s.State.BrightnessPercent = (int)Math.Round(Math.Clamp(v, 0, 100)), fade: Fade(0), cut: true);
                    return "ok";
                }
                case "on":
                case "off":
                {
                    bool on = command.Equals("on", StringComparison.OrdinalIgnoreCase);
                    Live(engine, s =>
                    {
                        foreach (string id in Targets(s, a0)) s.State.Look(id).On = on;
                        s.State.Mode = OutputMode.Static;
                    });
                    return "ok";
                }
                case "color":
                case "colour":
                {
                    ColorRef c = ParseColor(a0);
                    if (c == null) return "unknown colour: " + a0;
                    Live(engine, s =>
                    {
                        foreach (string id in Targets(s, a1)) { FixtureLook l = s.State.Look(id); l.On = true; l.Color = c; }
                        s.State.Mode = OutputMode.Static;
                    });
                    return "ok";
                }
                case "effect":
                {
                    bool screen = IsScreen(a0);
                    int fx = screen ? -1 : FindEffect(a0);
                    CustomChase chase = fx >= 0 || screen ? null : engine.Read(s => Find(s.State.Chases, a0, x => x.Name));
                    if (fx < 0 && chase == null && !screen) return "effect not found: " + a0;
                    Live(engine, s =>
                    {
                        FixtureGroup g = a1 == null ? null : Find(s.Groups, a1, x => x.Name);
                        EffectSettings e = SceneOps.Clone(s.State.Effect);
                        e.ScreenSync = screen;
                        if (screen) e.ChaseId = null;
                        else if (chase != null) e.ChaseId = chase.Id;
                        else { e.EffectIndex = fx; e.ChaseId = null; }
                        if (g == null) { s.State.Effect = e; s.State.Mode = OutputMode.Effect; }
                        else
                        {
                            s.State.Layers.RemoveAll(l => l.GroupId == g.Id && l.Source == null);
                            s.State.Layers.Add(new EffectLayer { GroupId = g.Id, Effect = e });
                        }
                    });
                    return "ok";
                }
                case "stop":
                    Live(engine, s => { s.State.Layers.RemoveAll(l => l.Source == null); if (s.State.Mode == OutputMode.Effect) s.State.Mode = OutputMode.Static; });
                    return "ok";
                case "recover":
                case "usb":
                    if (engine.Output is DmxLicht.Core.Output.MultiOutput mo) mo.Udmx.Recover(); else engine.Output.Reconnect();
                    return "ok";
                case "tap":
                    engine.Beat.Tap(engine.Ms);
                    return "ok";
                case "bpm":
                    engine.Beat.SetBpm(Number(a0), engine.Ms);
                    return "ok";
                case "strobe":
                    engine.Change(s => s.State.StrobeActive = a0 == null || a0 == "toggle" ? !s.State.StrobeActive : Bool(a0));
                    return "ok";
                case "flash":
                {
                    ColorRef c = ParseColor(a0 ?? "white");
                    if (c == null) return "unknown colour: " + a0;
                    double secs = a1 == null ? 3 : Number(a1);
                    bool solid = args.Count > 2 && args[2].Equals("solid", StringComparison.OrdinalIgnoreCase);
                    engine.Flash(c.Resolve().Show, (int)(Math.Clamp(secs, 0.2, 600) * 1000), solid);
                    return "ok";
                }
                case "fade":
                    engine.Change(s => s.State.FadeMs = (int)Math.Clamp(Number(a0) * 1000, 0, 60000));
                    return "ok";
                case "timeline":
                {
                    string verb = (a0 ?? "play").ToLowerInvariant();
                    if (verb == "go") verb = "play";      // GO = continue after a wait point
                    if (verb == "stop") { engine.WithTimeline((p, _) => p.Stop()); return "ok"; }
                    if (verb == "pause") { engine.WithTimeline((p, _) => p.Pause()); return "ok"; }
                    if (verb is "next" or "prev" or "previous" or "seek" or "toggle")
                    {
                        Timeline cur = engine.Read(s => s.Timelines.FirstOrDefault(x => x.Id == engine.Timeline.TimelineId) ?? s.Timelines.FirstOrDefault());
                        if (cur == null) return "timeline not found";
                        engine.WithTimeline((p, _) =>
                        {
                            if (verb == "next") p.Next(cur);
                            else if (verb == "seek") { if (p.TimelineId != cur.Id) p.Play(cur); p.Seek(Number(a1) * 1000); }
                            else if (verb == "toggle") { if (p.Playing) p.Pause(); else p.Play(cur); }
                            else p.Previous(cur);
                        });
                        return "ok";
                    }
                    Timeline t = engine.Read(s => a1 == null ? s.Timelines.FirstOrDefault(x => x.Id == engine.Timeline.TimelineId) ?? s.Timelines.FirstOrDefault()
                                                              : Find(s.Timelines, a1, x => x.Name));
                    if (t == null) return "timeline not found";
                    engine.WithTimeline((p, _) => p.Play(t));
                    return "ok";
                }
                default:
                    return "unknown command: " + command;
            }
        }
        catch (Exception ex) { return "error: " + ex.Message; }
    }

    static void Live(LightEngine engine, Action<Storage.ShowFile> change) =>
        engine.Change(s => { change(s); s.State.ActiveScene = null; }, fade: Fade(engine.Show.State.FadeMs), cut: true);

    static IEnumerable<string> Targets(Storage.ShowFile s, string group)
    {
        if (group == null) return s.Patch.Select(p => p.Id).ToList();
        FixtureGroup g = Find(s.Groups, group, x => x.Name);
        return g?.FixtureIds ?? (IEnumerable<string>)Array.Empty<string>();
    }

    /// <summary>By name (case-insensitive, spaces/underscores ignored) or by 1-based position.</summary>
    static T Find<T>(IList<T> list, string key, Func<T, string> name) where T : class
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string k = Norm(key);
        T byName = list.FirstOrDefault(x => Norm(name(x)) == k);
        if (byName != null) return byName;
        return WholeNumber(key, out int i) && i >= 1 && i <= list.Count ? list[i - 1] : null;
    }

    static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    static int FindEffect(string key)
    {
        if (key == null) return -1;
        int i = EffectLibrary.IndexOf(key.ToLowerInvariant());
        if (i >= 0) return i;
        string k = Norm(key);
        for (int j = 0; j < EffectLibrary.All.Count; j++)
            if (Norm(EffectLibrary.All[j].Name) == k || Norm(EffectLibrary.All[j].Key) == k) return j;
        return WholeNumber(key, out int n) && n >= 1 && n <= EffectLibrary.All.Count ? n - 1 : -1;
    }

    /// <summary>Palette key ("red", "warm-white"), Dutch name ("rood"), or hex ("#FF8800" / "FF8800").</summary>
    public static ColorRef ParseColor(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string k = s.Trim().ToLowerInvariant();
        if (Palette.ByKey.ContainsKey(k)) return ColorRef.Palette(k);
        ColorDef byName = Palette.Fan.Concat(Palette.Specials).FirstOrDefault(c => Norm(c.Name) == Norm(k) || Norm(c.Key) == Norm(k));
        if (byName != null) return ColorRef.Palette(byName.Key);
        string hex = k.TrimStart('#');
        if (hex.Length == 6 && hex.All(Uri.IsHexDigit)) return ColorRef.Custom(ColorUtil.ParseHex(hex, Color.White));
        return null;
    }

    static bool Bool(string s) => s?.ToLowerInvariant() is "1" or "on" or "true" or "aan" or "yes" or "1.0";

    static double Number(string s) =>
        double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    /// <summary>Splits on spaces and slashes; quotes keep spaces ("scene \"Rood licht\"").</summary>
    public static List<string> Split(string line)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(line)) return parts;
        var cur = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char ch in line.Trim())
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (!quoted && (ch == ' ' || ch == '/'))
            {
                if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); }
                continue;
            }
            cur.Append(ch);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }
}
