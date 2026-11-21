using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DmxLicht.Core;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Legacy;
using DmxLicht.Core.Output;
using DmxLicht.Core.Storage;
using Microsoft.Extensions.FileProviders;

namespace DmxLicht.App;

/// <summary>
/// Starts the engine and a local web server, then shows the UI in a window (WebView2).
/// Options: --browser (open in the default browser instead of a window), --headless (no window at all).
/// Everything is stored in the "Instellingen" folder next to the exe, so the folder is portable.
/// </summary>
static class Program
{
    static readonly string FixturesDir = Path.Combine(Log.Dir, "Fixtures");
    public static readonly string AudioDir = Path.Combine(Log.Dir, "Audio");
    public static (DmxLicht.Core.Control.OscServer osc, MidiControl midi, Action<DmxLicht.Core.Control.ControlSettings> apply) Controls;

    /// <summary>Closes the app cleanly (show saved, web server stopped); used by the updater.</summary>
    public static Action ExitApp { get; private set; } = () => Environment.Exit(0);

    /// <summary>The screen/camera sampler (for the camera preview).</summary>
    public static ScreenSampler Screen { get; private set; }

    static WebApplication web;
    static int webPort;
    /// <summary>The port the web server really listens on (the setting, or a free one).</summary>
    public static int WebPort => webPort;
    static Func<bool, WebApplication> rebuildWeb;

    /// <summary>
    /// Restarts only the web server (same port), e.g. to open it to phones on the network. The light keeps
    /// running; the window and phones reconnect by themselves. Runs after the current request has finished.
    /// </summary>
    static readonly SemaphoreSlim webRestart = new(1, 1);
    static bool wantNetwork;

    public static void RestartWebSoon(bool allowNetwork) => Task.Run(async () =>
    {
        wantNetwork = allowNetwork;
        await Task.Delay(400);
        await webRestart.WaitAsync();
        try
        {
            allowNetwork = wantNetwork;   // the latest toggle wins
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3))) await web.StopAsync(cts.Token);
            await web.DisposeAsync();
            web = rebuildWeb(allowNetwork);
            await web.StartAsync();
            Log.Write("Webserver opnieuw gestart (" + (allowNetwork ? "ook vanaf het netwerk" : "alleen deze pc") + ") op poort " + webPort);
        }
        catch (Exception ex)
        {
            Log.Write("Webserver opnieuw starten mislukt: " + ex.Message + " - ik probeer alleen deze pc");
            try { web = rebuildWeb(false); await web.StartAsync(); } catch (Exception ex2) { Log.Crash("webserver", ex2); }
        }
        finally { webRestart.Release(); }
    });

    [STAThread]
    static void Main(string[] args)
    {
        bool headless = args.Contains("--headless");
        bool browser = args.Contains("--browser");

        // One DmxLicht per PC: two programs sending to one dongle make it drop out.
        using var single = new Mutex(true, @"Local\DmxLicht.Output", out bool firstInstance);
        if (!firstInstance && !args.Contains("--allow-second"))
        {
            const string msg = "DmxLicht draait al op deze pc (kijk in de taakbalk of bij de klok).\n" +
                               "DmxLicht is already running on this PC.";
            if (headless) Console.Error.WriteLine(msg);
            else MessageBox.Show(msg, "DmxLicht", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        // every crash lands in Instellingen\Crashes (and in the problem report)
        Application.ThreadException += (_, e) => Log.Crash("venster", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Crash(e.IsTerminating ? "app (afgesloten)" : "app", e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Crash("achtergrondtaak", e.Exception); e.SetObserved(); };
        Log.Write("DmxLicht " + Log.AppVersion + " start op " + Environment.OSVersion);

        ShowFile show = LoadShow();
        var udmx = new UdmxOutput { MinIntervalMs = show.Settings.DongleIntervalMs };
        var output = new MultiOutput(udmx);
        output.Apply(show.Settings.Outputs ?? new OutputSettings());
        bool oscOn = false;
        if (show.Settings.PreventSleep) KeepAwake.On();
        using var engine = new LightEngine(show, output, FixturesDir);

        var saver = new AutoSaver(engine, ShowFile.DefaultPath);
        engine.Changed += saver.Touch;

        engine.UseTimelineClock(t => SongClock.For(t, AudioDir));

        show.Settings.Control ??= new DmxLicht.Core.Control.ControlSettings();
        using var osc = new DmxLicht.Core.Control.OscServer(engine);
        using var midi = new MidiControl(engine);
        void ApplyControl(DmxLicht.Core.Control.ControlSettings c)
        {
            if (c.OscEnabled && (osc.Port != c.OscPort || osc.Error != null || !oscOn)) { osc.Start(c.OscPort); oscOn = true; }
            if (!c.OscEnabled && oscOn) { osc.Stop(); oscOn = false; }
            midi.Apply(c);
        }
        ApplyControl(show.Settings.Control);
        Controls = (osc, midi, ApplyControl);

        using var schedule = new ScheduleRunner(engine);
        using var screenSampler = new ScreenSampler(engine);
        Screen = screenSampler;
        using var homeAssistant = new HomeAssistant(engine);
        using var mdns = new MdnsResponder();
        mdns.Apply(show.Settings.AllowNetwork, show.Settings.HostName);
        engine.Changed += () => { var st = engine.Show.Settings; mdns.Apply(st.AllowNetwork, st.HostName); };
        using var audio = new AudioInput(engine);
        audio.Apply(show.State.Music);
        engine.Changed += () => audio.Apply(engine.Read(s => s.State.Music));

        int port = FreePort(show.Settings.Port);
        webPort = port;
        rebuildWeb = allow => BuildWeb(engine, output, audio, allow, port);
        web = rebuildWeb(show.Settings.AllowNetwork);
        web.StartAsync().GetAwaiter().GetResult();
        engine.Start();
        string url = $"http://127.0.0.1:{port}/";
        Updater.CleanUp();
        // new version on GitHub? check shortly after start and then daily (only shows a notice; installing is a click)
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            while (true)
            {
                if (engine.Show.Settings.CheckUpdates) await Updater.CheckAsync();
                await Task.Delay(TimeSpan.FromHours(24));
            }
        });
        Log.Write("Gestart op " + url);

        try
        {
            if (headless)
            {
                Console.WriteLine("DmxLicht draait op " + url + " (Ctrl+C om te stoppen)");
                var done = new ManualResetEventSlim();
                ExitApp = done.Set;
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
                done.Wait();
            }
            else if (browser)
            {
                OpenInBrowser(url);
                var invoker = new Control(); invoker.CreateControl();
                ExitApp = () => invoker.BeginInvoke(Application.Exit);
                Application.Run(new TrayContext(url));
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var invoker = new Control(); invoker.CreateControl();
                ExitApp = () => invoker.BeginInvoke(Application.Exit);
                Application.Run(new MainWindow(url));
            }
        }
        finally
        {
            // safety net: once the show is saved the app always ends, even if a device or library hangs on
            // shutdown (an updater waiting for us must never wait forever)
            new Thread(() => { Thread.Sleep(10000); Log.Write("Afsluiten duurde te lang: app wordt beëindigd"); Environment.Exit(0); })
                { IsBackground = true, Name = "exit watchdog" }.Start();
            saver.Flush();
            web.StopAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>show.json; else take over v1.4's settings if they sit next to the exe; else empty.</summary>
    static ShowFile LoadShow()
    {
        ShowLibrary.AutoBackup(ShowFile.DefaultPath);
        ShowFile show = ShowFile.Load(ShowFile.DefaultPath);
        if (show != null) return show;
        show = LegacyImport.TryImport(Log.Dir, FixturesDir) ?? new ShowFile();
        try { show.Save(ShowFile.DefaultPath); } catch (Exception ex) { Log.Write("Opslaan mislukt: " + ex.Message); }
        return show;
    }

    static WebApplication BuildWeb(LightEngine engine, MultiOutput output, AudioInput audio, bool allowNetwork, int port)
    {
        string webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot,
        });
        builder.Logging.ClearProviders();
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 300_000_000);
        // closing or restarting the web part never waits long for open pages (default is 30 s)
        builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(3));
        builder.WebHost.UseKestrel(k =>
        {
            k.Limits.MaxRequestBodySize = 300_000_000; // songs
            if (allowNetwork) k.ListenAnyIP(port);
            else k.Listen(IPAddress.Loopback, port);
        });
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            foreach (var c in Json.Compact.Converters) o.SerializerOptions.Converters.Add(c);
            o.SerializerOptions.PropertyNamingPolicy = Json.Compact.PropertyNamingPolicy;
        });

        WebApplication app = builder.Build();
        app.UseWebSockets();
        if (Directory.Exists(webRoot))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });
        }
        Api.Map(app, engine, audio, settings =>
        {
            output.Udmx.MinIntervalMs = settings.DongleIntervalMs;
            output.Apply(settings.Outputs ?? new OutputSettings());
            if (settings.Control != null) Controls.apply(settings.Control);
            else settings.Control = engine.Show.Settings.Control;
            // network access: the web server restarts by itself; port / keep-awake still need an app restart
            if (settings.AllowNetwork != engine.Show.Settings.AllowNetwork && settings.Port == engine.Show.Settings.Port)
                RestartWebSoon(settings.AllowNetwork);
            bool restart = settings.Port != engine.Show.Settings.Port || settings.PreventSleep != engine.Show.Settings.PreventSleep;
            return restart ? "restart" : settings.AllowNetwork != engine.Show.Settings.AllowNetwork ? "network" : null;
        });
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>The preferred port, or a free one if something else uses it.</summary>
    static int FreePort(int preferred)
    {
        try
        {
            var l = new TcpListener(IPAddress.Loopback, preferred);
            l.Start(); l.Stop();
            return preferred;
        }
        catch (SocketException)
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }
    }

    public static void OpenInBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Browser openen mislukt: " + ex.Message); }
    }
}

/// <summary>Keeps Windows from sleeping while the app runs (sleep would cut the USB dongle).</summary>
static class KeepAwake
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern uint SetThreadExecutionState(uint flags);
    const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x00000001;

    // Called from the main thread, which lives as long as the app.
    public static void On() => SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
    public static void Off() => SetThreadExecutionState(ES_CONTINUOUS);
}

/// <summary>Saves show.json at most once per second after changes.</summary>
sealed class AutoSaver
{
    readonly LightEngine engine;
    readonly string path;
    readonly System.Threading.Timer timer;
    int dirty;

    public AutoSaver(LightEngine engine, string path)
    {
        this.engine = engine;
        this.path = path;
        timer = new System.Threading.Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Touch()
    {
        Interlocked.Exchange(ref dirty, 1);
        timer.Change(1000, Timeout.Infinite);
    }

    readonly object fileLock = new();

    public void Flush()
    {
        if (Interlocked.Exchange(ref dirty, 0) == 0) return;
        try
        {
            // serialise under the engine lock (fast), write the file outside it: disk, antivirus or
            // OneDrive must never hold up the light
            string json = engine.Read(s => s.ToJson());
            lock (fileLock) ShowFile.WriteJson(path, json);
        }
        catch (Exception ex)
        {
            Log.Write("Opslaan mislukt (ik probeer het over 5 s opnieuw): " + ex.Message);
            Interlocked.Exchange(ref dirty, 1);
            timer.Change(5000, Timeout.Infinite);
        }
    }
}
