using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Engine;
using NAudio.Midi;

namespace DmxLicht.App;

/// <summary>
/// MIDI input: notes and controllers run their mapped commands. "Learn" captures the next message
/// so the UI can link it. CC used as a button fires when it goes over half way (a press).
/// </summary>
sealed class MidiControl : IDisposable
{
    public sealed record Message(string Type, int Channel, int Number, int Value);

    static readonly HashSet<string> ValueCommands = new() { "master", "brightness", "bpm", "fade" };

    readonly LightEngine engine;
    readonly object sync = new();
    MidiIn input;
    string device;
    bool enabled;
    List<MidiMapping> mappings = new();
    readonly Dictionary<(int, int), int> lastCc = new();

    public Message Last { get; private set; }
    public bool Learning { get; set; }
    public Message Learned { get; private set; }
    public string Error { get; private set; }
    public string Device => input != null ? device : null;

    public MidiControl(LightEngine engine) { this.engine = engine; }

    public static List<string> Devices()
    {
        var list = new List<string>();
        try { for (int i = 0; i < MidiIn.NumberOfDevices; i++) list.Add(MidiIn.DeviceInfo(i).ProductName); }
        catch (Exception ex) { Log.Write("MIDI-apparaten opvragen mislukt: " + ex.Message); }
        return list;
    }

    public void Apply(ControlSettings s)
    {
        lock (sync)
        {
            mappings = s.MidiMappings?.ToList() ?? new();
            string want = s.MidiEnabled ? (s.MidiDevice ?? Devices().FirstOrDefault()) : null;
            if (enabled == s.MidiEnabled && want == device && (input != null || want == null)) return;
            Close();
            enabled = s.MidiEnabled;
            device = want;
            if (want == null) return;
            int index = Devices().IndexOf(want);
            if (index < 0) { Error = "MIDI-apparaat niet gevonden: " + want; return; }
            try
            {
                input = new MidiIn(index);
                input.MessageReceived += OnMessage;
                input.Start();
                Error = null;
                Log.Write("MIDI luistert naar " + want);
            }
            catch (Exception ex) { Error = ex.Message; input = null; Log.Write("MIDI openen mislukt: " + ex.Message); }
        }
    }

    void OnMessage(object sender, MidiInMessageEventArgs e)
    {
        Message m = e.MidiEvent switch
        {
            NoteOnEvent n when n.Velocity > 0 => new Message("note", n.Channel, n.NoteNumber, n.Velocity),
            ControlChangeEvent c => new Message("cc", c.Channel, (int)c.Controller, c.ControllerValue),
            _ => null,
        };
        if (m == null) return;
        Last = m;
        if (Learning) { Learned = m; Learning = false; return; }

        List<MidiMapping> map;
        lock (sync) map = mappings;
        foreach (MidiMapping x in map)
        {
            if (x.Type != m.Type || x.Number != m.Number || (x.Channel != 0 && x.Channel != m.Channel)) continue;
            Run(x, m);
        }
    }

    void Run(MidiMapping x, Message m)
    {
        var parts = Commands.Split(x.Command);
        if (parts.Count == 0) return;
        string cmd = parts[0].ToLowerInvariant();
        if (m.Type == "cc" && parts.Count == 1 && ValueCommands.Contains(cmd))
        {
            double v = cmd switch
            {
                "bpm" => 60 + m.Value * 120.0 / 127,
                "fade" => m.Value * 5.0 / 127,
                _ => Math.Round(m.Value * 100.0 / 127),   // whole percent: "0.79" would read as a 0..1 fader (79 %)
            };
            Commands.Run(engine, cmd + " " + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (m.Type == "cc")
        {
            // as a button: fire once when it crosses half way going up
            int prev = lastCc.TryGetValue((m.Channel, m.Number), out int p) ? p : 0;
            lastCc[(m.Channel, m.Number)] = m.Value;
            if (!(prev < 64 && m.Value >= 64)) return;
        }
        Commands.Run(engine, x.Command);
    }

    void Close()
    {
        if (input == null) return;
        try { input.Stop(); input.MessageReceived -= OnMessage; input.Dispose(); } catch { }
        input = null;
    }

    public void Dispose() { lock (sync) Close(); }
}
