using System.Drawing;
using DmxLicht.Core;
using DmxLicht.Core.Audio;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class AudioEffectTests
{
    static LampOutput[] Render(string key, int lamps, AudioFrame audio, long ms = 0) =>
        EffectLibrary.Render(new EffectSettings { EffectIndex = EffectLibrary.IndexOf(key) }, false, ms, lamps, audio);

    [Fact]
    public void AllSoundEffectsExistAndStayInRange()
    {
        var keys = EffectLibrary.All.Where(e => e.Uses.HasFlag(EffectParams.Audio) || e.Uses.HasFlag(EffectParams.Beat)).Select(e => e.Key).ToList();
        Assert.Equal(21, keys.Count);   // 14 classic + 7 from the second collection
        var r = new Random(1);
        foreach (string k in keys)
            foreach (int n in new[] { 1, 2, 3, 7, 24 })
                for (int t = 0; t < 30; t++)
                {
                    var a = new AudioFrame(r.NextDouble(), r.NextDouble(), r.NextDouble(), r.NextDouble(), r.NextDouble() * 100, r.Next(2) == 0);
                    foreach (LampOutput o in Render(k, n, a, t * 37))
                    {
                        Assert.InRange(o.Level, 0.0, 1.0);
                        Assert.InRange(o.Color.R, 0, 255);
                        Assert.InRange(o.Color.G, 0, 255);
                        Assert.InRange(o.Color.B, 0, 255);
                    }
                }
    }

    [Fact]
    public void VuMeterLightsAsManyLampsAsTheVolume()
    {
        LampOutput[] o = Render("audio-vu", 10, new AudioFrame(0.5, 0, 0, 0, 0, false));
        Assert.Equal(new[] { 1.0, 1, 1, 1, 1, 0, 0, 0, 0, 0 }, o.Select(x => x.Level));
        Assert.Equal(255, o[0].Color.G);                     // green at the bottom (colour A)...
        Assert.All(Render("audio-vu", 10, new AudioFrame(0, 0, 0, 0, 0, false)), x => Assert.Equal(0, x.Level));
    }

    [Fact]
    public void BandsGoLeftBassMiddleMidRightHigh()
    {
        LampOutput[] o = Render("audio-bands", 6, new AudioFrame(0.5, 0.9, 0.4, 0.1, 0, false));
        Assert.Equal(new[] { 0.9, 0.9, 0.4, 0.4, 0.1, 0.1 }, o.Select(x => x.Level));
        Assert.Equal(Light.Red, o[0].Color);
        Assert.Equal(Light.Green, o[2].Color);
        Assert.Equal(Light.Blue, o[5].Color);
    }

    [Fact]
    public void SpectrumIsRedOnPureBass()
    {
        LampOutput o = Render("audio-spectrum", 1, new AudioFrame(0.8, 0.8, 0, 0, 0, false))[0];
        Assert.Equal((255, 0, 0), (o.Color.R, o.Color.G, o.Color.B));
        Assert.Equal(0.8, o.Level, 3);
    }

    [Fact]
    public void BeatEffectsChangeOnEveryBeat()
    {
        var s = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("audio-beat-ab"), ColorA = Color.Red, ColorB = Color.Blue };
        Light onBeat0 = EffectLibrary.Render(s, false, 0, 1, new AudioFrame(0, 0, 0, 0, 0.1, false))[0].Color;
        Light onBeat1 = EffectLibrary.Render(s, false, 0, 1, new AudioFrame(0, 0, 0, 0, 1.1, false))[0].Color;
        Assert.Equal(Light.Red, onBeat0);
        Assert.Equal(Light.Blue, onBeat1);

        Light c0 = Render("audio-beat-colors", 1, new AudioFrame(0, 0, 0, 0, 3.2, false))[0].Color;
        Light c1 = Render("audio-beat-colors", 1, new AudioFrame(0, 0, 0, 0, 4.2, false))[0].Color;
        Assert.NotEqual(c0, c1);
        Assert.Equal(c0, Render("audio-beat-colors", 1, new AudioFrame(0, 0, 0, 0, 3.9, false))[0].Color);  // same beat, same colour
    }

    [Fact]
    public void SurgeStrobesWhite()
    {
        LampOutput o = Render("audio-surge-strobe", 1, new AudioFrame(1, 1, 1, 1, 0, true), ms: 0)[0];
        Assert.Equal((Light.White, 1.0), (o.Color, o.Level));
        Assert.NotEqual(Light.White, Render("audio-surge-strobe", 1, new AudioFrame(1, 1, 1, 1, 0, false))[0].Color);
    }

    [Fact]
    public void EngineFeedsSmoothedSoundToEffects()
    {
        var show = new ShowFile { Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } } };
        show.State.Mode = OutputMode.Effect;
        show.State.Effect = new EffectSettings { EffectIndex = EffectLibrary.IndexOf("audio-spectrum") };
        var output = new CaptureOutput();
        using var engine = new LightEngine(show, output, null);
        engine.Start();
        var until = DateTime.Now.AddSeconds(3);
        while (DateTime.Now < until && output.Last[1] < 200)
        {
            engine.SetAudio(new AudioLevels(0.9, 0.9, 0.1, 0.0, 120, 1));   // a kick
            Thread.Sleep(10);
        }
        Assert.True(output.Last[1] >= 200, "red follows the bass");
        Assert.True(output.Last[3] < 30, "no blue without highs");
        Thread.Sleep(1000);   // no more audio data: after 0.4 s it counts as silence, then fades out in ~0.3 s
        Assert.True(output.Last[1] < 20, $"fades out after the sound stops ({output.Last[1]})");
    }
}
