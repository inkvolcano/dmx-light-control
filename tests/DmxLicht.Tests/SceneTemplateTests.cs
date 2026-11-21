using DmxLicht.Core;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;

namespace DmxLicht.Tests;

public class SceneTemplateTests
{
    [Fact]
    public void EveryTemplateBuildsAValidSceneForAnyRig()
    {
        Assert.True(SceneTemplates.All.Count >= 30);
        Assert.Equal(SceneTemplates.All.Count, SceneTemplates.All.Select(t => t.Key).Distinct().Count());
        foreach (SceneTemplate t in SceneTemplates.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.NameEn));
            Assert.Contains(t.Category, new[] { "basic", "mood", "party", "theme" });
            foreach (int n in new[] { 0, 1, 2, 7 })
            {
                var ids = Enumerable.Range(0, n).Select(i => "f" + i).ToList();
                SceneState s = t.Build(ids);                          // effect templates throw on unknown effects
                if (s.Mode == OutputMode.Static) Assert.Equal(n, s.Looks.Count);
            }
        }
    }

    [Fact]
    public void TemplatesRenderOnTheRealLamps()
    {
        var patch = new[]
        {
            new PatchedFixture { Id = "par", TypeId = "ledpar-18x7-rgbwauv", Mode = "10ch", Address = 1 },
            new PatchedFixture { Id = "brick", TypeId = "stairville-xbrick-16x3", Mode = "13ch (4 segments)", Address = 11 },
            new PatchedFixture { Id = "tour", TypeId = "expolite-tourled-42cm", Mode = "Ar1.d 4ch", Address = 24 },
        };
        Rig rig = Rig.Build(patch, new FixtureLibrary(FixtureLibrary.BuiltIn()));
        foreach (SceneTemplate t in SceneTemplates.All)
        {
            var state = new ShowState();
            SceneOps.Apply(t.Build(patch.Select(p => p.Id).ToList()), state);
            byte[] f = FrameBuilder.Build(rig, state, 1234);
            if (t.Key == "all-off") Assert.All(f[1..28], v => Assert.Equal(0, v));
            if (t.Key == "red") Assert.Equal(255, f[2]);              // PAR 10ch: R on channel 2
        }
    }

    [Fact]
    public void AlternateAndStripesUsePatchOrder()
    {
        var ids = new List<string> { "a", "b", "c", "d", "e", "f" };
        SceneState flag = SceneTemplates.Find("nl-flag").Build(ids);
        Assert.Equal(new[] { "red", "red", "white", "white", "blue", "blue" }, ids.Select(i => flag.Looks[i].Color.Key));
        SceneState halloween = SceneTemplates.Find("halloween").Build(ids);
        Assert.NotEqual(halloween.Looks["a"].Color.Hex, halloween.Looks["b"].Color.Hex);
        Assert.Equal(halloween.Looks["a"].Color.Hex, halloween.Looks["c"].Color.Hex);
    }
}
