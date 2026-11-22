using System.Runtime.InteropServices;
using DmxLicht.Core;
using DmxLicht.Core.Control;

namespace DmxLicht.App;

/// <summary>
/// Linux build: one MIDI input read straight from ALSA's raw MIDI device (/dev/snd/midiC&lt;card&gt;D&lt;device&gt;).
/// Needs no extra packages; the user must be in the "audio" group. Read with poll() so that stopping never hangs on a
/// blocked read, and so an unplugged controller is noticed.
/// </summary>
sealed class LinuxMidiIn : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct PollFd { public int fd; public short events, revents; }

    [DllImport("libc", SetLastError = true)] static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", SetLastError = true)] static extern nint read(int fd, byte[] buf, nint count);
    [DllImport("libc", SetLastError = true)] static extern int poll(ref PollFd fds, nuint nfds, int timeout);
    [DllImport("libc")] static extern int close(int fd);

    const int O_RDONLY = 0, O_NONBLOCK = 0x800;
    const short POLLIN = 1, POLLERR = 8, POLLHUP = 16, POLLNVAL = 32;

    public sealed record Port(string Path, string Name);

    /// <summary>Raw MIDI ports, named after the sound card (e.g. "nanoKONTROL2").</summary>
    public static List<Port> Ports()
    {
        var list = new List<Port>();
        if (!Directory.Exists("/dev/snd")) return list;
        foreach (string path in Directory.GetFiles("/dev/snd", "midiC*D*").OrderBy(p => p, StringComparer.Ordinal))
        {
            string file = System.IO.Path.GetFileName(path);            // midiC1D0
            int d = file.IndexOf('D', 5);
            if (d < 0) continue;
            string card = file[5..d], dev = file[(d + 1)..];
            string name = null;
            try { name = File.ReadAllText($"/proc/asound/card{card}/id").Trim(); } catch { }
            if (string.IsNullOrEmpty(name)) name = "MIDI " + card;
            list.Add(new Port(path, dev == "0" ? name : name + " " + dev));
        }
        // the same name twice (two identical controllers): make them distinct
        return list.GroupBy(p => p.Name).SelectMany(g => g.Count() == 1 ? g : g.Select((p, i) => p with { Name = p.Name + " (" + (i + 1) + ")" })).ToList();
    }

    readonly string path;
    readonly MidiParser parser = new();
    Thread thread;
    volatile bool run;

    public event Action<MidiInput> Received;
    /// <summary>The device went away or could not be read; the text says why.</summary>
    public event Action<string> Failed;

    public LinuxMidiIn(string path) { this.path = path; }

    public void Start()
    {
        int fd = open(path, O_RDONLY | O_NONBLOCK);
        if (fd < 0)
        {
            int e = Marshal.GetLastPInvokeError();
            throw new IOException(e == 13
                ? "geen toegang tot " + path + " (de gebruiker moet in de groep audio)"
                : path + " openen mislukt (errno " + e + ")");
        }
        run = true;
        thread = new Thread(() => Loop(fd)) { IsBackground = true, Name = "MIDI" };
        thread.Start();
    }

    void Loop(int fd)
    {
        var buf = new byte[256];
        string failure = null;
        try
        {
            while (run)
            {
                var p = new PollFd { fd = fd, events = POLLIN };
                int r = poll(ref p, 1, 200);
                if (r < 0)
                {
                    if (Marshal.GetLastPInvokeError() == 4) continue;   // EINTR
                    failure = "MIDI lezen mislukt"; break;
                }
                if (r == 0) continue;
                if ((p.revents & (POLLERR | POLLHUP | POLLNVAL)) != 0) { failure = "MIDI-apparaat losgekoppeld"; break; }
                nint n = read(fd, buf, buf.Length);
                if (n < 0)
                {
                    int e = Marshal.GetLastPInvokeError();
                    if (e is 11 or 4) continue;                            // EAGAIN, EINTR
                    failure = "MIDI-apparaat losgekoppeld (errno " + e + ")"; break;
                }
                if (n == 0) continue;
                for (int i = 0; i < n; i++)
                    if (parser.Push(buf[i]) is MidiInput m)
                        try { Received?.Invoke(m); } catch (Exception ex) { Log.Write("MIDI-opdracht fout: " + ex.Message); }
            }
        }
        finally { close(fd); }
        if (failure != null && run) Failed?.Invoke(failure);
    }

    public void Stop()
    {
        run = false;
        if (thread != null && thread != Thread.CurrentThread) thread.Join(1000);
        thread = null;
    }

    public void Dispose() => Stop();
}
