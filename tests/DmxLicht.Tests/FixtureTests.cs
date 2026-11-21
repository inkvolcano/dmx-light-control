using System.Drawing;
using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;

namespace DmxLicht.Tests;

/// <summary>The built-in lamps from the manuals, rendered in their different modes.</summary>
public class FixtureTests
{
    static readonly FixtureLibrary Lib = new(FixtureLibrary.BuiltIn());

    static (Rig rig, ShowState s) One(string typeId, string mode, int address = 1)
    {
        var p = new PatchedFixture { Id = "a", TypeId = typeId, Mode = mode, Address = address };
        return (Rig.Build(new[] { p }, Lib), new ShowState());
    }

    static byte[] StaticColor(Rig rig, ShowState s, string paletteKey, int bright = 100)
    {
        s.BrightnessPercent = bright;
        foreach (RigFixture f in rig.Fixtures) { var l = s.Look(f.Patch.Id); l.On = true; l.Color = ColorRef.Palette(paletteKey); }
        return FrameBuilder.Build(rig, s, 0);
    }

    [Fact]
    public void SgmP2ModesFromTheDmxChart()
    {
        FixtureType t = Lib.Find("sgm-p2");
        Assert.Equal(new[] { 6, 3, 4, 6, 9, 10, 10, 12, 15, 22 }, t.Modes.Select(m => m.Footprint));

        // 6ch CTC: shutter open, dimmer, no colour correction, R G B
        var (rig, s) = One("sgm-p2", "6ch CTC (calibrated)");
        Assert.Equal(new byte[] { 250, 255, 2, 255, 0, 0 }, StaticColor(rig, s, "red")[1..7]);
        // the lamp's own strobe: shutter 151 (slow) .. 16 (fast)
        s.StrobeActive = true; s.StrobeSpeed = 100;
        Assert.Equal(16, FrameBuilder.Build(rig, s, 0)[1]);
        s.StrobeSpeed = 1;
        Assert.Equal(151, FrameBuilder.Build(rig, s, 0)[1]);

        // off: the shutter stays open (250), so fading on/off never sweeps through the strobe range
        s.StrobeActive = false;
        foreach (RigFixture f2 in rig.Fixtures) s.Look(f2.Patch.Id).On = false;
        Assert.Equal(new byte[] { 250, 0, 2, 0, 0, 0 }, FrameBuilder.Build(rig, s, 0)[1..7]);

        // 12ch: three panels = three heads for effects
        (rig, s) = One("sgm-p2", "12ch (3 panels, CTC)");
        Assert.Equal(3, rig.Heads.Count);
        Assert.Equal(new byte[] { 250, 255, 2, 0, 0, 255, 0, 0, 255, 0, 0, 255 }, StaticColor(rig, s, "blue")[1..13]);

        // 22ch 16 bit: coarse carries the value, fine stays 0
        (rig, s) = One("sgm-p2", "22ch (3 panels, 16 bit)");
        byte[] f = StaticColor(rig, s, "red");
        Assert.Equal(new byte[] { 250, 255, 0, 2, 255, 0, 0, 0, 0, 0 }, f[1..11]);
        Assert.Equal(3, rig.Heads.Count);
    }

    [Fact]
    public void AllBuiltInModesAreConsistent()
    {
        foreach (FixtureType t in Lib.Types)
            foreach (FixtureMode m in t.Modes)
            {
                Assert.True(m.Footprint >= m.Channels.Count, $"{t.Id}/{m.Name}");
                Assert.All(m.Channels, c => Assert.InRange(c.Number, 1, m.Footprint));
                Assert.Equal(m.Channels.Count, m.Channels.Select(c => c.Number).Distinct().Count());
            }
    }

    [Fact]
    public void LedPar10chHoldsFunctionChannelAtZeroAndDimsOnChannel1()
    {
        var (rig, s) = One("ledpar-18x7-rgbwauv", "10ch", 11);
        byte[] f = StaticColor(rig, s, "red", 50);
        Assert.Equal(128, f[11]);            // dimmer = 50 %
        Assert.Equal(255, f[12]);            // red at full; the dimmer does the dimming
        Assert.Equal(0, f[19]);              // CH9 function: 0 = DMX control
        Assert.Equal(20, rig.Fixtures[0].LastAddress);
    }

    [Fact]
    public void RgbLampMixesWhiteFromRgb()
    {
        var (rig, s) = One("expolite-tourled-42cm", "Arc.1 3ch");
        byte[] f = StaticColor(rig, s, "white");   // "Wit" = R+G+B+W LEDs; this lamp has no W LED
        Assert.Equal(new byte[] { 255, 255, 255 }, f[1..4]);
    }

    [Fact]
    public void RgbLampShowsAmberAsOrange()
    {
        var (rig, s) = One("generic-rgb", "3ch");
        byte[] f = StaticColor(rig, s, "amber");
        Assert.Equal(255, f[1]);
        Assert.InRange(f[2], 180, 200);
        Assert.Equal(0, f[3]);
    }

    [Fact]
    public void TourLedHsvModeSendsHueSaturationValue()
    {
        var (rig, s) = One("expolite-tourled-42cm", "HSV 3ch");
        byte[] f = StaticColor(rig, s, "blue");
        Assert.Equal(170, f[1]);   // 240 degrees
        Assert.Equal(255, f[2]);
        Assert.Equal(255, f[3]);
    }

    [Fact]
    public void XBrick13chHasFourHeadsAndMasterDimmer()
    {
        var (rig, s) = One("stairville-xbrick-16x3", "13ch (4 segments)");
        Assert.Equal(4, rig.Heads.Count);

        s.Mode = OutputMode.Effect;
        s.Effect = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("chase-right"), StepMs = 1000, ColorA = Color.FromArgb(0, 0, 255) };
        s.BrightnessPercent = 100;
        byte[] f = FrameBuilder.Build(rig, s, 2500); // step 2 -> third segment lit
        Assert.Equal(255, f[13]);                    // master dimmer
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0 }, f[1..13]);
    }

    [Fact]
    public void EffectsRunOverAllHeadsOfMixedRig()
    {
        var patch = new[]
        {
            new PatchedFixture { Id = "p", TypeId = "ledpar-18x7-rgbwauv", Mode = "6ch", Address = 1 },
            new PatchedFixture { Id = "x", TypeId = "stairville-xbrick-16x3", Mode = "13ch (4 segments)", Address = 7 },
            new PatchedFixture { Id = "t", TypeId = "expolite-tourled-42cm", Mode = "Ar1.d 4ch", Address = 20 },
        };
        Rig rig = Rig.Build(patch, Lib);
        Assert.Equal(1 + 4 + 1, rig.Heads.Count);
        Assert.Empty(rig.Conflicts());
        Assert.Equal(23, rig.UsedChannelCount);
    }

    [Fact]
    public void OverlappingAddressesAreReported()
    {
        var patch = new[]
        {
            new PatchedFixture { Id = "a", TypeId = "ledpar-18x7-rgbwauv", Mode = "10ch", Address = 1 },
            new PatchedFixture { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 9 },
        };
        AddressConflict c = Assert.Single(Rig.Build(patch, Lib).Conflicts());
        Assert.Equal((9, 10), (c.From, c.To));
    }

    [Fact]
    public void NextFreeAddressFillsGaps()
    {
        Assert.Equal(1, Rig.NextFreeAddress(Array.Empty<(int, int)>(), 6));
        Assert.Equal(7, Rig.NextFreeAddress(new[] { (1, 6) }, 6));
        Assert.Equal(7, Rig.NextFreeAddress(new[] { (1, 6), (20, 10) }, 10));
        Assert.Equal(30, Rig.NextFreeAddress(new[] { (1, 6), (20, 10) }, 14));
        Assert.Equal(-1, Rig.NextFreeAddress(new[] { (1, 510) }, 6));
    }

    [Fact]
    public void IdentifyBlinksWhiteEvenInBlackout()
    {
        var (rig, s) = One("generic-rgbw", "4ch");
        s.Blackout = true;
        s.Identify.Add("a");
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, FrameBuilder.Build(rig, s, 100)[1..5]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, FrameBuilder.Build(rig, s, 300)[1..5]);
    }

    [Fact]
    public void PreviewDecodesFrameBackToColours()
    {
        var (rig, s) = One("ledpar-18x7-rgbwauv", "10ch");
        byte[] f = StaticColor(rig, s, "red", 50);
        Assert.Equal("#800000", Preview.HeadColors(rig, f)[0]);
    }
}

public class EffectTests
{
    [Fact]
    public void SecondCollectionHasHundredUniqueCategorisedEffects()
    {
        Assert.Equal(66, EffectLibrary.IndexOf("audio-surge-strobe") + 1);           // the classic list stays in place
        Assert.Equal(166, EffectLibrary.All.Count);
        var more = EffectLibrary.All.Skip(66).ToList();
        Assert.All(more, e => Assert.Contains(e.Category, new[] { "onoff", "chase", "smooth", "color", "theme", "nature", "party", "sound" }));
        // names are unique too: Home Assistant and commands find effects by name
        Assert.Equal(EffectLibrary.All.Count, EffectLibrary.All.Select(e => e.Name.ToLowerInvariant()).Distinct().Count());
        // sound effects say so (the UI shows them under Sound and the music page)
        Assert.All(more.Where(e => e.Category == "sound"), e => Assert.True(e.Uses.HasFlag(EffectParams.Audio) || e.Uses.HasFlag(EffectParams.Beat)));
    }

    [Fact]
    public void NewEffectsActuallyChangeOverTime()
    {
        // an effect that renders the same picture at every moment is a bug (or a duplicate of "static colour")
        var audio = new AudioFrame(0.6, 0.7, 0.5, 0.8, 3.4, false);
        foreach (EffectDefinition e in EffectLibrary.All.Skip(66))
        {
            int fx = EffectLibrary.IndexOf(e.Key);
            var s = new EffectSettings { EffectIndex = fx, StepMs = 150, OnMs = 200, OffMs = 200, DelayMs = 100 };
            var frames = new HashSet<string>();
            for (long ms = 0; ms < 60000; ms += 377)
            {
                var a = audio with { Beat = ms / 500.0, Bass = (ms % 1000) / 1000.0, High = (ms % 700) / 700.0, Mid = (ms % 900) / 900.0 };
                frames.Add(string.Join(";", EffectLibrary.Render(s, false, ms, 8, a).Select(o => o.Color + "@" + Math.Round(o.Level, 2))));
            }
            Assert.True(frames.Count >= 2, e.Key + " never changes");
        }
    }

    [Fact]
    public void EveryEffectHasAKeyAndStaysInRange()
    {
        Assert.Equal(EffectLibrary.All.Count, EffectLibrary.All.Select(e => e.Key).Distinct().Count());
        Assert.DoesNotContain(EffectLibrary.All, e => e.Key.StartsWith("fx"));
        foreach (int n in new[] { 1, 2, 7, 40 })
            for (int fx = 0; fx < EffectLibrary.All.Count; fx++)
                foreach (long ms in new long[] { 0, 33, 777, 123456, 86400000 })
                {
                    var s = new EffectSettings { EffectIndex = fx, StepMs = 150, ColorPerLamp = true };
                    foreach (LampOutput o in EffectLibrary.Render(s, true, ms, n))
                    {
                        Assert.InRange(o.Level, 0.0, 1.0);
                        Assert.InRange(o.Color.R, 0, 255);
                        Assert.InRange(o.Color.B, 0, 255);
                    }
                }
    }
}
