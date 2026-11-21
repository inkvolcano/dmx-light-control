using DmxLicht.Core;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class StrobeAndTestTests
{
    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    static (Rig, ShowState) Lit(string type, string mode)
    {
        Rig rig = Rig.Build(new[] { new PatchedFixture { Id = "a", TypeId = type, Mode = mode, Address = 1 } }, Lib);
        var s = new ShowState();
        s.Look("a").On = true;
        s.Look("a").Color = ColorRef.Palette("red");
        return (rig, s);
    }

    [Fact]
    public void LampStrobeChannelFollowsSpeedAndIsOffOtherwise()
    {
        var (rig, s) = Lit("ledpar-18x7-rgbwauv", "10ch");
        Assert.Equal(0, FrameBuilder.Build(rig, s, 0)[8]);
        s.StrobeActive = true;
        s.StrobeSpeed = 100;
        Assert.Equal(255, FrameBuilder.Build(rig, s, 0)[8]);
        s.StrobeSpeed = 1;
        Assert.Equal(1, FrameBuilder.Build(rig, s, 0)[8]);
        Assert.Equal(255, FrameBuilder.Build(rig, s, 123)[1]);   // dimmer stays up, the lamp flashes itself
    }

    [Fact]
    public void TourLedStrobeStartsAboveItsDeadZone()
    {
        var (rig, s) = Lit("expolite-tourled-42cm", "TOUR 10ch");
        s.StrobeActive = true; s.StrobeSpeed = 1;
        Assert.Equal(11, FrameBuilder.Build(rig, s, 0)[6]);       // 0-10 = no function
    }

    [Fact]
    public void LampsWithoutStrobeChannelGetASoftwareStrobe()
    {
        var (rig, s) = Lit("generic-rgb", "3ch");
        s.StrobeActive = true; s.StrobeSpeed = 1;                 // 1 Hz, 25 % on
        Assert.Equal(255, FrameBuilder.Build(rig, s, 100)[1]);
        Assert.Equal(0, FrameBuilder.Build(rig, s, 600)[1]);
    }

    [Fact]
    public void TestChannelsOverrideAndWalkLightsOneFixtureAtATime()
    {
        var show = new ShowFile
        {
            Patch =
            {
                new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 },
                new PatchedFixture { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 4 },
            },
        };
        show.State.Look("a").On = true;
        show.State.Look("a").Color = ColorRef.Palette("blue");
        var output = new CaptureOutput();
        using var engine = new LightEngine(show, output, null);
        engine.Start();

        engine.SetTest(new Dictionary<int, byte> { [10] = 77 }, exclusive: true);
        Wait(() => output.Last[10] == 77 && output.Last[3] == 0, "exclusive test channel");
        engine.SetTest(new Dictionary<int, byte> { [10] = 77 }, exclusive: false);
        Wait(() => output.Last[10] == 77 && output.Last[3] == 255, "overlay on the normal look");

        engine.ClearTest();
        engine.SetWalk(300);
        Wait(() => output.Last[1] == 255 && output.Last[4] == 0, "walk: first fixture white");
        Wait(() => output.Last[1] == 0 && output.Last[4] == 255, "walk: second fixture white");
        engine.SetWalk(0);
        Wait(() => output.Last[3] == 255 && output.Last[1] == 0, "back to the normal look");
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
