using System.Text.RegularExpressions;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Show;
using Xunit;

namespace DmxLicht.Tests;

public class EffectPreviewTests
{
    [Fact]
    public void EveryEffectGivesAValidLoop()
    {
        var hex = new Regex("^#[0-9A-F]{6}$");
        for (int i = 0; i < EffectLibrary.All.Count; i++)
        {
            var fx = new EffectSettings { EffectIndex = i, ColorA = System.Drawing.Color.Red, ColorB = System.Drawing.Color.Blue };
            string[][] frames = Preview.EffectFrames(fx, 4, 75, 40);
            Assert.Equal(75, frames.Length);
            Assert.All(frames, f => { Assert.Equal(4, f.Length); Assert.All(f, c => Assert.Matches(hex, c)); });
        }
    }

    [Fact]
    public void SoundEffectsMoveWithoutMusic()
    {
        // the made-up song makes sound effects move on the tile, even when nothing is listening
        int i = EffectLibrary.IndexOf("audio-bass-flash");
        var fx = new EffectSettings { EffectIndex = i, ColorA = System.Drawing.Color.Red };
        string[][] frames = Preview.EffectFrames(fx, 4, 75, 40);
        Assert.True(frames.Select(f => string.Join(",", f)).Distinct().Count() > 3);
    }

    [Fact]
    public void SizesAreKeptSafe()
    {
        var fx = new EffectSettings { EffectIndex = 0 };
        Assert.Equal(16, Preview.EffectFrames(fx, 500, 1, 40)[0].Length);
        Assert.Equal(200, Preview.EffectFrames(fx, 4, 100000, 40).Length);
    }
}
