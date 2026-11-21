using System.Text.Json;
using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

/// <summary>The user's own chases from the chase editor.</summary>
public class ChaseTests
{
    const string R = "#FF0000", G = "#00FF00", B = "#0000FF";

    static ChaseStep Step(params string[] colors) => new() { Colors = colors.ToList() };

    /// <summary>Running light over 3 columns: step i lights column i red.</summary>
    static CustomChase Runner(int cols = 3) => new()
    {
        Columns = cols,
        StepMs = 100,
        Steps = Enumerable.Range(0, cols).Select(i => Step(Enumerable.Range(0, cols).Select(c => c == i ? R : null).ToArray())).ToList(),
    };

    static double[] Levels(CustomChase c, long ms, int lamps = 3, AudioFrame audio = default) =>
        ChaseRenderer.Render(c, false, ms, lamps, audio).Select(o => Math.Round(o.Level, 3)).ToArray();

    static int Lit(CustomChase c, long ms, int lamps = 3, AudioFrame audio = default) =>
        Array.IndexOf(Levels(c, ms, lamps, audio), 1.0);

    [Fact]
    public void StepsFollowEachOtherInTime()
    {
        CustomChase c = Runner();
        Assert.Equal(new[] { 1.0, 0, 0 }, Levels(c, 0));
        Assert.Equal(new[] { 0.0, 1, 0 }, Levels(c, 150));
        Assert.Equal(new[] { 0.0, 0, 1 }, Levels(c, 250));
        Assert.Equal(new[] { 1.0, 0, 0 }, Levels(c, 300));   // loops
        Light red = ChaseRenderer.Render(c, false, 0, 3, default)[0].Color;
        Assert.Equal((255, 0, 0), (red.R, red.G, red.B));
    }

    [Fact]
    public void OwnStepDurationAndSpeed()
    {
        CustomChase c = Runner();
        c.Steps[0].DurationMs = 1000;                         // 1000 + 100 + 100
        Assert.Equal(0, Lit(c, 900));
        Assert.Equal(1, Lit(c, 1050));
        Assert.Equal(2, Lit(c, 1150));
        Assert.Equal(0, Lit(c, 1250));
        c.SpeedPercent = 200;                                 // twice as fast
        Assert.Equal(1, Lit(c, 525));
    }

    [Fact]
    public void PatternRepeatsOverMoreLamps()
    {
        CustomChase c = Runner();
        Assert.Equal(new[] { 1.0, 0, 0, 1, 0, 0, 1 }, Levels(c, 0, 7));
    }

    [Fact]
    public void MappingsStretchMirrorAndOnce()
    {
        CustomChase c = Runner();
        c.Mapping = ChaseMapping.Stretch;
        Assert.Equal(new[] { 1.0, 1, 0, 0, 0, 0 }, Levels(c, 0, 6));
        c.Mapping = ChaseMapping.Mirror;
        Assert.Equal(new[] { 1.0, 0, 0, 0, 0, 1 }, Levels(c, 0, 6));
        Assert.Equal(new[] { 0.0, 0, 1, 1, 0, 0 }, Levels(c, 250, 6));   // meets in the middle
        c.Mapping = ChaseMapping.Once;
        Assert.Equal(new[] { 0.0, 0, 1, 0, 0, 0 }, Levels(c, 250, 6));
    }

    [Fact]
    public void RepeatOffsetMakesAWave()
    {
        CustomChase c = Runner();
        c.RepeatOffset = 1;                                   // second repeat one step behind
        Assert.Equal(new[] { 1.0, 0, 0, 0, 0, 1 }, Levels(c, 0, 6));
        c.RepeatOffset = -1;                                  // or ahead
        Assert.Equal(new[] { 1.0, 0, 0, 0, 1, 0 }, Levels(c, 0, 6));
    }

    [Fact]
    public void DirectionsReverseBounceRandom()
    {
        CustomChase c = Runner(4);
        c.Direction = ChaseDirection.Reverse;
        Assert.Equal(new[] { 3, 2, 1, 0, 3 }, Enumerable.Range(0, 5).Select(i => Lit(c, i * 100 + 50, 4)));
        c.Direction = ChaseDirection.Bounce;
        Assert.Equal(new[] { 0, 1, 2, 3, 2, 1, 0, 1 }, Enumerable.Range(0, 8).Select(i => Lit(c, i * 100 + 50, 4)));

        c.Direction = ChaseDirection.Random;
        int[] seen = Enumerable.Range(0, 400).Select(i => Lit(c, i * 100 + 50, 4)).ToArray();
        for (int cycle = 0; cycle < 100; cycle++)          // every pass plays every step once
            Assert.Equal(new[] { 0, 1, 2, 3 }, seen.Skip(cycle * 4).Take(4).OrderBy(x => x));
        for (int i = 1; i < seen.Length; i++) Assert.NotEqual(seen[i - 1], seen[i]);   // never the same twice in a row
        Assert.Contains(Enumerable.Range(0, 99), k => !seen.Skip(k * 4).Take(4).SequenceEqual(seen.Skip(k * 4 + 4).Take(4)));
    }

    [Fact]
    public void CrossfadeBlendsColourAndLevel()
    {
        var c = new CustomChase { Columns = 1, StepMs = 100, FadePercent = 50, Steps = { Step(R), Step(B) } };
        Assert.Equal(255, ChaseRenderer.Render(c, false, 40, 1, default)[0].Color.R);    // before the fade: red
        LampOutput mid = ChaseRenderer.Render(c, false, 75, 1, default)[0];             // halfway the fade
        Assert.InRange(mid.Color.R, 120, 135);
        Assert.InRange(mid.Color.B, 120, 135);
        Assert.Equal(1.0, mid.Level, 3);

        var onOff = new CustomChase { Columns = 1, StepMs = 100, FadePercent = 100, Steps = { Step(R), Step((string)null) } };
        Assert.Equal(0.5, ChaseRenderer.Render(onOff, false, 50, 1, default)[0].Level, 3); // fades out
        Assert.Equal(0.5, ChaseRenderer.Render(onOff, false, 150, 1, default)[0].Level, 3); // and back in
        Assert.Equal(255, ChaseRenderer.Render(onOff, false, 150, 1, default)[0].Color.R);  // in red, not via black

        onOff.Steps[0].FadePercent = 0;                       // own fade per step
        Assert.Equal(1.0, ChaseRenderer.Render(onOff, false, 50, 1, default)[0].Level, 3);
    }

    [Fact]
    public void StepLevelBackgroundAndSound()
    {
        CustomChase c = Runner();
        c.Steps[0].Level = 40;
        Assert.Equal(new[] { 0.4, 0, 0 }, Levels(c, 0));
        c.Background = G;
        LampOutput[] o = ChaseRenderer.Render(c, false, 150, 3, default);
        Assert.Equal((0, 255, 0), (o[0].Color.R, o[0].Color.G, o[0].Color.B));  // empty cell = background
        Assert.Equal(1.0, o[0].Level);
        c.Background = null;
        c.AudioLevel = true;
        Assert.Equal(0.1, Levels(c, 150, 3, new AudioFrame(0, 0, 0, 0, 0, false))[1], 3);
        Assert.Equal(1.0, Levels(c, 150, 3, new AudioFrame(1, 0, 0, 0, 0, false))[1], 3);
    }

    [Fact]
    public void BeatSyncFollowsTheMusicalClock()
    {
        CustomChase c = Runner();
        c.SyncToBeat = true;
        Assert.Equal(0, Lit(c, 99999, 3, new AudioFrame(0, 0, 0, 0, 0.5, false)));
        Assert.Equal(1, Lit(c, 0, 3, new AudioFrame(0, 0, 0, 0, 1.2, false)));
        Assert.Equal(0, Lit(c, 0, 3, new AudioFrame(0, 0, 0, 0, 3.0, false)));
        c.BeatsPerStep = 2;
        Assert.Equal(0, Lit(c, 0, 3, new AudioFrame(0, 0, 0, 0, 1.9, false)));
        Assert.Equal(1, Lit(c, 0, 3, new AudioFrame(0, 0, 0, 0, 2.1, false)));
        c.BeatsPerStep = 0.5;
        Assert.Equal(2, Lit(c, 0, 3, new AudioFrame(0, 0, 0, 0, 1.1, false)));
    }

    [Fact]
    public void OddChasesStaySafe()
    {
        var r = new Random(3);
        var chases = new[]
        {
            new CustomChase(),                                                   // no steps
            new CustomChase { Columns = 0, StepMs = 0, Steps = { new ChaseStep() } },
            new CustomChase { Columns = 5, StepMs = 1, FadePercent = 300, SpeedPercent = 0, RepeatOffset = -7, Steps = { Step("bad", "", null, "#123") } },
        };
        foreach (CustomChase c in chases)
            foreach (ChaseDirection d in Enum.GetValues<ChaseDirection>())
                foreach (ChaseMapping m in Enum.GetValues<ChaseMapping>())
                {
                    c.Direction = d; c.Mapping = m;
                    foreach (bool beat in new[] { false, true })
                    {
                        c.SyncToBeat = beat;
                        for (int t = 0; t < 20; t++)
                            foreach (LampOutput o in ChaseRenderer.Render(c, true, r.Next(), r.Next(1, 30), new AudioFrame(r.NextDouble(), 0, 0, 0, r.NextDouble() * 50, false)))
                                Assert.InRange(o.Level, 0.0, 1.0);
                    }
                }
    }

    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    [Fact]
    public void RunsAsMainEffectAndAsGroupLayer()
    {
        var patch = Enumerable.Range(0, 3).Select(i => new PatchedFixture { Id = "l" + i, TypeId = "generic-rgb", Mode = "3ch", Address = 1 + i * 3 }).ToList();
        Rig rig = Rig.Build(patch, Lib, new[] { new FixtureGroup { Id = "g", FixtureIds = { "l2" } } });
        CustomChase c = Runner();
        c.Steps[0].Colors = new() { R, G, B };
        var s = new ShowState { Mode = OutputMode.Effect };
        s.Chases.Add(c);
        s.Effect.ChaseId = c.Id;
        byte[] f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255 }, f[1..10]);

        s.Effect.ChaseId = "gone";                            // deleted chase: falls back to the library effect
        FrameBuilder.Build(rig, s, 0);

        s.Effect.ChaseId = null;
        s.Layers.Add(new EffectLayer { GroupId = "g", Effect = new EffectSettings { ChaseId = c.Id } });
        f = FrameBuilder.Build(rig, s, 150);                  // group has one lamp -> column 0, step 1 = off
        Assert.Equal(new byte[] { 0, 0, 0 }, f[7..10]);
        f = FrameBuilder.Build(rig, s, 0);
        Assert.Equal(new byte[] { 255, 0, 0 }, f[7..10]);
    }

    [Fact]
    public void SavedInTheShowFile()
    {
        var s = new ShowState();
        CustomChase c = Runner();
        c.Direction = ChaseDirection.Bounce;
        c.Mapping = ChaseMapping.Mirror;
        c.Steps[1].DurationMs = 777;
        s.Chases.Add(c);
        string json = JsonSerializer.Serialize(s, Json.Options);
        Assert.Contains("\"bounce\"", json);
        ShowState back = JsonSerializer.Deserialize<ShowState>(json, Json.Options);
        CustomChase b = Assert.Single(back.Chases);
        Assert.Equal((c.Id, ChaseDirection.Bounce, ChaseMapping.Mirror, 777), (b.Id, b.Direction, b.Mapping, b.Steps[1].DurationMs));
        Assert.Equal(c.Steps[0].Colors, b.Steps[0].Colors);
    }

    [Fact]
    public void CommandStartsAChaseByName()
    {
        var show = new ShowFile { Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } } };
        var output = new CaptureOutput();
        using var e = new LightEngine(show, output, null);
        CustomChase c = Runner();
        c.Name = "Mijn looplicht";
        e.Change(s => s.State.Chases.Add(c));
        Assert.Equal("ok", Commands.Run(e, "effect \"mijn looplicht\""));
        Assert.Equal(c.Id, e.Read(s => s.State.Effect.ChaseId));
        Assert.Equal("ok", Commands.Run(e, "effect chase-right"));
        Assert.Null(e.Read(s => s.State.Effect.ChaseId));
    }
}
