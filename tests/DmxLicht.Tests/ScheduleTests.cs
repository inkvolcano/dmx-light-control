using DmxLicht.Core.Control;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class ScheduleTests
{
    static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void ActionsCanFadeSlowly()
    {
        var show = new ShowFile { Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } } };
        var output = new CaptureOutput();
        using var e = new LightEngine(show, output, null);
        e.Start();
        Thread.Sleep(150);
        Assert.Equal(0, output.Last[1]);
        // "over 1.6 s from dark to red" (the schedule uses minutes, the mechanism is the same)
        Assert.Equal("ok", Commands.Run(e, "color red", 1600));
        Thread.Sleep(800);
        Assert.InRange(output.Last[1], 40, 215);                // halfway: partly on
        Thread.Sleep(1200);
        Assert.Equal(255, output.Last[1]);                       // done
        // master too, and without an override things stay instant
        Assert.Equal("ok", Commands.Run(e, "master 0", 1600));
        Thread.Sleep(800);
        Assert.InRange(output.Last[1], 40, 215);
        Assert.Equal("ok", Commands.Run(e, "master 100"));
        Thread.Sleep(150);
        Assert.Equal(255, output.Last[1]);
    }

    [Fact]
    public void ADeliberateChangeEndsALongFade()
    {
        var show = new ShowFile { Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } } };
        var output = new CaptureOutput();
        using var e = new LightEngine(show, output, null);
        e.Start();
        Assert.Equal("ok", Commands.Run(e, "color red"));
        Thread.Sleep(150);
        Assert.Equal("ok", Commands.Run(e, "off", 30 * 60 * 1000));   // schedule: off over 30 minutes
        Thread.Sleep(200);
        Assert.True(output.Last[1] > 200);                               // still nearly on
        Assert.Equal("ok", Commands.Run(e, "blackout on"));              // the user presses blackout
        Thread.Sleep(150);
        Assert.Equal(0, output.Last[1]);                                 // dark now, not in 30 minutes
    }

    [Fact]
    public void SunOffsetsAcrossMidnightAndDaylightSaving()
    {
        // "sunset + 6 h" lands after midnight: it must still fire (the day before is checked too)
        var late = new ScheduleItem { Kind = ScheduleTimeKind.Sunset, OffsetMinutes = 6 * 60, Command = "off" };
        var s = Plan(late);
        DateTime day = new(2026, 6, 20);
        DateTime at = Scheduler.At(late, s, day)!.Value;
        Assert.True(at.Date == day.AddDays(1));
        Assert.Single(Scheduler.Due(s, at.AddSeconds(-1), at.AddSeconds(1)));

        // autumn clock change (Amsterdam, 25 Oct 2026, 03:00 -> 02:00): 02:30 runs once, not twice
        var night = new ScheduleItem { Time = "02:30", Command = "off" };
        var plan = Plan(night);
        DateTime fromUtc = new(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc);  // 01:00 CEST
        int fired = 0;
        for (DateTime t = fromUtc; t < fromUtc.AddHours(4); t = t.AddSeconds(30))
            fired += Scheduler.DueUtc(plan, t, t.AddSeconds(30), null, Amsterdam).Count;
        Assert.Equal(1, fired);
        // spring: 02:30 does not exist on 29 Mar 2026; it runs at 03:30 summer time instead of being lost
        fromUtc = new(2026, 3, 28, 23, 0, 0, DateTimeKind.Utc);
        fired = 0;
        for (DateTime t = fromUtc; t < fromUtc.AddHours(4); t = t.AddSeconds(30))
            fired += Scheduler.DueUtc(plan, t, t.AddSeconds(30), null, Amsterdam).Count;
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SunTimesForUtrecht()
    {
        // reference (timeanddate.com, Utrecht): 21 Jun ~05:19 / 22:06 CEST, 21 Dec ~08:45 / 16:28 CET
        var (rise, set) = SunTimes.For(new DateTime(2026, 6, 21), 52.09, 5.12, Amsterdam);
        Assert.InRange(rise!.Value.TimeOfDay.TotalMinutes, 5 * 60 + 15, 5 * 60 + 24);
        Assert.InRange(set!.Value.TimeOfDay.TotalMinutes, 22 * 60 + 1, 22 * 60 + 10);
        (rise, set) = SunTimes.For(new DateTime(2026, 12, 21), 52.09, 5.12, Amsterdam);
        Assert.InRange(rise!.Value.TimeOfDay.TotalMinutes, 8 * 60 + 40, 8 * 60 + 50);
        Assert.InRange(set!.Value.TimeOfDay.TotalMinutes, 16 * 60 + 23, 16 * 60 + 33);
        // midnight sun: no sunset in Tromsø in June
        Assert.Null(SunTimes.For(new DateTime(2026, 6, 21), 69.65, 18.96, Amsterdam).Sunset);
    }

    static ScheduleSettings Plan(params ScheduleItem[] items)
    {
        var s = new ScheduleSettings();
        s.Items.AddRange(items);
        return s;
    }

    [Fact]
    public void DaysTimesAndOnceDates()
    {
        var weekdays = new ScheduleItem { Time = "18:00", Days = { 1, 2, 3, 4, 5 } };
        var once = new ScheduleItem { Time = "20:30", Date = "2026-11-21" };
        ScheduleSettings s = Plan(weekdays, once);
        Assert.Equal(new DateTime(2026, 11, 20, 18, 0, 0), Scheduler.At(weekdays, s, new DateTime(2026, 11, 20)));  // Friday
        Assert.Null(Scheduler.At(weekdays, s, new DateTime(2026, 11, 21)));                                         // Saturday
        Assert.Equal(new DateTime(2026, 11, 21, 20, 30, 0), Scheduler.At(once, s, new DateTime(2026, 11, 21)));
        Assert.Null(Scheduler.At(once, s, new DateTime(2026, 11, 22)));
        Assert.Null(Scheduler.At(new ScheduleItem { Time = "25:00" }, s, new DateTime(2026, 11, 20)));
    }

    [Fact]
    public void FiresOnceWhenItsMomentPasses()
    {
        var item = new ScheduleItem { Time = "18:00", Command = "scene 1" };
        ScheduleSettings s = Plan(item);
        DateTime t = new(2026, 11, 20, 17, 59, 59);
        Assert.Empty(Scheduler.Due(s, t, t.AddSeconds(0.5)));
        Assert.Single(Scheduler.Due(s, t.AddSeconds(0.5), t.AddSeconds(1.5)));   // 18:00:00 lies in this tick
        Assert.Empty(Scheduler.Due(s, t.AddSeconds(1.5), t.AddSeconds(2.5)));    // not again
        item.Enabled = false;
        Assert.Empty(Scheduler.Due(s, t, t.AddSeconds(2)));
        item.Enabled = true; s.Enabled = false;
        Assert.Empty(Scheduler.Due(s, t, t.AddSeconds(2)));
    }

    [Fact]
    public void AfterSleepOnlyRecentItemsFire()
    {
        var evening = new ScheduleItem { Time = "23:00", Command = "off" };
        var morning = new ScheduleItem { Time = "07:00", Command = "on" };
        ScheduleSettings s = Plan(evening, morning);
        // PC slept from 22:00 until 07:01 the next day: only 07:00 (within the catch-up window) runs
        var due = Scheduler.Due(s, new DateTime(2026, 11, 20, 22, 0, 0), new DateTime(2026, 11, 21, 7, 1, 0));
        Assert.Equal(new[] { "on" }, due.Select(d => d.Item.Command));
        // across midnight in normal ticks
        Assert.Single(Scheduler.Due(Plan(new ScheduleItem { Time = "00:00" }), new DateTime(2026, 11, 20, 23, 59, 59), new DateTime(2026, 11, 21, 0, 0, 1)));
    }

    [Fact]
    public void SunsetWithOffsetAndUpcoming()
    {
        var item = new ScheduleItem { Kind = ScheduleTimeKind.Sunset, OffsetMinutes = -30, Command = "on" };
        ScheduleSettings s = Plan(item, new ScheduleItem { Time = "23:30", Command = "off" });
        DateTime day = new(2026, 11, 20);
        DateTime set = SunTimes.For(day, s.Latitude, s.Longitude).Sunset!.Value;
        Assert.Equal(set.AddMinutes(-30), Scheduler.At(item, s, day));
        var next = Scheduler.Upcoming(s, day.AddHours(12), 3);
        Assert.Equal(new[] { "on", "off", "on" }, next.Select(n => n.Item.Command));
        Assert.True(next.Zip(next.Skip(1)).All(p => p.First.At < p.Second.At));
    }
}
