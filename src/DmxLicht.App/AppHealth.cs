using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
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
    /// Measured in UTC, so a clock change (summer time) is not a stall.
    /// </summary>
    public static void StartStallWatch()
    {
        new Thread(() =>
        {
            var sw = Stopwatch.StartNew();
            DateTime lastUtc = DateTime.UtcNow;
            long lastMs = 0;
            while (true)
            {
                Thread.Sleep(1000);
                long now = sw.ElapsedMilliseconds;
                DateTime nowUtc = DateTime.UtcNow;
                // the stopwatch does not count sleep; the clock does: use the larger gap
                double gap = Math.Max(now - lastMs, (nowUtc - lastUtc).TotalMilliseconds);
                if (gap > 5000)
                    Log.Write($"App stond {gap / 1000:0} s stil (van {lastUtc.ToLocalTime():HH:mm:ss} tot {nowUtc.ToLocalTime():HH:mm:ss}): slaapstand of een vastgelopen pc");
                lastMs = now; lastUtc = nowUtc;
            }
        }) { IsBackground = true, Name = "stall watch", Priority = ThreadPriority.AboveNormal }.Start();
    }

    /// <summary>What Windows recorded in the last days: the dongle leaving the USB bus, sleep and wake, app crashes.</summary>
    public static string WindowsEvents()
    {
        var b = new StringBuilder();
        b.AppendLine("Windows-gebeurtenissen (laatste 3 dagen, nieuwste eerst, tijden in pc-tijd):");
        const string days = "259200000";
        // device removals: a laptop logs many (Wi-Fi, hubs on every wake); the dongle is picked out afterwards
        Section(b, "Dongle van de USB-bus (verdwenen of eruit getrokken)",
            "Microsoft-Windows-Kernel-PnP/Device Management", $"*[System[(EventID=1010) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            e => e.Contains("VID_16C0", StringComparison.OrdinalIgnoreCase), 2000);
        Section(b, "Slaapstand en wakker worden",
            "System", $"*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=506 or EventID=507 or EventID=42 or EventID=107 or EventID=41) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            _ => true, 60);
        Section(b, "App vastgelopen of gecrasht",
            "Application", $"*[System[(EventID=1000 or EventID=1002 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= {days}]]]",
            e => e.Contains("DmxLicht", StringComparison.OrdinalIgnoreCase), 300);
        return b.ToString();
    }

    static void Section(StringBuilder b, string title, string log, string query, Func<string, bool> keep, int max)
    {
        b.AppendLine("  " + title + ":");
        try
        {
            var events = Query(log, query, max).Where(e => keep(e.raw)).Take(25).ToList();
            if (events.Count == 0) b.AppendLine("    (niets)");
            foreach (var (when, text, _) in events) b.AppendLine("    " + when + "  " + text);
        }
        catch (Exception ex) { b.AppendLine("    (niet te lezen: " + ex.Message + ")"); }
    }

    /// <summary>
    /// wevtutil instead of an extra package, as XML (the same on Dutch and English Windows): local time, the first
    /// line of the message, and the raw event for filtering. A failed query throws instead of looking like "nothing".
    /// </summary>
    static IEnumerable<(string when, string text, string raw)> Query(string log, string query, int max)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding oem;
        try { oem = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch { oem = Encoding.UTF8; }
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "wevtutil.exe"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = oem, StandardErrorEncoding = oem,
        };
        foreach (string a in new[] { "qe", log, "/q:" + query, "/rd:true", "/c:" + max, "/f:RenderedXml" }) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi);
        Task<string> read = p.StandardOutput.ReadToEndAsync(), err = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } throw new TimeoutException("wevtutil reageert niet"); }
        if (p.ExitCode != 0)
            throw new InvalidOperationException((err.Result ?? "").Trim() is { Length: > 0 } e ? e : "wevtutil fout " + p.ExitCode);

        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        XDocument doc = XDocument.Parse("<Events>" + read.Result + "</Events>");
        var list = new List<(string, string, string)>();
        foreach (XElement ev in doc.Root!.Elements(ns + "Event"))
        {
            string when = "?";
            string t = ev.Element(ns + "System")?.Element(ns + "TimeCreated")?.Attribute("SystemTime")?.Value;
            if (t != null && DateTimeOffset.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dto))
                when = dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            string msg = ev.Element(ns + "RenderingInfo")?.Element(ns + "Message")?.Value;
            if (string.IsNullOrWhiteSpace(msg))
                msg = string.Join(", ", ev.Element(ns + "EventData")?.Elements().Select(x => x.Value) ?? Enumerable.Empty<string>());
            string first = msg.Split('\r', '\n').FirstOrDefault(x => x.Trim().Length > 0)?.Trim() ?? "";
            list.Add((when, first.Length > 200 ? first[..200] + "..." : first, ev.ToString()));
        }
        return list;
    }
}
