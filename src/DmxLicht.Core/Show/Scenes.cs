using System.Text.Json;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Storage;

namespace DmxLicht.Core.Show;

/// <summary>A named set of fixtures ("front", "back").</summary>
public sealed class FixtureGroup
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public string Name { get; set; }
    public List<string> FixtureIds { get; set; } = new();
}

/// <summary>An effect running on one group, on top of whatever the rest does.</summary>
public sealed class EffectLayer
{
    /// <summary>Group id, or "*" for all fixtures, or "scene:" + a scene id for a scene on a selection.</summary>
    public string GroupId { get; set; }
    /// <summary>When set: these fixtures instead of a group (a scene recalled on some lamps only).</summary>
    public List<string> FixtureIds { get; set; }
    /// <summary>Who started it: null = the user, "timeline" = the timeline player.</summary>
    public string Source { get; set; }
    public EffectSettings Effect { get; set; } = new();
}

/// <summary>Everything a scene restores: the look of the lamps, not the master or the patch.</summary>
public sealed class SceneState
{
    public OutputMode Mode { get; set; }
    public Dictionary<string, FixtureLook> Looks { get; set; } = new();
    public Dictionary<string, byte[]> Faders { get; set; } = new();
    public EffectSettings Effect { get; set; } = new();
    public List<EffectLayer> Layers { get; set; } = new();
    public List<string> Held { get; set; } = new();
}

/// <summary>A saved look with a button colour and a fade time.</summary>
public sealed class Scene
{
    public string Id { get; set; } = PatchedFixture.NewId();
    public string Name { get; set; }
    /// <summary>Button colour "#RRGGBB" (defaults to the scene's main colour).</summary>
    public string Color { get; set; }
    public int FadeMs { get; set; } = 1000;
    public SceneState State { get; set; } = new();
    /// <summary>
    /// The lamps the scene is for (saved with only some lamps chosen); null = every lamp. Recalling it changes
    /// only these lamps, and on the timeline it lands on their rows.
    /// </summary>
    public List<string> Lamps { get; set; }
}

public static class SceneOps
{
    /// <summary>Deep copy via JSON (state is small; keeps the copy independent of later edits).</summary>
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json.Compact), Json.Compact);

    /// <summary>The lamps of <paramref name="patch"/> the scene is for; null = all of them.</summary>
    public static List<string> LampsOf(Scene sc, IReadOnlyCollection<string> patch)
    {
        if (sc.Lamps == null || sc.Lamps.Count == 0) return null;
        List<string> l = sc.Lamps.Where(patch.Contains).Distinct().ToList();
        return l.Count >= patch.Count ? null : l;
    }

    /// <summary>"Only these lamps" for a scene being saved: null when that is every lamp (or none is given).</summary>
    public static List<string> LampsFor(IEnumerable<string> ids, IReadOnlyCollection<string> patch)
    {
        List<string> l = (ids ?? Array.Empty<string>()).Where(patch.Contains).Distinct().ToList();
        return l.Count == 0 || l.Count >= patch.Count ? null : l;
    }

    public static SceneState Capture(ShowState s)
    {
        SceneState c = Clone(new SceneState { Mode = s.Mode, Looks = s.Looks, Faders = s.Faders, Effect = s.Effect, Layers = s.Layers, Held = s.Held });
        c.Layers?.RemoveAll(l => l.Source != null);   // a flash or timeline effect running now is not part of the scene
        return c;
    }

    public static void Apply(SceneState scene, ShowState s)
    {
        SceneState c = Clone(scene);
        s.Mode = c.Mode;
        // lamps the scene does not know (added after it was saved, or a scene from another patch) keep their
        // look: they used to lose it and go dark
        Dictionary<string, FixtureLook> looks = c.Looks ?? new();
        foreach (var kv in s.Looks ?? new()) looks.TryAdd(kv.Key, kv.Value);
        Dictionary<string, byte[]> faders = c.Faders ?? new();
        foreach (var kv in s.Faders ?? new()) faders.TryAdd(kv.Key, kv.Value);
        s.Looks = looks;
        s.Faders = faders;
        s.Effect = c.Effect ?? new();
        s.Layers = c.Layers ?? new();
        s.Held = c.Held ?? new();
    }

    /// <summary>A sensible button colour: the colour most lamps have, or effect colour A.</summary>
    public static string GuessColor(SceneState s)
    {
        if (s.Mode == OutputMode.Effect) return ColorUtil.Hex(s.Effect.ColorA);
        var on = s.Looks.Values.Where(l => l.On).Select(l => ColorUtil.Hex(l.Color.Resolve().Show)).ToList();
        return on.GroupBy(h => h).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "#3A3A44";
    }
}
