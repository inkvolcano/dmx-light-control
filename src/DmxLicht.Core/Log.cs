namespace DmxLicht.Core;

/// <summary>
/// Log file in the "Instellingen" folder next to the exe. At ~1 MB the file moves to dmx-log.1.txt
/// (the previous one is kept), so the history before a problem is not lost.
/// </summary>
public static class Log
{
    /// <summary>
    /// "Instellingen" next to the exe; DMXLICHT_DATA moves it elsewhere (the Linux service uses
    /// /var/lib/dmx-light-control, so the program folder can stay read-only and be replaced on an update).
    /// </summary>
    public static readonly string Dir =
        Environment.GetEnvironmentVariable("DMXLICHT_DATA") is { Length: > 0 } d ? Path.GetFullPath(d)
        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Instellingen");
    public static readonly string FilePath = Path.Combine(Dir, "dmx-log.txt");
    public static readonly string OldFilePath = Path.Combine(Dir, "dmx-log.1.txt");
    public static readonly string CrashDir = Path.Combine(Dir, "Crashes");
    const long MaxBytes = 1_000_000;
    static readonly object lk = new();

    public static void Write(string msg)
    {
        try
        {
            lock (lk)
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes) File.Move(FilePath, OldFilePath, overwrite: true);
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + "\r\n");
            }
        }
        catch { }
    }

    /// <summary>Last lines of the log (both files), newest last.</summary>
    public static string[] Tail(int lines)
    {
        try
        {
            lock (lk)
            {
                var all = new List<string>();
                foreach (string p in new[] { OldFilePath, FilePath })
                {
                    if (!File.Exists(p)) continue;
                    using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var r = new StreamReader(fs);
                    all.AddRange(r.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0));
                }
                return all.TakeLast(lines).ToArray();
            }
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>
    /// Writes a crash file (Instellingen\Crashes\crash-....txt) with the error, the version, Windows and the
    /// last log lines. Never throws. Returns the file path or null.
    /// </summary>
    public static string Crash(string where, object error)
    {
        Write("CRASH (" + where + "): " + error);
        try
        {
            Directory.CreateDirectory(CrashDir);
            string path = Path.Combine(CrashDir, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            string text =
                "DMX Light Control crash\r\n" +
                "Tijd:      " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                "Waar:      " + where + "\r\n" +
                "Versie:    " + AppVersion + "\r\n" +
                "Windows:   " + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " 64-bit" : "") + "\r\n" +
                ".NET:      " + Environment.Version + "\r\n\r\n" +
                "Fout:\r\n" + error + "\r\n\r\n" +
                "Laatste regels van het logboek:\r\n" + string.Join("\r\n", Tail(80)) + "\r\n";
            File.WriteAllText(path, text);
            // keep the folder small
            foreach (FileInfo old in new DirectoryInfo(CrashDir).GetFiles("crash-*.txt").OrderByDescending(f => f.Name).Skip(30))
                try { old.Delete(); } catch { }
            return path;
        }
        catch { return null; }
    }

    public static string AppVersion =>
        (System.Reflection.Assembly.GetEntryAssembly() ?? typeof(Log).Assembly)
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion?.Split('+')[0]
        ?? typeof(Log).Assembly.GetName().Version?.ToString() ?? "?";
}
