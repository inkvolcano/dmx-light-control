using System.Drawing;
using System.Text.Json;
using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Output;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

/// <summary>Output that just keeps the latest frame.</summary>
sealed class CaptureOutput : IDmxOutput
{
    public volatile byte[] Last = new byte[513];
    public string Name => "test";
    public bool Connected => true;
    public string Error => null;
    public string Status => "";
    public OutputDiagnostics Diagnostics => null;
    public void Start() { }
    public void Stop() { }
    public void Reconnect() { }
    public void SetFrame(byte[] channels, int count) => Last = (byte[])channels.Clone();
}

public class SceneTests
{
    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    static List<PatchedFixture> ThreeRgb() => new()
    {
        new() { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 },
        new() { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 4 },
        new() { Id = "c", TypeId = "generic-rgb", Mode = "3ch", Address = 7 },
    };

    [Fact]
    public void GroupEffectRepaintsOnlyItsGroup()
    {
        var groups = new[] { new FixtureGroup { Id = "back", FixtureIds = { "c", "b" } } };
        Rig rig = Rig.Build(ThreeRgb(), Lib, groups);
        var s = new ShowState();
        foreach (string id in new[] { "a", "b", "c" }) { var l = s.Look(id); l.On = true; l.Color = ColorRef.Palette("red"); }
        s.Layers.Add(new EffectLayer
        {
            GroupId = "back",
            Effect = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("chase-right"), StepMs = 1000, ColorA = Color.FromArgb(0, 0, 255) },
        });
        byte[] f = FrameBuilder.Build(rig, s, 500);  // step 0: first lamp of the group (b, in rig order) lit
        Assert.Equal(new byte[] { 255, 0, 0 }, f[1..4]);   // a: untouched red
        Assert.Equal(new byte[] { 0, 0, 255 }, f[4..7]);   // b: chase head
        Assert.Equal(new byte[] { 0, 0, 0 }, f[7..10]);    // c: chase off
    }

    [Fact]
    public void SceneCaptureAndRecallRoundTrip()
    {
        var s = new ShowState { Mode = OutputMode.Effect };
        s.Look("a").On = true;
        s.Look("a").Color = ColorRef.Custom(Color.FromArgb(1, 2, 3));
        s.Effect.EffectIndex = 26;
        s.Layers.Add(new EffectLayer { GroupId = "g", Effect = new EffectSettings { EffectIndex = 5 } });
        SceneState scene = SceneOps.Capture(s);
        string before = JsonSerializer.Serialize(SceneOps.Capture(s), Json.Compact);

        s.Mode = OutputMode.Static; s.Looks.Clear(); s.Layers.Clear(); s.Effect.EffectIndex = 0;
        SceneOps.Apply(scene, s);
        Assert.Equal(before, JsonSerializer.Serialize(SceneOps.Capture(s), Json.Compact));

        s.Look("a").On = false;                   // editing the live state must not change the scene
        Assert.True(scene.Looks["a"].On);
    }

    [Fact]
    public void MixBlendsChannelsWithEasing()
    {
        var a = new byte[513]; var b = new byte[513];
        b[1] = 255; a[2] = 200;
        byte[] mid = LightEngine.Mix(a, b, 0.5);
        Assert.Equal(128, mid[1]);
        Assert.Equal(100, mid[2]);
        Assert.Equal(0, LightEngine.Mix(a, b, 0)[1]);
        Assert.Equal(255, LightEngine.Mix(a, b, 1)[1]);
    }

    [Fact]
    public void EngineFadesBetweenLooks()
    {
        var show = new ShowFile { Patch = ThreeRgb() };
        var output = new CaptureOutput();
        using var engine = new LightEngine(show, output, null);
        engine.Change(s => { var l = s.State.Look("a"); l.On = true; l.Color = ColorRef.Palette("red"); });
        engine.Start();
        Wait(() => output.Last[1] == 255, "red");

        engine.Change(s => s.State.Look("a").Color = ColorRef.Palette("blue"), fade: 600);
        Wait(() => output.Last[1] is > 30 and < 225 && output.Last[3] is > 30 and < 225, "half way: red and blue mixed");
        Wait(() => output.Last[1] == 0 && output.Last[3] == 255, "blue at the end");
    }

    static void Wait(Func<bool> cond, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond())
        {
            if (sw.ElapsedMilliseconds > 3000) Assert.Fail("timed out: " + what);
            Thread.Sleep(5);
        }
    }
}
