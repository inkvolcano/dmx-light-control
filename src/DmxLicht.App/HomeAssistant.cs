using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DmxLicht.Core;
using DmxLicht.Core.Control;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;
using MQTTnet;
using MQTTnet.Client;

namespace DmxLicht.App;

/// <summary>
/// Home Assistant over MQTT with discovery: the light, groups, scenes, effects, blackout, master, buttons
/// and the dongle appear by themselves under one device "DmxLicht". Everything sent to the app goes through
/// the command language. Raw commands: publish e.g. <c>scene "Sfeer"</c> to <c>dmxlicht/command</c>.
/// </summary>
sealed class HomeAssistant : IDisposable
{
    readonly LightEngine engine;
    readonly CancellationTokenSource stop = new();
    readonly SemaphoreSlim wake = new(0);
    readonly Dictionary<string, string> published = new();   // topic -> last payload (state and config)
    IMqttClient client;
    MqttSettings active;
    string shape;                                             // what the discovery configs were built from

    public static string Status { get; private set; } = "off";
    public static string Error { get; private set; }

    public HomeAssistant(LightEngine engine)
    {
        this.engine = engine;
        engine.Changed += () => wake.Release();
        _ = Task.Run(Loop);
    }

    public void SettingsChanged() => wake.Release();

    async Task Loop()
    {
        DateTime lastState = DateTime.MinValue;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                MqttSettings want = engine.Read(s => SceneOps.Clone(s.Settings.Mqtt ?? new MqttSettings()));
                if (!want.Enabled || string.IsNullOrWhiteSpace(want.Host))
                {
                    await Disconnect();
                    Status = "off"; Error = null;
                }
                else
                {
                    if (client?.IsConnected != true || !Same(want, active)) await Connect(want);
                    if (client?.IsConnected == true)
                    {
                        await PublishDiscovery(false);
                        await PublishState();
                        lastState = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Error != ex.Message) Log.Write("Home Assistant (MQTT): " + ex.Message);
                Error = ex.Message;
                Status = "error";
                await Disconnect();
                await Task.Delay(5000, stop.Token).ContinueWith(_ => { });
                continue;
            }
            // wait for a change, or refresh every 5 s (dongle status, BPM)
            await wake.WaitAsync(TimeSpan.FromSeconds(5), stop.Token).ContinueWith(_ => { });
            await Task.Delay(150).ContinueWith(_ => { });   // gather bursts (slider drags) into one update
            while (wake.CurrentCount > 0) wake.Wait(0);
        }
        await Disconnect();
    }

    static bool Same(MqttSettings a, MqttSettings b) => b != null && a.Host == b.Host && a.Port == b.Port && a.User == b.User
        && a.Password == b.Password && a.Prefix == b.Prefix && a.Discovery == b.Discovery && a.Name == b.Name;

    string P => string.IsNullOrWhiteSpace(active?.Prefix) ? "dmxlicht" : active.Prefix.Trim('/');
    string D => string.IsNullOrWhiteSpace(active?.Discovery) ? "homeassistant" : active.Discovery.Trim('/');

    async Task Connect(MqttSettings s)
    {
        await Disconnect();
        active = s;
        published.Clear();
        shape = null;
        Status = "connecting";
        client = new MqttFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            try { Handle(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)); }
            catch (Exception ex) { Log.Write("Home Assistant opdracht mislukt: " + ex.Message); }
            return Task.CompletedTask;
        };
        client.DisconnectedAsync += _ => { if (Status == "connected") { Status = "connecting"; Log.Write("Home Assistant (MQTT): verbinding weg"); } wake.Release(); return Task.CompletedTask; };

        var b = new MqttClientOptionsBuilder()
            .WithTcpServer(s.Host.Trim(), s.Port <= 0 ? 1883 : s.Port)
            .WithClientId("dmxlicht-" + Environment.MachineName.ToLowerInvariant())
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithWillTopic(P + "/status").WithWillPayload("offline").WithWillRetain(true);
        if (!string.IsNullOrEmpty(s.User)) b = b.WithCredentials(s.User, s.Password ?? "");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        MqttClientConnectResult r = await client.ConnectAsync(b.Build(), timeout.Token);
        if (r.ResultCode != MqttClientConnectResultCode.Success) throw new InvalidOperationException("broker weigert: " + r.ResultCode);
        await client.SubscribeAsync(new MqttFactory().CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(P + "/+/set"))
            .WithTopicFilter(f => f.WithTopic(P + "/+/press"))
            .WithTopicFilter(f => f.WithTopic(P + "/command"))
            .WithTopicFilter(f => f.WithTopic(D + "/status"))      // HA restarted: send everything again
            .Build());
        await Publish(P + "/status", "online", true);
        Status = "connected"; Error = null;
        Log.Write("Home Assistant (MQTT) verbonden met " + s.Host);
    }

    async Task Disconnect()
    {
        if (client == null) return;
        try { if (client.IsConnected) { await Publish(P + "/status", "offline", true); await client.DisconnectAsync(); } } catch { }
        client.Dispose();
        client = null;
    }

    async Task Publish(string topic, string payload, bool retain, bool onlyIfChanged = false)
    {
        if (onlyIfChanged && published.TryGetValue(topic, out string old) && old == payload) return;
        published[topic] = payload;
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).WithRetainFlag(retain).Build(), stop.Token);
    }

    // ------------------------------------------------------------------ discovery

    sealed record GroupName(string Id, string Name);
    sealed record Names(List<string> Scenes, List<GroupName> Groups, List<string> Effects, List<string> Timelines);

    Names Current() => engine.Read(s => new Names(
        s.Scenes.Select(x => x.Name).ToList(),
        s.Groups.Select(g => new GroupName(g.Id, g.Name)).ToList(),
        EffectNames(s),
        s.Timelines.Select(t => t.Name).ToList()));

    static List<string> EffectNames(ShowFile s) =>
        s.State.Chases.Select(c => c.Name).Append(ScreenName).Concat(EffectLibrary.All.Select(e => e.Name)).Distinct().ToList();

    const string ScreenName = "Scherm volgen";
    const string NoScene = "-";

    async Task PublishDiscovery(bool force)
    {
        Names n = Current();
        string now = JsonSerializer.Serialize(n);
        if (!force && now == shape) return;
        string oldShape = shape;
        shape = now;

        JsonObject Device() => new()
        {
            ["identifiers"] = new JsonArray("dmxlicht-" + Environment.MachineName.ToLowerInvariant()),
            ["name"] = string.IsNullOrWhiteSpace(active.Name) ? "DmxLicht" : active.Name,
            ["manufacturer"] = "DmxLicht",
            ["model"] = "DMX lichtbesturing",
            ["sw_version"] = Log.AppVersion,
        };
        JsonObject Base(string component, string id, string name) => new()
        {
            ["name"] = name,
            ["unique_id"] = "dmxlicht_" + Environment.MachineName.ToLowerInvariant() + "_" + id,
            ["default_entity_id"] = component + ".dmxlicht_" + id,
            ["availability_topic"] = P + "/status",
            ["device"] = Device(),
        };
        async Task Config(string component, string id, JsonObject cfg) =>
            await Publish($"{D}/{component}/dmxlicht_{Environment.MachineName.ToLowerInvariant()}/{id}/config", cfg.ToJsonString(), true);

        JsonObject light = Base("light", "light", "Licht");
        light["schema"] = "json";
        light["command_topic"] = P + "/light/set";
        light["state_topic"] = P + "/light/state";
        light["brightness"] = true;
        light["supported_color_modes"] = new JsonArray("rgb");
        light["effect"] = true;
        light["effect_list"] = new JsonArray(n.Effects.Select(e => (JsonNode)e).ToArray());
        await Config("light", "light", light);

        foreach (var (id, name) in n.Groups.Select(g => (g.Id, g.Name)))
        {
            JsonObject g = Base("light", "group_" + id, name);
            g["schema"] = "json";
            g["command_topic"] = $"{P}/group_{id}/set";
            g["state_topic"] = $"{P}/group_{id}/state";
            g["supported_color_modes"] = new JsonArray("rgb");
            await Config("light", "group_" + id, g);
        }
        // groups that no longer exist: remove from Home Assistant
        if (oldShape != null)
            foreach (string id in JsonSerializer.Deserialize<Names>(oldShape)!.Groups.Where(o => o?.Id != null && n.Groups.All(g => g.Id != o.Id)).Select(o => o.Id))
                await Publish($"{D}/light/dmxlicht_{Environment.MachineName.ToLowerInvariant()}/group_{id}/config", "", true);

        JsonObject scene = Base("select", "scene", "Scène");
        scene["command_topic"] = P + "/scene/set";
        scene["state_topic"] = P + "/scene/state";
        scene["options"] = new JsonArray(n.Scenes.Prepend(NoScene).Distinct().Select(x => (JsonNode)x).ToArray());
        scene["icon"] = "mdi:palette";
        await Config("select", "scene", scene);

        JsonObject effect = Base("select", "effect", "Effect");
        effect["command_topic"] = P + "/effect/set";
        effect["state_topic"] = P + "/effect/state";
        effect["options"] = new JsonArray(n.Effects.Prepend(NoScene).Select(x => (JsonNode)x).ToArray());
        effect["icon"] = "mdi:auto-fix";
        await Config("select", "effect", effect);

        JsonObject master = Base("number", "master", "Helderheid");
        master["command_topic"] = P + "/master/set";
        master["state_topic"] = P + "/master/state";
        master["min"] = 0; master["max"] = 100; master["step"] = 1;
        master["unit_of_measurement"] = "%";
        master["mode"] = "slider";
        master["icon"] = "mdi:brightness-6";
        await Config("number", "master", master);

        JsonObject blackout = Base("switch", "blackout", "Blackout");
        blackout["command_topic"] = P + "/blackout/set";
        blackout["state_topic"] = P + "/blackout/state";
        blackout["icon"] = "mdi:lightbulb-off";
        await Config("switch", "blackout", blackout);

        JsonObject strobe = Base("switch", "strobe", "Strobo");
        strobe["command_topic"] = P + "/strobe/set";
        strobe["state_topic"] = P + "/strobe/state";
        strobe["icon"] = "mdi:flash";
        await Config("switch", "strobe", strobe);

        foreach (var (id, name, icon) in new[] { ("stop", "Effecten stoppen", "mdi:stop"), ("timeline_play", "Tijdlijn start / GO", "mdi:play"),
                                                 ("timeline_stop", "Tijdlijn stop", "mdi:stop-circle"), ("tap", "Tap tempo", "mdi:gesture-tap") })
        {
            JsonObject btn = Base("button", id, name);
            btn["command_topic"] = $"{P}/{id}/press";
            btn["icon"] = icon;
            await Config("button", id, btn);
        }

        JsonObject dongle = Base("binary_sensor", "dongle", "Dongle");
        dongle["state_topic"] = P + "/dongle/state";
        dongle["device_class"] = "connectivity";
        dongle["entity_category"] = "diagnostic";
        await Config("binary_sensor", "dongle", dongle);

        JsonObject bpm = Base("sensor", "bpm", "BPM");
        bpm["state_topic"] = P + "/bpm/state";
        bpm["icon"] = "mdi:metronome";
        bpm["state_class"] = "measurement";
        await Config("sensor", "bpm", bpm);

        JsonObject cmd = Base("text", "command", "Opdracht");
        cmd["command_topic"] = P + "/command";
        cmd["icon"] = "mdi:console";
        cmd["mode"] = "text";
        await Config("text", "command", cmd);

        published.Keys.Where(k => k.StartsWith(P + "/") && k.EndsWith("/state")).ToList().ForEach(k => published.Remove(k));  // resend states
    }

    // ------------------------------------------------------------------ state

    async Task PublishState()
    {
        var st = engine.Read(s =>
        {
            ShowState state = s.State;
            bool anyOn = state.Mode == OutputMode.Effect || state.Mode == OutputMode.Faders || state.Looks.Values.Any(l => l.On);
            Color c = state.Looks.Values.FirstOrDefault(l => l.On)?.Color.Resolve().Show ?? Color.White;
            string effect = state.Mode != OutputMode.Effect ? null
                : state.Effect.ScreenSync ? ScreenName
                : state.Effect.ChaseId != null ? state.Chases.FirstOrDefault(x => x.Id == state.Effect.ChaseId)?.Name
                : EffectLibrary.All.ElementAtOrDefault(state.Effect.EffectIndex)?.Name;
            string scene = s.Scenes.FirstOrDefault(x => x.Id == state.ActiveScene)?.Name ?? NoScene;
            var groups = s.Groups.Select(g =>
            {
                FixtureLook l = g.FixtureIds.Select(id => state.Looks.GetValueOrDefault(id)).FirstOrDefault(x => x?.On == true);
                Color gc = l?.Color.Resolve().Show ?? Color.White;
                bool layer = state.Layers.Any(x => x.GroupId == g.Id);
                return (g.Id, On: !state.Blackout && (l != null || layer), Color: gc);
            }).ToList();
            return (On: anyOn && !state.Blackout, Bright: state.BrightnessPercent, Color: c, Effect: effect, Scene: scene,
                    state.Blackout, state.StrobeActive, Groups: groups);
        });

        var light = new JsonObject
        {
            ["state"] = st.On ? "ON" : "OFF",
            ["brightness"] = (int)Math.Round(st.Bright * 255 / 100.0),
            ["color_mode"] = "rgb",
            ["color"] = new JsonObject { ["r"] = st.Color.R, ["g"] = st.Color.G, ["b"] = st.Color.B },
            ["effect"] = st.Effect,
        };
        await Publish(P + "/light/state", light.ToJsonString(), true, true);
        foreach (var g in st.Groups)
        {
            var gs = new JsonObject
            {
                ["state"] = g.On ? "ON" : "OFF",
                ["color_mode"] = "rgb",
                ["color"] = new JsonObject { ["r"] = g.Color.R, ["g"] = g.Color.G, ["b"] = g.Color.B },
            };
            await Publish($"{P}/group_{g.Id}/state", gs.ToJsonString(), true, true);
        }
        await Publish(P + "/scene/state", st.Scene, true, true);
        await Publish(P + "/effect/state", st.Effect ?? NoScene, true, true);
        await Publish(P + "/master/state", st.Bright.ToString(), true, true);
        await Publish(P + "/blackout/state", st.Blackout ? "ON" : "OFF", true, true);
        await Publish(P + "/strobe/state", st.StrobeActive ? "ON" : "OFF", true, true);
        bool dongle = engine.Output is DmxLicht.Core.Output.MultiOutput mo ? mo.Udmx.Connected : engine.Output.Connected;
        await Publish(P + "/dongle/state", dongle ? "ON" : "OFF", true, true);
        await Publish(P + "/bpm/state", Math.Round(engine.Beat.Bpm).ToString(), true, true);
    }

    // ------------------------------------------------------------------ commands from Home Assistant

    void Handle(string topic, string payload)
    {
        payload = payload.Trim();
        if (topic == D + "/status") { if (payload == "online") { shape = null; wake.Release(); } return; }
        if (!topic.StartsWith(P + "/")) return;
        string what = topic[(P.Length + 1)..];
        string Q(string name) => "\"" + name.Replace("\"", "") + "\"";
        void Run(string line) { string r = Commands.Run(engine, line); Log.Write("Home Assistant: " + line + " -> " + r); }

        switch (what)
        {
            case "command": Run(payload); break;
            case "scene/set": if (payload != NoScene) Run("scene " + Q(payload)); break;
            case "effect/set": if (payload == NoScene) Run("stop"); else Run("effect " + EffectArg(payload)); break;
            case "master/set": if (double.TryParse(payload, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double m)) Run("master " + Math.Round(m)); break;
            case "blackout/set": Run("blackout " + (payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? "on" : "off")); break;
            case "strobe/set": Run("strobe " + (payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? "on" : "off")); break;
            case "stop/press": Run("stop"); break;
            case "timeline_play/press": Run("timeline play"); break;
            case "timeline_stop/press": Run("timeline stop"); break;
            case "tap/press": Run("tap"); break;
            case "light/set": LightCommand(payload, null); break;
            default:
                if (what.StartsWith("group_") && what.EndsWith("/set"))
                {
                    string id = what["group_".Length..^"/set".Length];
                    string name = engine.Read(s => s.Groups.FirstOrDefault(g => g.Id == id)?.Name);
                    if (name != null) LightCommand(payload, Q(name));
                }
                break;
        }
        wake.Release();

        void LightCommand(string json, string group)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement j = doc.RootElement;
            bool off = j.TryGetProperty("state", out JsonElement s) && s.GetString()?.Equals("OFF", StringComparison.OrdinalIgnoreCase) == true;
            string g = group == null ? "" : " " + group;
            if (off) { Run("off" + g); return; }
            if (group == null && engine.Read(x => x.State.Blackout)) Run("blackout off");
            bool hasBright = j.TryGetProperty("brightness", out JsonElement b);
            if (hasBright && group == null) Run("master " + Math.Round(b.GetDouble() * 100 / 255));
            bool hasColor = j.TryGetProperty("color", out _), hasEffect = j.TryGetProperty("effect", out _);
            // only the brightness changed on a light that is already on: keep the scene/effect that runs
            if (hasBright && !hasColor && !hasEffect && group == null && engine.Read(x => x.State.Mode == OutputMode.Effect || x.State.Looks.Values.Any(l => l.On))) return;
            if (j.TryGetProperty("effect", out JsonElement e) && e.GetString() is string fx) { Run("effect " + EffectArg(fx) + g); return; }
            if (j.TryGetProperty("color", out JsonElement c))
            {
                int r = c.GetProperty("r").GetInt32(), gg = c.GetProperty("g").GetInt32(), bb = c.GetProperty("b").GetInt32();
                Run($"color #{r:X2}{gg:X2}{bb:X2}{g}");
                return;
            }
            Run("on" + g);
        }
    }

    /// <summary>Effect name from Home Assistant to a command argument (library key, own chase or the screen).</summary>
    string EffectArg(string name)
    {
        if (name == ScreenName) return "screen";
        EffectDefinition lib = EffectLibrary.All.FirstOrDefault(e => e.Name == name);
        return lib != null ? lib.Key : "\"" + name.Replace("\"", "") + "\"";
    }

    public void Dispose()
    {
        stop.Cancel();
        try { Disconnect().Wait(2000); } catch { }
    }
}
