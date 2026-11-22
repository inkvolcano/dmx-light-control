using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DmxLicht.Core;
using DmxLicht.Core.Audio;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;

namespace DmxLicht.App;

/// <summary>
/// Linux build: captures a microphone / line-in with ALSA's arecord (alsa-utils, on Raspberry Pi OS by default) and
/// feeds the same analyser as on Windows. 16-bit mono at 44.1 kHz on stdout; "plughw" lets ALSA convert whatever the
/// sound card delivers. Follows the music settings and retries when the device disappears, like the Windows version.
/// "Loopback" (what this computer plays) does not exist here; it means the default microphone.
/// </summary>
sealed class AudioInput : IDisposable
{
    public const string Loopback = "loopback";
    const int Rate = 44100;

    readonly LightEngine engine;
    readonly object sync = new();
    Process capture;
    AudioAnalyzer analyzer;
    bool wantRunning;
    string wantDevice;
    System.Threading.Timer retry;

    public string Error { get; private set; }
    public bool Running => capture != null;

    public AudioInput(LightEngine engine) { this.engine = engine; }

    static readonly Regex CardLine = new(@"^card (\d+): (\S+) \[(.*?)\], device (\d+): .*?\[(.*?)\]", RegexOptions.Compiled);

    /// <summary>Capture devices from "arecord -l": id plughw:CARD=&lt;card&gt;,DEV=&lt;n&gt; (stable across reboots).</summary>
    static List<(string id, string name)> Hardware()
    {
        var list = new List<(string, string)>();
        try
        {
            var psi = new ProcessStartInfo("arecord", "-l") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using Process p = Process.Start(psi)!;
            string text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            foreach (string line in text.Split('\n'))
            {
                Match m = CardLine.Match(line.Trim());
                if (!m.Success) continue;
                string card = m.Groups[2].Value, dev = m.Groups[4].Value, cardName = m.Groups[3].Value, devName = m.Groups[5].Value;
                list.Add(($"plughw:CARD={card},DEV={dev}", devName.Length > 0 && devName != cardName ? cardName + " - " + devName : cardName));
            }
        }
        catch (Exception ex) { Log.Write("Audio-apparaten opvragen mislukt (arecord): " + ex.Message); }
        return list;
    }

    public static List<object> Devices()
    {
        var list = new List<object> { new { id = "", name = "default" } };
        foreach (var (id, name) in Hardware()) list.Add(new { id, name });
        return list;
    }

    /// <summary>Call after any settings change; cheap when nothing relevant changed.</summary>
    public void Apply(MusicSettings m)
    {
        lock (sync)
        {
            if (analyzer != null) analyzer.Sensitivity = m.Sensitivity;
            string dev = m.DeviceId ?? "";
            if (m.Enabled == wantRunning && dev == wantDevice) return;
            wantRunning = m.Enabled;
            wantDevice = dev;
            Restart();
        }
    }

    void Restart()
    {
        StopCapture();
        if (!wantRunning) { Error = null; return; }
        // "loopback" (the default in a new show, and in shows from Windows) does not exist here: the microphone instead.
        // "default": on Raspberry Pi OS Lite the ALSA default is often the HDMI/headphone output without a microphone,
        // so take the first real capture device when there is one
        string device = wantDevice == Loopback ? "" : wantDevice;
        if (device == "") device = Hardware().Select(h => h.id).FirstOrDefault() ?? "default";
        try
        {
            var psi = new ProcessStartInfo("arecord")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            // small blocks (10 ms periods): beats arrive on time
            foreach (string a in new[] { "-q", "-D", device, "-f", "S16_LE", "-c", "1", "-r", Rate.ToString(), "-t", "raw",
                                         "--period-time=10000", "--buffer-time=80000" })
                psi.ArgumentList.Add(a);
            Process p = Process.Start(psi) ?? throw new InvalidOperationException("arecord start niet");
            analyzer = new AudioAnalyzer(Rate) { Sensitivity = engine.Read(s => s.State.Music.Sensitivity) };
            AudioAnalyzer a0 = analyzer;
            a0.Beat += _ => engine.Beat.AudioBeat(engine.Ms, a0.Levels.Bpm);
            capture = p;
            Error = null;
            new Thread(() => ReadLoop(p, a0)) { IsBackground = true, Name = "arecord" }.Start();
            Log.Write("Geluid luisteren gestart (arecord): " + device);
        }
        catch (Win32Exception)
        {
            StopCapture();
            Error = "arecord ontbreekt: sudo apt install alsa-utils";
            Log.Write("Geluid luisteren mislukt: " + Error);
        }
        catch (Exception ex)
        {
            StopCapture();
            Error = ex.Message;
            Log.Write("Geluid luisteren mislukt: " + ex.Message);
            ScheduleRetry();
        }
    }

    void ReadLoop(Process p, AudioAnalyzer a)
    {
        Task<string> err = p.StandardError.ReadToEndAsync();
        Stream s = p.StandardOutput.BaseStream;
        var buf = new byte[1024];   // ~12 ms of sound per read
        var mono = new float[buf.Length / 2];
        int carry = 0;
        try
        {
            int n;
            while ((n = s.Read(buf, carry, buf.Length - carry)) > 0)
            {
                n += carry;
                int frames = n / 2;
                for (int i = 0; i < frames; i++) mono[i] = BitConverter.ToInt16(buf, i * 2) / 32768f;
                carry = n - frames * 2;                 // an odd byte waits for the next read
                if (carry > 0) buf[0] = buf[n - 1];
                a.Process(mono.AsSpan(0, frames));
                engine.SetAudio(a.Levels);
            }
        }
        catch { }
        lock (sync)
        {
            if (capture != p) return;                   // stopped on purpose (settings change, shutdown)
            string why = "";
            try { why = err.Wait(1000) ? err.Result.Trim().Split('\n').LastOrDefault()?.Trim() ?? "" : ""; } catch { }
            Error = "Geluid gestopt" + (why.Length > 0 ? ": " + why : "");
            Log.Write(Error);
            StopCapture();
            ScheduleRetry();   // e.g. the USB microphone was unplugged
        }
    }

    void ScheduleRetry()
    {
        retry?.Dispose();
        retry = new System.Threading.Timer(_ => { lock (sync) if (wantRunning && capture == null) Restart(); }, null, 3000, Timeout.Infinite);
    }

    void StopCapture()
    {
        Process c = capture;
        capture = null;
        if (c == null) return;
        try { if (!c.HasExited) c.Kill(); } catch { }
        try { c.Dispose(); } catch { }
    }

    public void Dispose()
    {
        lock (sync) { wantRunning = false; retry?.Dispose(); StopCapture(); }
    }
}
