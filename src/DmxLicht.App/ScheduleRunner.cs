using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;

namespace DmxLicht.App;

/// <summary>Checks the schedule every second and runs what is due through the command language.</summary>
sealed class ScheduleRunner : IDisposable
{
    readonly LightEngine engine;
    readonly System.Threading.Timer timer;
    DateTime last = DateTime.UtcNow;
    int busy;

    /// <summary>What ran last (for the UI).</summary>
    public static (string Name, string Command, string Result, DateTime At)? LastRun { get; private set; }

    public ScheduleRunner(LightEngine engine)
    {
        this.engine = engine;
        timer = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
    }

    void Tick()
    {
        if (Interlocked.Exchange(ref busy, 1) == 1) return;
        try
        {
            DateTime now = DateTime.UtcNow;   // UTC: daylight-saving clock changes neither repeat nor skip items
            ScheduleSettings plan = engine.Read(s => s.Schedule);
            List<(ScheduleItem Item, DateTime At)> due = Scheduler.DueUtc(plan, last, now);
            last = now;
            foreach (var (item, _) in due) Run(engine, item);
        }
        catch (Exception ex) { Log.Write("Tijdschema fout: " + ex.Message); }
        finally { Volatile.Write(ref busy, 0); }
    }

    public static string Run(LightEngine engine, ScheduleItem item)
    {
        string result = Commands.Run(engine, item.Command ?? "", item.FadeSeconds > 0 ? item.FadeSeconds * 1000 : null);
        Log.Write("Tijdschema: " + (string.IsNullOrWhiteSpace(item.Name) ? item.Command : item.Name + " (" + item.Command + ")") + " -> " + result);
        LastRun = (item.Name, item.Command, result, DateTime.Now);
        return result;
    }

    public void Dispose() => timer.Dispose();
}
