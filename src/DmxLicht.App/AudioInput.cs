using DmxLicht.Core;
using DmxLicht.Core.Audio;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DmxLicht.App;

/// <summary>
/// Captures sound (a microphone, or "loopback" = whatever this PC plays) and feeds the analyser.
/// Follows the music settings: starts, stops and switches device when they change, and retries
/// when a device disappears.
/// </summary>
sealed class AudioInput : IDisposable
{
    public const string Loopback = "loopback";

    readonly LightEngine engine;
    readonly object sync = new();
    IWaveIn capture;
    AudioAnalyzer analyzer;
    bool wantRunning;
    string wantDevice;
    System.Threading.Timer retry;

    public string Error { get; private set; }
    public bool Running => capture != null;

    public AudioInput(LightEngine engine) { this.engine = engine; }

    public static List<object> Devices()
    {
        var list = new List<object> { new { id = Loopback, name = "loopback" }, new { id = "", name = "default" } };
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (MMDevice d in en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                list.Add(new { id = d.ID, name = d.FriendlyName });
        }
        catch (Exception ex) { Log.Write("Audio-apparaten opvragen mislukt: " + ex.Message); }
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
        try
        {
            using var en = new MMDeviceEnumerator();
            if (wantDevice == Loopback)
                capture = new WasapiLoopbackCapture(en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia));
            else
            {
                MMDevice d = wantDevice == "" ? en.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console) : en.GetDevice(wantDevice);
                capture = new WasapiCapture(d) { ShareMode = AudioClientShareMode.Shared };
            }
            analyzer = new AudioAnalyzer(capture.WaveFormat.SampleRate) { Sensitivity = engine.Read(s => s.State.Music.Sensitivity) };
            analyzer.Beat += _ => engine.Beat.AudioBeat(engine.Ms, analyzer.Levels.Bpm);
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnStopped;
            capture.StartRecording();
            Error = null;
            Log.Write("Geluid luisteren gestart: " + (wantDevice == Loopback ? "geluid van deze pc" : wantDevice == "" ? "standaard microfoon" : wantDevice));
        }
        catch (Exception ex)
        {
            StopCapture();
            Error = ex.Message;
            Log.Write("Geluid luisteren mislukt: " + ex.Message);
            ScheduleRetry();
        }
    }

    void ScheduleRetry()
    {
        retry?.Dispose();
        retry = new System.Threading.Timer(_ => { lock (sync) if (wantRunning && capture == null) Restart(); }, null, 3000, Timeout.Infinite);
    }

    void OnStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception == null) return;
        lock (sync)
        {
            Error = e.Exception.Message;
            Log.Write("Geluid gestopt: " + e.Exception.Message);
            StopCapture();
            ScheduleRetry();   // e.g. the microphone was unplugged
        }
    }

    float[] mono = new float[4096];

    void OnData(object sender, WaveInEventArgs e)
    {
        WaveFormat f = ((IWaveIn)sender).WaveFormat;
        int ch = Math.Max(1, f.Channels);
        int frames;
        if (f.Encoding == WaveFormatEncoding.IeeeFloat || (f.Encoding == WaveFormatEncoding.Extensible && f.BitsPerSample == 32))
        {
            frames = e.BytesRecorded / 4 / ch;
            if (mono.Length < frames) mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int c = 0; c < ch; c++) sum += BitConverter.ToSingle(e.Buffer, (i * ch + c) * 4);
                mono[i] = sum / ch;
            }
        }
        else if (f.BitsPerSample == 16)
        {
            frames = e.BytesRecorded / 2 / ch;
            if (mono.Length < frames) mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int c = 0; c < ch; c++) sum += BitConverter.ToInt16(e.Buffer, (i * ch + c) * 2) / 32768f;
                mono[i] = sum / ch;
            }
        }
        else return;

        AudioAnalyzer a = analyzer;
        if (a == null) return;
        a.Process(mono.AsSpan(0, frames));
        engine.SetAudio(a.Levels);
    }

    void StopCapture()
    {
        IWaveIn c = capture;
        capture = null;
        if (c == null) return;
        try
        {
            c.DataAvailable -= OnData;
            c.RecordingStopped -= OnStopped;
            c.StopRecording();
            c.Dispose();
        }
        catch { }
    }

    public void Dispose()
    {
        lock (sync) { wantRunning = false; retry?.Dispose(); StopCapture(); }
    }
}
