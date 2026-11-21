namespace DmxLicht.Core.Show;

/// <summary>When a schedule item fires: at a clock time, or relative to sunrise/sunset.</summary>
public enum ScheduleTimeKind { Time, Sunrise, Sunset }

/// <summary>One planned action, e.g. "weekdays 18:00: scene Sfeer" or "every day sunset +15 min: on".</summary>
public sealed class ScheduleItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    /// <summary>Days it runs (Monday = 1 ... Sunday = 7). Empty = every day.</summary>
    public List<int> Days { get; set; } = new();
    /// <summary>Only on this date (yyyy-MM-dd); null = every matching day.</summary>
    public string Date { get; set; }
    public ScheduleTimeKind Kind { get; set; } = ScheduleTimeKind.Time;
    /// <summary>"HH:mm" for <see cref="ScheduleTimeKind.Time"/>.</summary>
    public string Time { get; set; } = "18:00";
    /// <summary>Minutes before (negative) or after the sunrise/sunset.</summary>
    public int OffsetMinutes { get; set; }
    /// <summary>A line in the command language, e.g. <c>scene "Sfeer"</c>, <c>off</c>, <c>timeline play Show</c>.</summary>
    public string Command { get; set; } = "on";
    /// <summary>Fade into the new look over this many seconds (0 = the action's own fade). 1800 = half an hour.</summary>
    public int FadeSeconds { get; set; }
}

public sealed class ScheduleSettings
{
    /// <summary>Master switch: false = nothing in the schedule runs.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Location for sunrise/sunset (default: the middle of the Netherlands).</summary>
    public double Latitude { get; set; } = 52.09;
    public double Longitude { get; set; } = 5.12;
    public List<ScheduleItem> Items { get; set; } = new();
}

/// <summary>Works out when schedule items fire. Pure: the app's timer asks <see cref="Due"/> every second.</summary>
public static class Scheduler
{
    /// <summary>When <paramref name="item"/> fires on <paramref name="day"/> (local time), or null when not that day.</summary>
    public static DateTime? At(ScheduleItem item, ScheduleSettings s, DateTime day)
    {
        day = day.Date;
        if (item.Date != null)
        {
            if (!DateTime.TryParse(item.Date, out DateTime only) || only.Date != day) return null;
        }
        else if (item.Days is { Count: > 0 } && !item.Days.Contains(IsoDay(day))) return null;

        switch (item.Kind)
        {
            case ScheduleTimeKind.Sunrise:
            case ScheduleTimeKind.Sunset:
            {
                (DateTime? rise, DateTime? set) = SunTimes.For(day, s.Latitude, s.Longitude);
                DateTime? sun = item.Kind == ScheduleTimeKind.Sunrise ? rise : set;
                return sun?.AddMinutes(item.OffsetMinutes);
            }
            default:
                return TimeSpan.TryParse(item.Time, out TimeSpan t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1) ? day + t : null;
        }
    }

    /// <summary>Monday = 1 ... Sunday = 7.</summary>
    public static int IsoDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>
    /// Items whose moment lies in (<paramref name="from"/>, <paramref name="to"/>]. A gap longer than
    /// <paramref name="maxCatchUp"/> (the PC slept) only fires what happened in the last part of it, so
    /// waking up in the morning does not replay last night's whole schedule.
    /// </summary>
    public static List<(ScheduleItem Item, DateTime At)> Due(ScheduleSettings s, DateTime from, DateTime to, TimeSpan? maxCatchUp = null) =>
        DueUtc(s, ToUtc(from), ToUtc(to), maxCatchUp);

    /// <summary>
    /// Like <see cref="Due"/>, on a UTC window (what the app's timer uses): clock changes for daylight saving
    /// neither repeat nor skip anything. Days around the window are checked too, so "sunset + 4 h" (after
    /// midnight) or "sunrise - 1 h" (before midnight) still fire.
    /// </summary>
    public static List<(ScheduleItem Item, DateTime At)> DueUtc(ScheduleSettings s, DateTime fromUtc, DateTime toUtc, TimeSpan? maxCatchUp = null, TimeZoneInfo zone = null)
    {
        var list = new List<(ScheduleItem, DateTime)>();
        if (s == null || !s.Enabled || toUtc <= fromUtc) return list;
        zone ??= TimeZoneInfo.Local;
        TimeSpan limit = maxCatchUp ?? TimeSpan.FromMinutes(2);
        if (toUtc - fromUtc > limit) fromUtc = toUtc - limit;
        DateTime firstDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone).Date.AddDays(-1);
        DateTime lastDay = TimeZoneInfo.ConvertTimeFromUtc(toUtc, zone).Date.AddDays(1);
        for (DateTime day = firstDay; day <= lastDay; day = day.AddDays(1))
            foreach (ScheduleItem item in s.Items)
            {
                if (!item.Enabled || At(item, s, day) is not DateTime at) continue;
                DateTime atUtc = ToUtc(at, zone);
                if (atUtc > fromUtc && atUtc <= toUtc && !list.Any(x => x.Item1 == item && x.Item2 == at)) list.Add((item, at));
            }
        return list.OrderBy(x => x.Item2).ToList();
    }

    /// <summary>Local wall-clock time to UTC; a time skipped by the spring clock change counts as the hour after.</summary>
    public static DateTime ToUtc(DateTime local, TimeZoneInfo zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        if (local.Kind == DateTimeKind.Utc) return local;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>The next moments things will happen (for the UI), up to <paramref name="days"/> ahead.</summary>
    public static List<(ScheduleItem Item, DateTime At)> Upcoming(ScheduleSettings s, DateTime now, int count = 10, int days = 8)
    {
        var list = new List<(ScheduleItem, DateTime)>();
        if (s == null) return list;
        for (int d = 0; d <= days; d++)
            foreach (ScheduleItem item in s.Items)
                if (item.Enabled && At(item, s, now.Date.AddDays(d)) is DateTime at && at > now) list.Add((item, at));
        return list.OrderBy(x => x.Item2).Take(count).ToList();
    }
}

/// <summary>Sunrise and sunset (NOAA algorithm, accurate to about a minute), in local time.</summary>
public static class SunTimes
{
    public static (DateTime? Sunrise, DateTime? Sunset) For(DateTime day, double latitude, double longitude, TimeZoneInfo zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        double? rise = UtcMinutes(day, latitude, longitude, true), set = UtcMinutes(day, latitude, longitude, false);
        DateTime ToLocal(double m) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(day.Date, DateTimeKind.Utc).AddMinutes(m), zone);
        return (rise is double r ? ToLocal(r) : null, set is double s ? ToLocal(s) : null);
    }

    /// <summary>Minutes after 00:00 UTC on that date, or null when the sun does not rise/set (polar).</summary>
    static double? UtcMinutes(DateTime day, double lat, double lon, bool rise)
    {
        int n = day.DayOfYear;
        double gamma = 2 * Math.PI / (DateTime.IsLeapYear(day.Year) ? 366 : 365) * (n - 1 + 0.5);
        double eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma)
                         - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));
        double decl = 0.006918 - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma) - 0.006758 * Math.Cos(2 * gamma)
                      + 0.000907 * Math.Sin(2 * gamma) - 0.002697 * Math.Cos(3 * gamma) + 0.00148 * Math.Sin(3 * gamma);
        double latRad = lat * Math.PI / 180;
        double cosHa = Math.Cos(90.833 * Math.PI / 180) / (Math.Cos(latRad) * Math.Cos(decl)) - Math.Tan(latRad) * Math.Tan(decl);
        if (cosHa < -1 || cosHa > 1) return null;
        double ha = Math.Acos(cosHa) * 180 / Math.PI;
        return 720 - 4 * (lon + (rise ? ha : -ha)) - eqTime;
    }
}
