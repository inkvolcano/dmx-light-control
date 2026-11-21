using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Show;

namespace DmxLicht.Tests;

public class OflImportTests
{
    const string Par = """
    {
      "$schema": "https://raw.githubusercontent.com/OpenLightingProject/open-fixture-library/master/schemas/fixture.json",
      "name": "Party PAR RGBW",
      "categories": ["Color Changer"],
      "availableChannels": {
        "Dimmer": { "fineChannelAliases": ["Dimmer fine"], "capability": { "type": "Intensity" } },
        "Red": { "capability": { "type": "ColorIntensity", "color": "Red" } },
        "Green": { "capability": { "type": "ColorIntensity", "color": "Green" } },
        "Blue": { "capability": { "type": "ColorIntensity", "color": "Blue" } },
        "White": { "capability": { "type": "ColorIntensity", "color": "White" } },
        "Strobe": { "capabilities": [
          { "dmxRange": [0, 9], "type": "ShutterStrobe", "shutterEffect": "Open" },
          { "dmxRange": [10, 255], "type": "ShutterStrobe", "shutterEffect": "Strobe", "speedStart": "1Hz", "speedEnd": "20Hz" } ] },
        "Program": { "capabilities": [
          { "dmxRange": [0, 15], "type": "NoFunction" },
          { "dmxRange": [16, 255], "type": "Effect", "effectName": "Auto", "comment": "Auto programs" } ] }
      },
      "modes": [
        { "name": "4-channel", "shortName": "4ch", "channels": ["Red", "Green", "Blue", "White"] },
        { "name": "8-channel", "shortName": "8ch", "channels": ["Dimmer", "Dimmer fine", "Red", "Green", "Blue", "White", "Strobe", "Program"] }
      ]
    }
    """;

    const string Bar = """
    {
      "name": "Pixel Bar 4",
      "categories": ["Pixel Bar"],
      "availableChannels": { "Master": { "capability": { "type": "Intensity" } } },
      "templateChannels": {
        "Red $pixelKey": { "capability": { "type": "ColorIntensity", "color": "Red" } },
        "Green $pixelKey": { "capability": { "type": "ColorIntensity", "color": "Green" } },
        "Blue $pixelKey": { "capability": { "type": "ColorIntensity", "color": "Blue" } }
      },
      "matrix": { "pixelCount": [4, 1, 1] },
      "modes": [
        { "name": "13-channel", "channels": [
          { "insert": "matrixChannels", "repeatFor": "eachPixel", "channelOrder": "perPixel", "templateChannels": ["Red $pixelKey", "Green $pixelKey", "Blue $pixelKey"] },
          "Master" ] }
      ]
    }
    """;

    [Fact]
    public void ParWithStrobeAndProgramChannels()
    {
        FixtureType t = OflImport.Convert(Par);
        Assert.Equal("Party PAR RGBW", t.Model);
        Assert.Equal("RGBW", t.Colors);
        Assert.Equal(2, t.Modes.Count);

        FixtureMode m = t.Mode("8-channel");
        Assert.Equal(8, m.Footprint);
        Assert.Equal(new[] { ChannelKind.Dimmer, ChannelKind.Fixed, ChannelKind.Red, ChannelKind.Green, ChannelKind.Blue, ChannelKind.White, ChannelKind.Strobe, ChannelKind.Fixed },
                     m.Channels.Select(c => c.Kind));
        ChannelDef strobe = m.Channels[6];
        Assert.Equal((0, 10, 255), (strobe.Default, strobe.RangeMin, strobe.RangeMax));
        Assert.Equal(0, m.Channels[7].Default);                  // program: held at "no function"
        Assert.Contains("Auto programs", m.Channels[7].Note);
    }

    [Fact]
    public void PixelBarBecomesSegments()
    {
        FixtureType t = OflImport.Convert(Bar);
        FixtureMode m = Assert.Single(t.Modes);
        Assert.Equal(13, m.Footprint);
        Assert.Equal(4, m.CellCount);
        Assert.Equal((ChannelKind.Red, 0), (m.Channels[0].Kind, m.Channels[0].Cell));
        Assert.Equal((ChannelKind.Blue, 3), (m.Channels[11].Kind, m.Channels[11].Cell));
        Assert.Equal(ChannelKind.Dimmer, m.Channels[12].Kind);

        // and it renders like the built-in xBrick: four heads
        Rig rig = Rig.Build(new[] { new PatchedFixture { Id = "x", TypeId = t.Id, Mode = m.Name, Address = 1 } }, new FixtureLibrary(new[] { t }));
        Assert.Equal(4, rig.Heads.Count);
    }
}
