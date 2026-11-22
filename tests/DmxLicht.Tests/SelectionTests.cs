using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;
using Xunit;

namespace DmxLicht.Tests;

/// <summary>Colours and scenes for the lamps chosen on the stage, while an effect keeps running on the others.</summary>
public class SelectionTests
{
    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    static (Rig rig, ShowState s) Three()
    {
        var patch = Enumerable.Range(0, 3).Select(i => new PatchedFixture { Id = "l" + i, TypeId = "generic-rgb", Mode = "3ch", Address = 1 + i * 3 }).ToList();
        Rig rig = Rig.Build(patch, Lib);
        var s = new ShowState { Mode = OutputMode.Effect };
        // "all lamps colour A" in green, so the effect is easy to see
        s.Effect = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("all-ab"), ColorA = System.Drawing.Color.Lime, ColorB = System.Drawing.Color.Lime };
        return (rig, s);
    }

    static byte[] Lamp(byte[] f, int i) => f[(1 + i * 3)..(4 + i * 3)];

    [Fact]
    public void AColourForOneLampLeavesTheEffectOnTheOthers()
    {
        var (rig, s) = Three();
        s.Looks["l1"] = new FixtureLook { On = true, Color = ColorRef.Palette("red") };
        Selection.Hold(s, new[] { "l1" });
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 0, 0 }, Lamp(f, 1));     // the chosen lamp: its own colour
        Assert.Equal(new byte[] { 0, 255, 0 }, Lamp(f, 0));     // the others: still the effect
        Assert.Equal(new byte[] { 0, 255, 0 }, Lamp(f, 2));
    }

    [Fact]
    public void NoEffectRunningMeansNothingIsHeld()
    {
        var (_, s) = Three();
        s.Mode = OutputMode.Static;
        Selection.Hold(s, new[] { "l1" });
        Assert.Empty(s.Held);
    }

    [Fact]
    public void AnEffectSceneRunsOnTheChosenLampsOnly()
    {
        var (rig, s) = Three();
        s.Mode = OutputMode.Static;
        foreach (string id in new[] { "l0", "l1", "l2" }) s.Looks[id] = new FixtureLook { On = true, Color = ColorRef.Palette("blue") };
        var scene = new Scene { Id = "sc", Name = "Rood effect" };
        scene.State.Mode = OutputMode.Effect;
        scene.State.Effect = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("all-ab"), ColorA = System.Drawing.Color.Red, ColorB = System.Drawing.Color.Red };

        Selection.ApplyScene(scene, s, new[] { "l2" });
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 0, 0, 255 }, Lamp(f, 0));     // untouched
        Assert.Equal(new byte[] { 255, 0, 0 }, Lamp(f, 2));     // the scene's effect
        Assert.Equal(OutputMode.Static, s.Mode);

        // a colour for that lamp takes it out of the scene effect again; with no lamps left the effect stops
        s.Looks["l2"] = new FixtureLook { On = true, Color = ColorRef.Palette("white") };
        Selection.Hold(s, new[] { "l2" });
        Assert.DoesNotContain(s.Layers, l => l.FixtureIds != null);
    }

    [Fact]
    public void AStaticSceneForSomeLampsKeepsTheEffectOnTheRest()
    {
        var (rig, s) = Three();
        var scene = new Scene { Id = "w" };
        scene.State.Mode = OutputMode.Static;
        scene.State.Looks["l0"] = new FixtureLook { On = true, Color = ColorRef.Palette("white") };
        Selection.ApplyScene(scene, s, new[] { "l0" });
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 255, 255 }, Lamp(f, 0));
        Assert.Equal(new byte[] { 0, 255, 0 }, Lamp(f, 1));
    }

    [Fact]
    public void ClearGivesEveryLampTheNewLook()
    {
        var (_, s) = Three();
        Selection.Hold(s, new[] { "l1" });
        s.Layers.Add(new EffectLayer { GroupId = Selection.ScenePrefix + "x", FixtureIds = new() { "l0" } });
        s.Layers.Add(new EffectLayer { GroupId = "*", Source = "flash" });
        Selection.Clear(s);
        Assert.Empty(s.Held);
        Assert.Single(s.Layers);                                  // a flash is not the user's: it stays
    }

    [Fact]
    public void AScenesKeepsItsHeldLamps()
    {
        var (_, s) = Three();
        Selection.Hold(s, new[] { "l1" });
        SceneState captured = SceneOps.Capture(s);
        var other = new ShowState();
        SceneOps.Apply(captured, other);
        Assert.Equal(new[] { "l1" }, other.Held);
    }
    [Fact]
    public void AnEffectOnTheChosenLampsRunsOverFixedColours()
    {
        var (rig, s) = Three();
        s.Mode = OutputMode.Static;
        foreach (string id in new[] { "l0", "l1", "l2" }) s.Looks[id] = new FixtureLook { On = true, Color = ColorRef.Palette("blue") };
        var red = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("all-ab"), ColorA = System.Drawing.Color.Red, ColorB = System.Drawing.Color.Red };
        Selection.StartEffect(s, new[] { "l0", "l1" }, red);
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 0, 0 }, Lamp(f, 0));
        Assert.Equal(new byte[] { 255, 0, 0 }, Lamp(f, 1));
        Assert.Equal(new byte[] { 0, 0, 255 }, Lamp(f, 2));

        // the same lamps again: the same effect changes (no second one)
        Selection.StartEffect(s, new[] { "l1", "l0" }, red);
        Assert.Single(s.Layers);
        // one of them gets its own effect: it leaves the first one
        Selection.StartEffect(s, new[] { "l1" }, red);
        Assert.Equal(2, s.Layers.Count);
        Assert.Equal(new[] { "l0" }, s.Layers[0].FixtureIds);
        // stopping on l0 ends the first effect (no lamps left)
        Selection.StopEffect(s, new[] { "l0" });
        Assert.Single(s.Layers);
        Assert.StartsWith(Selection.SelectionPrefix, s.Layers[0].GroupId);
    }
    [Fact]
    public void AnExampleOnSomeLampsKeepsTheOthers()
    {
        var (rig, s) = Three();
        s.Mode = OutputMode.Static;
        foreach (string id in new[] { "l0", "l1", "l2" }) s.Looks[id] = new FixtureLook { On = true, Color = ColorRef.Palette("blue") };
        SceneTemplate red = SceneTemplates.Find("red");
        SceneState st = Selection.TemplateOn(red.Build(new[] { "l1" }), s, new[] { "l1" });
        var other = new ShowState();
        SceneOps.Apply(st, other);
        byte[] f = FrameBuilder.Build(rig, other, 0);
        Assert.Equal(new byte[] { 0, 0, 255 }, Lamp(f, 0));
        Assert.Equal(new byte[] { 255, 0, 0 }, Lamp(f, 1));
        Assert.Equal(new byte[] { 0, 0, 255 }, Lamp(f, 2));
    }
}
