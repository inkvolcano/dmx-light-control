namespace DmxLicht.Core.Control;

/// <summary>A note-on (velocity &gt; 0) or a controller change. Channel 1..16, like NAudio reports it.</summary>
public readonly record struct MidiInput(string Type, int Channel, int Number, int Value);

/// <summary>
/// Turns a raw MIDI byte stream (Linux ALSA rawmidi, /dev/snd/midiC*D*) into note-on and controller messages.
/// Handles running status, skips system exclusive and system common messages, and ignores real-time bytes
/// (clock, start, stop) wherever they appear. Everything else (note off, pitch bend, ...) is dropped, as on Windows.
/// </summary>
public sealed class MidiParser
{
    int status;          // current channel status byte (running status), 0 = none
    int need, have;
    byte data1;
    bool sysex;

    /// <summary>Feeds one byte; returns a message when one is complete.</summary>
    public MidiInput? Push(byte b)
    {
        if (b >= 0xF8) return null;                       // real-time: does not disturb anything
        if (b >= 0x80)
        {
            sysex = b == 0xF0;
            if (b >= 0xF0) { status = 0; return null; }   // system common / sysex: ends running status
            status = b;
            need = (b & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
            have = 0;
            return null;
        }
        if (sysex || status == 0) return null;            // data without a status (or inside sysex)
        if (have == 0 && need == 2) { data1 = b; have = 1; return null; }
        have = 0;                                         // complete; the next data byte starts a new message (running status)
        int kind = status & 0xF0, channel = (status & 0x0F) + 1;
        if (need == 1) return null;
        if (kind == 0x90 && b > 0) return new MidiInput("note", channel, data1, b);
        if (kind == 0xB0) return new MidiInput("cc", channel, data1, b);
        return null;
    }
}
