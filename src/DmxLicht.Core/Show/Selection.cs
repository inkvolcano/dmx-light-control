using DmxLicht.Core.Effects;

namespace DmxLicht.Core.Show;

/// <summary>
/// Colours and scenes for some lamps only (the lamps chosen on the stage), while an effect keeps running on
/// the others. Two tools in the show state:
/// <list type="bullet">
/// <item><see cref="ShowState.Held"/>: lamps that show their own fixed colour while an effect runs;</item>
/// <item>a layer with <see cref="EffectLayer.FixtureIds"/>: a scene's effect running on just those lamps
/// (its id is "scene:" + the scene id).</item>
/// </list>
/// </summary>
public static class Selection
{
    public const string ScenePrefix = "scene:";
    /// <summary>Layer ids of effects started on a selection ("sel:" + a short id).</summary>
    public const string SelectionPrefix = "sel:";

    /// <summary>Is any effect painting lamps now (the main effect, or a group/selection effect of the user)?</summary>
    static bool EffectsPaint(ShowState s) => s.Mode == OutputMode.Effect || s.Layers.Any(l => l.Source == null);

    /// <summary>
    /// These lamps were given their own colour: they leave any selection effect, and while an effect runs
    /// they keep showing that colour instead of the effect.
    /// </summary>
    public static void Hold(ShowState s, IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        Release(s, set);
        if (!EffectsPaint(s)) return;
        foreach (string id in set)
            if (!s.Held.Contains(id)) s.Held.Add(id);
    }

    /// <summary>Recalls a scene on these lamps only; the other lamps keep what they do.</summary>
    public static void ApplyScene(Scene scene, ShowState s, IReadOnlyCollection<string> ids)
    {
        SceneState st = SceneOps.Clone(scene.State);
        foreach (string id in ids)
        {
            if (st.Looks != null && st.Looks.TryGetValue(id, out FixtureLook look)) s.Looks[id] = look;
            if (st.Faders != null && st.Faders.TryGetValue(id, out byte[] f)) s.Faders[id] = f;
        }
        if (st.Mode == OutputMode.Effect)
        {
            var set = ids.ToHashSet();
            Release(s, set);
            s.Held.RemoveAll(set.Contains);
            string gid = ScenePrefix + scene.Id;
            EffectLayer layer = s.Layers.FirstOrDefault(l => l.GroupId == gid && l.Source == null);
            if (layer == null) s.Layers.Add(layer = new EffectLayer { GroupId = gid, FixtureIds = new() });
            layer.Effect = st.Effect ?? new();
            foreach (string id in ids)
                if (!layer.FixtureIds.Contains(id)) layer.FixtureIds.Add(id);
        }
        else Hold(s, ids);
        s.ActiveScene = null;
    }

    /// <summary>
    /// Starts (or changes) an effect on these lamps only. The same lamps again change that effect; other lamps
    /// keep what they do. Lamps that were in another selection effect leave it.
    /// </summary>
    public static void StartEffect(ShowState s, IReadOnlyCollection<string> ids, EffectSettings effect)
    {
        var set = ids.ToHashSet();
        s.Held.RemoveAll(set.Contains);
        EffectLayer same = s.Layers.FirstOrDefault(l => l.Source == null && l.FixtureIds != null && l.FixtureIds.ToHashSet().SetEquals(set));
        if (same != null) { same.Effect = effect; s.ActiveScene = null; return; }
        Release(s, set);
        s.Layers.Add(new EffectLayer { GroupId = SelectionPrefix + Guid.NewGuid().ToString("N")[..8], FixtureIds = ids.ToList(), Effect = effect });
        s.ActiveScene = null;
    }

    /// <summary>Stops the effect on these lamps: they show what runs underneath again.</summary>
    public static void StopEffect(ShowState s, IEnumerable<string> ids) => Release(s, ids.ToHashSet());

    /// <summary>
    /// A ready-made scene (built for <paramref name="ids"/> only) as a scene of the whole show: those lamps get
    /// it, every other lamp keeps what it does now. Effect scenes run on those lamps only.
    /// </summary>
    public static SceneState TemplateOn(SceneState built, ShowState now, IReadOnlyCollection<string> ids)
    {
        var tmp = new ShowState();
        SceneOps.Apply(SceneOps.Capture(now), tmp);
        SceneState st = SceneOps.Clone(built);
        foreach (string id in ids)
        {
            if (st.Looks != null && st.Looks.TryGetValue(id, out FixtureLook look)) tmp.Looks[id] = look;
            if (st.Faders != null && st.Faders.TryGetValue(id, out byte[] f)) tmp.Faders[id] = f;
        }
        if (st.Mode == OutputMode.Effect) StartEffect(tmp, ids, st.Effect ?? new());
        else Hold(tmp, ids);
        return SceneOps.Capture(tmp);
    }

    /// <summary>A new look for every lamp: no lamps held, no selection effects.</summary>
    public static void Clear(ShowState s)
    {
        s.Held.Clear();
        s.Layers.RemoveAll(l => l.Source == null && l.FixtureIds != null);
    }

    /// <summary>A group effect starts on these lamps: they show it, not a held colour.</summary>
    public static void Unhold(ShowState s, IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        s.Held.RemoveAll(set.Contains);
    }

    /// <summary>Takes the lamps out of every selection effect; a selection effect left with no lamps stops.</summary>
    static void Release(ShowState s, HashSet<string> ids)
    {
        foreach (EffectLayer l in s.Layers)
            if (l.Source == null && l.FixtureIds != null) l.FixtureIds.RemoveAll(ids.Contains);
        s.Layers.RemoveAll(l => l.Source == null && l.FixtureIds is { Count: 0 });
    }
}
