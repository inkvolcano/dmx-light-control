using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class ControlTests
{
    static (LightEngine engine, CaptureOutput output) Engine()
    {
        var show = new ShowFile
        {
            Patch =
            {
                new PatchedFixture { Id = "a", Name = "Links", TypeId = "generic-rgb", Mode = "3ch", Address = 1 },
                new PatchedFixture { Id = "b", Name = "Rechts", TypeId = "generic-rgb", Mode = "3ch", Address = 4 },
            },
            Groups = { new FixtureGroup { Id = "g", Name = "Voor", FixtureIds = { "b" } } },
        };
        var rood = new Scene { Id = "s1", Name = "Rood licht", FadeMs = 0 };
        rood.State.Looks["a"] = new FixtureLook { On = true, Color = ColorRef.Palette("red") };
        show.Scenes.Add(rood);
        var output = new CaptureOutput();
        return (new LightEngine(show, output, null), output);
    }

    [Fact]
    public void CommandsChangeTheShow()
    {
        var (e, _) = Engine();
        using (e)
        {
            Assert.Equal("ok", Commands.Run(e, "scene \"rood licht\""));
            Assert.Equal("s1", e.Show.State.ActiveScene);
            Assert.Equal("ok", Commands.Run(e, "scene 1"));
            Assert.StartsWith("scene not found", Commands.Run(e, "scene paars"));

            Assert.Equal("ok", Commands.Run(e, "color blauw Voor"));     // Dutch colour name, group by name
            Assert.True(e.Show.State.Looks["b"].On);
            Assert.Equal("blue", e.Show.State.Looks["b"].Color.Key);
            Assert.False(e.Show.State.Looks["a"].Color.Key == "blue");

            Assert.Equal("ok", Commands.Run(e, "master 0.5"));
            Assert.Equal(50, e.Show.State.BrightnessPercent);
            Assert.Equal("ok", Commands.Run(e, "master/80"));             // slashes work like spaces (URLs)
            Assert.Equal(80, e.Show.State.BrightnessPercent);

            Assert.Equal("ok", Commands.Run(e, "blackout"));
            Assert.True(e.Show.State.Blackout);
            Assert.Equal("ok", Commands.Run(e, "blackout off"));
            Assert.False(e.Show.State.Blackout);

            Assert.Equal("ok", Commands.Run(e, "effect fire Voor"));
            Assert.Contains(e.Show.State.Layers, l => l.GroupId == "g" && l.Effect.EffectIndex == 26);
            Assert.Equal("ok", Commands.Run(e, "stop"));
            Assert.Empty(e.Show.State.Layers);

            Assert.StartsWith("unknown command", Commands.Run(e, "dance"));
        }
    }

    [Fact]
    public void ColoursByKeyNameOrHex()
    {
        Assert.Equal("warm-white", Commands.ParseColor("warm-white").Key);
        Assert.Equal("red", Commands.ParseColor("Rood").Key);
        Assert.Equal("#FF8800", Commands.ParseColor("ff8800").Hex);
        Assert.Null(Commands.ParseColor("not-a-colour"));
    }

    [Fact]
    public void FlashShowsAColourThenGoesAway()
    {
        var (e, output) = Engine();
        using (e)
        {
            e.Start();
            Commands.Run(e, "flash green 0.6 solid");
            Wait(() => output.Last[2] == 255 && output.Last[5] == 255, "green on both fixtures");
            Wait(() => output.Last[2] == 0 && output.Last[5] == 0, "back to dark");
            Assert.Empty(e.Show.State.Layers);
        }
    }

    [Fact]
    public void OscMessagesBecomeCommands()
    {
        OscMessage m = Assert.Single(OscServer.Parse(OscServer.Build("/dmxlicht/scene", "Rood licht")));
        Assert.Equal("scene \"Rood licht\"", OscServer.ToCommand(m));
        Assert.Equal("master 0.75", OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build("/master", 0.75f)))));
        Assert.Equal("scene 3", OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build("/scene/3", 1f)))));
        Assert.Null(OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build("/scene/3", 0f)))));  // release
        Assert.Equal("blackout 0", OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build("/blackout", 0)))));
        // a fader at full sends 1.0: that is 100 %, not 1 %
        string full = OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build("/master", 1f))));
        Assert.Equal("master 1.0", full);
        var (e, _) = Engine();
        using (e)
        {
            Assert.Equal("ok", Commands.Run(e, full));
            Assert.Equal(100, e.Read(s => s.State.BrightnessPercent));
            Assert.Equal("ok", Commands.Run(e, "master 1"));       // MIDI/URL: whole percent
            Assert.Equal(1, e.Read(s => s.State.BrightnessPercent));
            // OSC sends numbers as floats: "scene 1.0" is scene 1
            Assert.Equal("ok", OscCmd(e, "/dmxlicht/scene", 1f, extra: 1f));
        }
    }

    static string OscCmd(LightEngine e, string address, float value, float extra)
    {
        string cmd = OscServer.ToCommand(Assert.Single(OscServer.Parse(OscServer.Build(address, value, extra))));
        return Commands.Run(e, cmd);
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
