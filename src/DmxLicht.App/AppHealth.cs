using System.Diagnostics;
using System.Text;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Leaves a trace of what happened around a problem: when the app stood still (sleep, a frozen PC) and what
/// Windows itself recorded about the dongle, sleep and crashes. Both end up in the log or the problem report.
/// </summary>
static class AppHealth
{
    /// <summary>
    /// A background tick every second; a much longer gap means the whole app stood still (Modern Standby,
    /// sleep, or a PC so busy nothing ran). Logged once per gap, so the log shows "app stood still for 6 s".
    /// </summary>
    public static void StartStallWatch()
    {
        new Thread(() =>
        {
            var sw = Stopwatch.StartNew();
            DateTime last = DateTime.Now;
            long lastMs = 0;
            while (true)
            {
                Thread.Sleep(1000);
                long now = sw.ElapsedMilliseconds;
                DateTime wall = DateTime.Now;
                // the stopwatch does not count sleep; the wall clock does: use the larger gap
                double gap = Math.Max(now - lastMs, (wall - last).TotalMilliseconds);
                if (gap > 5000)
                    Log.Write($"App stond {gap / 1000:0} s stil (van {last:HH:mm:ss} tot {wall:HH:mm:ss}): slaapstand of een vastgelopen pc");
                lastMs = now; last = wall;
            }
        }) { IsBackground = true, Name = "stall watch", Priority = ThreadPriority.AboveNormal }.Start();
    }

    /// <summary>What Windows recorded in the last days: the dongle leaving the USB bus, sleep and wake, app crashes.</summary>
    public static string WindowsEvents()
    {
        var b = new StringBuilder();
        b.AppendLine("Windows-gebeurtenissen (laatste 3 dagen, nieuwste eerst):");
        const string days = "259200000";
        Section(b, "Dongle van de USB-bus (verdwenen of eruit getrokken)",
            "Microsoft-Windows-Kernel-PnP/Device Management", $"*[System[(EventID=1010) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            l => l.Contains("VID_16C0", StringComparison.OrdinalIgnoreCase));
        Section(b, "Slaapstand en wakker worden",
            "System", $"*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=506 or EventID=507 or EventID=42 or EventID=107 or EventID=41) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            _ => true);
        Section(b, "App vastgelopen of gecrasht",
            "Application", $"*[System[(EventID=1000 or EventID=1002 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            l => l.Contains("DmxLicht", StringComparison.OrdinalIgnoreCase));
        return b.ToString();
    }

    static void Section(StringBuilder b, string title, string log, string query, Func<string, bool> keep)
    {
        b.AppendLine("  " + title + ":");
        try
        {
            var events = Query(log, query).Where(e => keep(e.text)).Take(25).ToList();
            if (events.Count == 0) b.AppendLine("    (niets)");
            foreach (var (when, text) in events) b.AppendLine("    " + when + "  " + text);
        }
        catch (Exception ex) { b.AppendLine("    (niet te lezen: " + ex.Message + ")"); }
    }

    /// <summary>wevtutil instead of an extra package: date plus the first line of each event's description.</summary>
    static IEnumerable<(string when, string text)> Query(string log, string query)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "wevtutil.exe"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (string a in new[] { "qe", log, "/q:" + query, "/rd:true", "/c:60", "/f:text" }) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi);
        Task<string> read = p.StandardOutput.ReadToEndAsync();
        if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } throw new TimeoutException("wevtutil reageert niet"); }
        string output = read.Result;

        var list = new List<(string, string)>();
        string when = null; bool inDescription = false;
        foreach (string raw in output.Split('\n'))
        {
            string line = raw.Replace("\0", "").TrimEnd('\r');
            string t = line.Trim();
            if (t.StartsWith("Event[")) { when = null; inDescription = false; continue; }
            if (t.StartsWith("Date:")) { when = t[5..].Trim().Replace('T', ' '); if (when.Length > 19) when = when[..19]; continue; }
            if (t.StartsWith("Description:")) { inDescription = true; continue; }
            if (inDescription && t.Length > 0)
            {
                list.Add((when ?? "?", t.Length > 200 ? t[..200] + "..." : t));
                inDescription = false;
            }
        }
        return list;
    }
}
