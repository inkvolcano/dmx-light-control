using DmxLicht.Core.Control;

namespace DmxLicht.Tests;

/// <summary>The raw MIDI byte parser of the Linux build (ALSA rawmidi).</summary>
public class MidiParserTests
{
    static List<MidiInput> Parse(params int[] bytes)
    {
        var p = new MidiParser();
        var list = new List<MidiInput>();
        foreach (int b in bytes) if (p.Push((byte)b) is MidiInput m) list.Add(m);
        return list;
    }

    [Fact]
    public void NoteOnAndControllerWithChannelsFromOne()
    {
        var m = Parse(0x90, 60, 100, 0xB3, 7, 127);
        Assert.Equal(new[] { new MidiInput("note", 1, 60, 100), new MidiInput("cc", 4, 7, 127) }, m);
    }

    [Fact]
    public void RunningStatusRepeatsTheLastStatus()
    {
        var m = Parse(0xB0, 1, 10, 1, 20, 2, 30);
        Assert.Equal(new[] { new MidiInput("cc", 1, 1, 10), new MidiInput("cc", 1, 1, 20), new MidiInput("cc", 1, 2, 30) }, m);
    }

    [Fact]
    public void NoteOnWithVelocityZeroAndNoteOffAreIgnored()
    {
        Assert.Empty(Parse(0x90, 60, 0, 0x80, 60, 64));
    }

    [Fact]
    public void RealTimeBytesInsideAMessageDoNotBreakIt()
    {
        var m = Parse(0xB0, 0xF8, 5, 0xFE, 99);
        Assert.Equal(new[] { new MidiInput("cc", 1, 5, 99) }, m);
    }

    [Fact]
    public void SysexAndProgramChangeAreSkipped()
    {
        var m = Parse(0xF0, 0x7E, 0x01, 0x02, 0xF7, 0xC0, 5, 0x90, 64, 1);
        Assert.Equal(new[] { new MidiInput("note", 1, 64, 1) }, m);
    }

    [Fact]
    public void DataWithoutStatusIsIgnored()
    {
        Assert.Empty(Parse(10, 20, 30));
        // a system common message ends running status
        Assert.Equal(new[] { new MidiInput("cc", 1, 1, 2) }, Parse(0xB0, 1, 2, 0xF6, 3, 4));
    }
}
