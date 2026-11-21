using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Fixtures;
using DmxLicht.Core.Output;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.App;

/// <summary>HTTP API + WebSocket for the UI. All mutations go through <see cref="LightEngine.Change"/>.</summary>
static class Api
{
    public sealed record LooksRequest(string[] Ids, bool? On, ColorRef Color);
    public sealed record MasterRequest(int? Brightness, bool? Blackout, bool? UseWhiteLed, int? FadeMs);
    public sealed record ModeRequest(OutputMode Mode);
    public sealed record EffectRequest(EffectSettings Effect, bool Start);
    public sealed record FaderRequest(string Id, int Channel, int Value);
    public sealed record FadersAllRequest(string[] Ids, int Value);
    public sealed record IdentifyRequest(string Id, bool On);
    public sealed record AddRequest(string TypeId, string Mode, int Count, int? Address, string Name, int? Universe = null);
    public sealed record FixtureUpdate(string Id, string Name, string Mode, int? Address, double? X, double? Y, int? Universe = null);

    public sealed record BpmRequest(double Bpm);
    public sealed record RecoverRequest(bool Deep);
    public sealed record ReportRequest(string Note);
    public sealed record PairRequest(string Code, string Name);

    /// <summary>Null when the request comes from this PC, else a 403 (things only the PC itself may do).</summary>
    static IResult LocalOnly(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip) ? null : Results.Text("only on this PC", statusCode: 403);
    public sealed record TemplatesRequest(string[] Keys, Dictionary<string, string> Names);
    public sealed record PinRequest(string Pin, string NewPin, bool? Locked);
    public sealed record NameRequest(string Name);
    public sealed record StrobeRequest(bool? Active, int? Speed);
    public sealed record TestRequest(Dictionary<int, int> Channels, bool Exclusive);
    public sealed record WalkRequest(int IntervalMs);
    public sealed record PlayRequest(string Id, double? FromMs);
    public sealed record SeekRequest(double Ms);
    public sealed record GroupRequest(string Id, string Name, string[] FixtureIds);
    public sealed record IdRequest(string Id);
    public sealed record LayerRequest(string GroupId, EffectSettings Effect);
    public sealed record SceneRequest(string Id, string Name, string Color, int? FadeMs, bool Capture);

    // Endpoints that change the setup; blocked while locked unless the request carries the PIN.
    static readonly string[] Protected =
    {
        "/api/patch", "/api/groups", "/api/scenes/save", "/api/scenes/remove", "/api/scenes/order", "/api/timelines",
        "/api/settings", "/api/library", "/api/audio/upload", "/api/test", "/api/midi/learn",
        "/api/undo", "/api/redo", "/api/shows", "/api/restore", "/api/companion", "/api/scene-templates",
        "/api/chases", "/api/output/usb-power", "/api/phone/firewall", "/api/schedule", "/api/update/install",
    };

    public static void Map(WebApplication app, LightEngine engine, AudioInput audio, Func<AppSettings, string> applySettings)
    {
        // pairing: devices on the network need to be paired before they can use the API or the live socket
        app.Use(async (ctx, next) =>
        {
            string path = ctx.Request.Path.Value ?? "";
            bool api = path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/ws", StringComparison.OrdinalIgnoreCase);
            bool local = ctx.Connection.RemoteIpAddress is { } rip && System.Net.IPAddress.IsLoopback(rip);
            if (!api || local || !engine.Show.Settings.RequirePairing
                || path.StartsWith("/api/pair", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/cmd", StringComparison.OrdinalIgnoreCase)          // trigger links: own key check
                || (ctx.Request.Query["key"] is var k && k.Count > 0 && k[0] == engine.Show.Settings.Control?.TriggerKey && !string.IsNullOrEmpty(k[0]))
                || Pairing.IsPaired(engine, ctx.Request.Cookies[Pairing.Cookie]))
            {
                await next();
                return;
            }
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsync("pair");
        });

        app.Use(async (ctx, next) =>
        {
            AppSettings st = engine.Show.Settings;
            string path = ctx.Request.Path.Value ?? "";
            if (st.Locked && !string.IsNullOrEmpty(st.OperatorPin) && !HttpMethods.IsGet(ctx.Request.Method)
                && Protected.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                && ctx.Request.Headers["X-Dmx-Pin"] != st.OperatorPin)
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsync("locked");
                return;
            }
            await next();
        });

        var api = app.MapGroup("/api");

        // ---------- Companion: install the bundled module from the UI ----------
        api.MapGet("/companion", () => Results.Json(CompanionInstaller.Check(), Json.Compact));
        api.MapPost("/companion/install", () =>
        {
            try
            {
                string path = CompanionInstaller.Install();
                return Results.Json(new { path, status = CompanionInstaller.Check() }, Json.Compact);
            }
            catch (DirectoryNotFoundException ex) { return Results.Text(ex.Message, statusCode: 400); }
            catch (UnauthorizedAccessException ex) { return Results.Text("Close Companion and try again: " + ex.Message, statusCode: 409); }
            catch (IOException ex) { return Results.Text("Close Companion and try again: " + ex.Message, statusCode: 409); }
            catch (Exception ex) { return Results.Text(ex.Message, statusCode: 400); }
        });

        // ---------- undo, show files, backups ----------
        api.MapPost("/undo", () => engine.Undo() ? Results.Ok() : Results.NoContent());
        api.MapPost("/redo", () => engine.Redo() ? Results.Ok() : Results.NoContent());
        api.MapGet("/shows", () => Results.Json(ShowLibrary.List().Select(x => new { name = x.name, modified = x.modified }), Json.Compact));
        api.MapPost("/shows/save", (NameRequest r) =>
        {
            string name = ShowLibrary.SafeName(r.Name ?? engine.Show.Name ?? "Show");
            engine.Change(s => s.Name = name, undoable: false);
            ShowLibrary.Save(engine.Read(ShowContent.From), name);
            return Results.Ok(new { name });
        });
        api.MapPost("/shows/open", (NameRequest r) =>
        {
            try { engine.Replace(ShowLibrary.Load(r.Name)); return Results.Ok(); }
            catch (Exception ex) { return Results.BadRequest(ex.Message); }
        });
        api.MapPost("/shows/new", () =>
        {
            // a new show keeps the lamps and groups; looks, scenes and timelines start empty
            ShowContent c = engine.Read(ShowContent.From);
            engine.Replace(new ShowContent { Patch = c.Patch, Groups = c.Groups, State = new ShowState { Music = c.State.Music } });
            return Results.Ok();
        });
        api.MapPost("/shows/remove", (NameRequest r) => { ShowLibrary.Delete(r.Name); return Results.Ok(); });
        app.MapGet("/api/backup", (HttpContext ctx) =>
        {
            // the settings hold the PIN and passwords: only this PC, or a device that knows the PIN
            bool local = ctx.Connection.RemoteIpAddress is { } bip && System.Net.IPAddress.IsLoopback(bip);
            string pin = engine.Show.Settings.OperatorPin;
            if (!local && !string.IsNullOrEmpty(pin) && ctx.Request.Headers["X-Dmx-Pin"] != pin && ctx.Request.Query["pin"] != pin)
                return Results.Text("locked", statusCode: 403);
            ctx.Response.ContentType = "application/zip";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"Glowsmith-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip\"";
            var ms = new MemoryStream();
            ShowLibrary.WriteBackupZip(ms, engine.Read(sh =>
            {
                ShowFile c = SceneOps.Clone(sh);
                c.Settings.OperatorPin = null;
                if (c.Settings.Mqtt != null) c.Settings.Mqtt.Password = null;
                c.Settings.Devices = new();
                return c.ToJson();
            }));
            ms.Position = 0;
            return Results.Stream(ms, "application/zip", $"Glowsmith-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        });
        api.MapPost("/restore", async (IFormFile file) =>
        {
            if (file == null) return Results.BadRequest();
            try
            {
                var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;
                ShowContent c = file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? ShowContent.From(JsonSerializer.Deserialize<ShowFile>(ms, Json.Options))
                    : ShowLibrary.RestoreZip(ms);
                engine.ReloadLibrary();
                engine.Replace(c);
                return Results.Ok();
            }
            catch (Exception ex) { return Results.BadRequest(ex.Message); }
        }).DisableAntiforgery();

        // ---------- operator lock ----------
        api.MapPost("/unlock", (PinRequest r) =>
        {
            string pin = engine.Show.Settings.OperatorPin;
            return string.IsNullOrEmpty(pin) || r.Pin == pin ? Results.Ok() : Results.Text("wrong pin", statusCode: 403);
        });
        api.MapPost("/lock/setup", (PinRequest r) =>
        {
            string pin = engine.Show.Settings.OperatorPin;
            if (!string.IsNullOrEmpty(pin) && r.Pin != pin) return Results.Text("wrong pin", statusCode: 403);
            engine.Change(s =>
            {
                if (r.NewPin != null) s.Settings.OperatorPin = r.NewPin.Trim().Length == 0 ? null : r.NewPin.Trim();
                if (r.Locked is bool l) s.Settings.Locked = l && !string.IsNullOrEmpty(s.Settings.OperatorPin);
            }, undoable: false);
            return Results.Ok();
        });

        // ---------- fixture library: own types and Open Fixture Library import ----------
        api.MapPost("/library/save", (FixtureType t) =>
        {
            if (t == null || string.IsNullOrWhiteSpace(t.Model) || t.Modes.Count == 0) return Results.BadRequest("model and at least one mode needed");
            t.Id = string.IsNullOrWhiteSpace(t.Id) || FixtureLibrary.BuiltIn().Any(b => b.Id == t.Id)
                ? "custom-" + System.Text.RegularExpressions.Regex.Replace((t.Manufacturer + "-" + t.Model).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')
                : t.Id;
            foreach (FixtureMode m in t.Modes)
            {
                for (int i = 0; i < m.Channels.Count; i++) m.Channels[i].Number = m.Channels[i].Number < 1 ? i + 1 : m.Channels[i].Number;
                m.Footprint = Math.Max(m.Footprint, m.Channels.Select(c => c.Number).DefaultIfEmpty(1).Max());
            }
            t.Custom = true;
            Directory.CreateDirectory(engine.FixturesFolder);
            File.WriteAllText(Path.Combine(engine.FixturesFolder, t.Id + ".json"), JsonSerializer.Serialize(t, Json.Options));
            engine.ReloadLibrary();
            return Results.Ok(new { id = t.Id });
        });
        api.MapPost("/library/remove", (IdRequest r) =>
        {
            string file = Path.Combine(engine.FixturesFolder, Path.GetFileName(r.Id ?? "") + ".json");
            if (!File.Exists(file)) return Results.NotFound();
            File.Delete(file);
            engine.ReloadLibrary();
            return Results.Ok();
        });
        api.MapPost("/library/ofl", async (IFormFile file) =>
        {
            if (file == null) return Results.BadRequest();
            using var r = new StreamReader(file.OpenReadStream());
            try { return Results.Json(OflImport.Convert(await r.ReadToEndAsync(), file.FileName), Json.Compact); }
            catch (Exception ex) { return Results.BadRequest("Not an OFL fixture file: " + ex.Message); }
        }).DisableAntiforgery();

        // ---------- commands: trigger URLs for phones, Home Assistant, IFTTT, scripts ----------
        // GET or POST /api/cmd/scene/Rood  ·  /api/cmd/flash/green/5  ·  /api/cmd?c=blackout%20on
        // From another device the key is required: ?key=... (shown on the Koppelingen page).
        IResult RunCommand(HttpContext ctx, string path)
        {
            bool local = ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip);
            string key = engine.Show.Settings.Control?.TriggerKey;
            if (!local && !string.IsNullOrEmpty(key) && ctx.Request.Query["key"] != key)
                return Results.Text("wrong or missing key", statusCode: 403);
            string line = !string.IsNullOrWhiteSpace(path) ? Uri.UnescapeDataString(path) : ctx.Request.Query["c"].ToString();
            string result = DmxLicht.Core.Control.Commands.Run(engine, line);
            Log.Write("Opdracht van " + (local ? "deze pc" : ctx.Connection.RemoteIpAddress) + ": " + line + " -> " + result);
            return result == "ok" ? Results.Text("ok") : Results.Text(result, statusCode: 400);
        }
        app.MapGet("/api/cmd/{**path}", (HttpContext ctx, string path) => RunCommand(ctx, path));
        app.MapPost("/api/cmd/{**path}", (HttpContext ctx, string path) => RunCommand(ctx, path));
        app.MapGet("/api/cmd", (HttpContext ctx) => RunCommand(ctx, null));

        api.MapGet("/midi/devices", () => Results.Json(MidiControl.Devices(), Json.Compact));
        api.MapPost("/midi/learn", () => { Program.Controls.midi.Learning = true; return Results.Ok(); });
        api.MapGet("/control/info", () => Results.Json(new
        {
            commands = DmxLicht.Core.Control.Commands.All,
            addresses = NetworkOutput.LocalAddresses(),
        }, Json.Compact));

        // ---------- strobe and test page ----------
        api.MapPost("/strobe", (StrobeRequest r) =>
        {
            engine.Change(s =>
            {
                if (r.Active is bool a) s.State.StrobeActive = a;
                if (r.Speed is int sp) s.State.StrobeSpeed = Math.Clamp(sp, 1, 100);
            }, undoable: r.Active == null);
            return Results.Ok();
        });
        api.MapPost("/test", (TestRequest r) =>
        {
            var ch = (r.Channels ?? new()).Where(kv => kv.Key >= 1 && kv.Key <= 512 * Dmx.MaxUniverses)
                .ToDictionary(kv => kv.Key, kv => (byte)Math.Clamp(kv.Value, 0, 255));
            engine.SetTest(ch, r.Exclusive);
            return Results.Ok();
        });
        api.MapPost("/test/walk", (WalkRequest r) => { engine.SetWalk(r.IntervalMs); return Results.Ok(); });
        api.MapPost("/test/clear", () => { engine.ClearTest(); return Results.Ok(); });

        // ---------- timelines ----------
        api.MapPost("/timelines/save", (Timeline t) =>
        {
            if (t == null) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(t.Id)) t.Id = PatchedFixture.NewId();
            t.LengthMs = Math.Clamp(t.LengthMs, 1000, 6 * 3600_000);
            t.Bpm = Math.Clamp(t.Bpm, 30, 300);
            foreach (TimelineTrack k in t.Tracks)
                foreach (TimelineClip c in k.Clips)
                {
                    c.StartMs = Math.Max(0, c.StartMs);
                    c.LengthMs = Math.Max(50, c.LengthMs);
                    if (c.Effect != null) Clean(c.Effect);
                }
            bool restart = false; double pos = 0;
            engine.WithTimeline((player, s) =>
            {
                int i = s.Timelines.FindIndex(x => x.Id == t.Id);
                string oldAudio = i >= 0 ? s.Timelines[i].AudioFile : null;
                if (i >= 0) s.Timelines[i] = t; else s.Timelines.Add(t);
                if (player.TimelineId == t.Id && oldAudio != t.AudioFile) { restart = player.Playing; pos = player.PositionMs; player.Unload(); }
            });
            if (restart) engine.WithTimeline((player, s) => player.Play(s.Timelines.First(x => x.Id == t.Id), pos));
            return Results.Ok(new { id = t.Id });
        });
        api.MapPost("/timelines/remove", (IdRequest r) =>
        {
            engine.WithTimeline((player, s) =>
            {
                if (player.TimelineId == r.Id) player.Stop();
                s.Timelines.RemoveAll(x => x.Id == r.Id);
            });
            return Results.Ok();
        });
        api.MapPost("/timeline/play", (PlayRequest r) =>
        {
            Timeline t = engine.Read(s => s.Timelines.FirstOrDefault(x => x.Id == r.Id));
            if (t == null) return Results.NotFound();
            engine.WithTimeline((player, _) =>
            {
                if (player.TimelineId != null && player.TimelineId != t.Id) player.Stop();
                player.Play(t, r.FromMs);
            });
            return Results.Ok();
        });
        api.MapPost("/timeline/pause", () => { engine.WithTimeline((p, _) => p.Pause()); return Results.Ok(); });
        IResult TimelineStep(string id, Action<TimelinePlayer, Timeline> step)
        {
            Timeline t = engine.Read(s => s.Timelines.FirstOrDefault(x => x.Id == (id ?? engine.Timeline.TimelineId)) ?? s.Timelines.FirstOrDefault());
            if (t == null) return Results.NotFound();
            engine.WithTimeline((p, _) => step(p, t));
            return Results.Ok();
        }
        api.MapPost("/timeline/go", (IdRequest r) => TimelineStep(r?.Id, (p, t) => p.Play(t)));
        api.MapPost("/timeline/next", (IdRequest r) => TimelineStep(r?.Id, (p, t) => p.Next(t)));
        api.MapPost("/timeline/prev", (IdRequest r) => TimelineStep(r?.Id, (p, t) => p.Previous(t)));
        api.MapPost("/timeline/stop", () => { engine.WithTimeline((p, _) => p.Stop()); return Results.Ok(); });
        api.MapPost("/timeline/seek", (SeekRequest r) => { engine.WithTimeline((p, _) => p.Seek(r.Ms)); return Results.Ok(); });

        api.MapPost("/audio/upload", async (IFormFile file) =>
        {
            if (file == null || file.Length == 0) return Results.BadRequest();
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".mp3" or ".wav" or ".m4a" or ".aac" or ".wma" or ".flac")) return Results.BadRequest("unsupported");
            string name = string.Concat(Path.GetFileNameWithoutExtension(file.FileName).Select(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' ? ch : '_')).Trim();
            if (name.Length == 0) name = "song";
            Directory.CreateDirectory(Program.AudioDir);
            string target = name + ext;
            for (int i = 2; File.Exists(Path.Combine(Program.AudioDir, target)); i++) target = name + " (" + i + ")" + ext;
            await using (var fs = File.Create(Path.Combine(Program.AudioDir, target))) await file.CopyToAsync(fs);
            try
            {
                var (len, _) = SongClock.Peaks(Path.Combine(Program.AudioDir, target), 10);
                return Results.Ok(new { file = target, lengthMs = len });
            }
            catch (Exception ex)
            {
                File.Delete(Path.Combine(Program.AudioDir, target));
                return Results.BadRequest("not playable: " + ex.Message);
            }
        }).DisableAntiforgery();

        api.MapGet("/audio/peaks", (string file) =>
        {
            string path = Path.Combine(Program.AudioDir, Path.GetFileName(file ?? ""));
            if (!File.Exists(path)) return Results.NotFound();
            var (len, peaks) = SongClock.Peaks(path, 2000);
            return Results.Json(new { lengthMs = len, peaks }, Json.Compact);
        });

        // ---------- music / sound-to-light ----------
        api.MapGet("/audio/devices", () => Results.Json(AudioInput.Devices(), Json.Compact));
        api.MapPut("/music", (MusicSettings m) =>
        {
            m.StepsPerBeat = m.StepsPerBeat is 0.25 or 0.5 or 1 or 2 or 4 ? m.StepsPerBeat : 1;
            m.FloorPercent = Math.Clamp(m.FloorPercent, 0, 100);
            m.Sensitivity = Math.Clamp(m.Sensitivity, 0.25, 8);
            engine.Change(s => s.State.Music = m);
            return Results.Ok();
        });
        api.MapPost("/tap", () => { engine.Beat.Tap(engine.Ms); return Results.Ok(new { bpm = engine.Beat.Bpm }); });
        api.MapPost("/bpm", (BpmRequest r) => { engine.Beat.SetBpm(r.Bpm, engine.Ms); return Results.Ok(new { bpm = engine.Beat.Bpm }); });

        api.MapGet("/bootstrap", () => Results.Json(Bootstrap(engine), Json.Compact));
        api.MapGet("/state", () => Results.Json(State(engine), Json.Compact));

        // ---------- patch ----------
        api.MapPut("/patch", (List<PatchedFixture> patch) =>
        {
            foreach (PatchedFixture p in patch)
            {
                p.Address = Math.Clamp(p.Address, 1, 512);
                p.Universe = Dmx.Clamp(p.Universe);
                if (string.IsNullOrWhiteSpace(p.Id)) p.Id = PatchedFixture.NewId();
            }
            engine.Change(s =>
            {
                s.Patch = patch;
                var ids = patch.Select(p => p.Id).ToHashSet();
                foreach (string gone in s.State.Looks.Keys.Where(k => !ids.Contains(k)).ToList()) s.State.Looks.Remove(gone);
                foreach (string gone in s.State.Faders.Keys.Where(k => !ids.Contains(k)).ToList()) s.State.Faders.Remove(gone);
                s.State.Identify.RemoveWhere(id => !ids.Contains(id));
            }, patchChanged: true);
            return Results.Ok();
        });

        // Small operations instead of replacing the whole list, so two screens (or a quick
        // add followed by an edit) never overwrite each other's changes.
        api.MapPost("/patch/update", (List<FixtureUpdate> updates) =>
        {
            engine.Change(s =>
            {
                foreach (FixtureUpdate u in updates)
                {
                    PatchedFixture p = s.Patch.FirstOrDefault(x => x.Id == u.Id);
                    if (p == null) continue;
                    if (!string.IsNullOrWhiteSpace(u.Name)) p.Name = u.Name.Trim();
                    if (u.Mode != null) p.Mode = u.Mode;
                    if (u.Address is int a) p.Address = Math.Clamp(a, 1, 512);
                    if (u.Universe is int un) p.Universe = Dmx.Clamp(un);
                    if (u.X is double x) p.X = x < 0 ? -1 : Math.Clamp(x, 0, 1);
                    if (u.Y is double y) p.Y = y < 0 ? -1 : Math.Clamp(y, 0, 1);
                }
            }, patchChanged: true);
            return Results.Ok();
        });

        api.MapPost("/patch/order", (string[] ids) =>
        {
            engine.Change(s =>
            {
                var rank = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                s.Patch = s.Patch.OrderBy(p => rank.TryGetValue(p.Id, out int r) ? r : int.MaxValue).ToList();
            }, patchChanged: true);
            return Results.Ok();
        });

        api.MapPost("/patch/remove", (string[] ids) =>
        {
            engine.Change(s =>
            {
                s.Patch.RemoveAll(p => ids.Contains(p.Id));
                foreach (FixtureGroup g in s.Groups) g.FixtureIds.RemoveAll(ids.Contains);
                foreach (string id in ids) { s.State.Looks.Remove(id); s.State.Faders.Remove(id); s.State.Identify.Remove(id); }
            }, patchChanged: true);
            return Results.Ok();
        });

        api.MapPost("/patch/renumber", () =>
        {
            engine.Change(s =>
            {
                // one after another; what does not fit in a universe anymore moves on to the next one
                int universe = 1, next = 1;
                foreach (PatchedFixture p in s.Patch)
                {
                    int fp = engine.Library.Find(p.TypeId)?.Mode(p.Mode)?.Footprint ?? 1;
                    if (next + fp - 1 > 512 && universe < Dmx.MaxUniverses) { universe++; next = 1; }
                    p.Universe = universe;
                    p.Address = Math.Min(next, 512);
                    next += fp;
                }
            }, patchChanged: true);
            return Results.Ok();
        });

        api.MapPost("/patch/add", (AddRequest r) =>
        {
            FixtureType t = engine.Library.Find(r.TypeId);
            FixtureMode m = t?.Mode(r.Mode);
            if (m == null) return Results.BadRequest("unknown type/mode");
            int count = Math.Clamp(r.Count, 1, 64);
            int failed = 0;
            engine.Change(s =>
            {
                int existing = s.Patch.Count(p => p.TypeId == t.Id);
                for (int i = 0; i < count; i++)
                {
                    // the first free spot from the asked universe on (a full universe continues in the next one)
                    var (uni, addr) = i == 0 && r.Address is int a ? (Dmx.Clamp(r.Universe ?? 1), Math.Clamp(a, 1, 512))
                        : Rig.NextFree(UsedRanges(engine, s), m.Footprint, r.Universe ?? 1);
                    if (addr < 1) { failed++; continue; }
                    s.Patch.Add(new PatchedFixture
                    {
                        Name = (string.IsNullOrWhiteSpace(r.Name) ? t.Model : r.Name) + " #" + (existing + i + 1),
                        TypeId = t.Id, Mode = m.Name, Address = addr, Universe = uni,
                    });
                }
            }, patchChanged: true);
            return Results.Ok(new { added = count - failed, failed });
        });

        // ---------- live control ----------
        api.MapPost("/looks", (LooksRequest r) =>
        {
            Live(engine, s =>
            {
                var ids = r.Ids is { Length: > 0 } ? r.Ids : s.Patch.Select(p => p.Id).ToArray();
                foreach (string id in ids)
                {
                    FixtureLook l = s.State.Look(id);
                    if (r.On is bool on) l.On = on;
                    if (r.Color != null) l.Color = r.Color;
                }
                // like v1.4: on/off switches to static colours; picking a colour alone leaves a running effect alone
                if (r.On != null) s.State.Mode = OutputMode.Static;
            });
            return Results.Ok();
        });

        api.MapPost("/master", (MasterRequest r) =>
        {
            engine.Change(s =>
            {
                if (r.Brightness is int b) s.State.BrightnessPercent = Math.Clamp(b, 0, 100);
                if (r.Blackout is bool bo) s.State.Blackout = bo;
                if (r.UseWhiteLed is bool w) s.State.UseWhiteLed = w;
                if (r.FadeMs is int fm) s.State.FadeMs = Math.Clamp(fm, 0, 60000);
            });
            return Results.Ok();
        });

        api.MapPost("/mode", (ModeRequest r) => { Live(engine, s => s.State.Mode = r.Mode); return Results.Ok(); });

        api.MapPost("/effect", (EffectRequest r) =>
        {
            if (r.Effect == null) return Results.BadRequest();
            Clean(r.Effect);
            bool start = r.Start;
            Live(engine, s =>
            {
                s.State.Effect = r.Effect;
                if (start) s.State.Mode = OutputMode.Effect;
            });
            return Results.Ok();
        });

        // ---------- own chases (chase editor) ----------
        api.MapPost("/chases/save", (CustomChase c) =>
        {
            if (c == null) return Results.BadRequest();
            CleanChase(c);
            engine.Change(s =>
            {
                int i = s.State.Chases.FindIndex(x => x.Id == c.Id);
                if (i >= 0) s.State.Chases[i] = c; else s.State.Chases.Add(c);
            });
            return Results.Ok(new { id = c.Id });
        });
        api.MapPost("/chases/remove", (IdRequest r) =>
        {
            engine.Change(s =>
            {
                s.State.Chases.RemoveAll(x => x.Id == r.Id);
                // whatever played this chase stops: live, group layers, scenes and timeline clips
                void Forget(EffectSettings e) { if (e != null && e.ChaseId == r.Id) e.ChaseId = null; }
                Forget(s.State.Effect);
                s.State.Layers.RemoveAll(l => l.Effect?.ChaseId == r.Id);
                foreach (Scene sc in s.Scenes)
                {
                    Forget(sc.State.Effect);
                    sc.State.Layers?.RemoveAll(l => l.Effect?.ChaseId == r.Id);
                }
                foreach (Timeline t in s.Timelines)
                    foreach (TimelineTrack tr in t.Tracks)
                        foreach (TimelineClip cl in tr.Clips) Forget(cl.Effect);
            });
            return Results.Ok();
        });
        api.MapPost("/chases/order", (string[] ids) =>
        {
            engine.Change(s =>
            {
                var rank = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                s.State.Chases = s.State.Chases.OrderBy(x => rank.TryGetValue(x.Id, out int k) ? k : int.MaxValue).ToList();
            });
            return Results.Ok();
        });

        // ---------- groups and group effects ----------
        api.MapPost("/groups/save", (GroupRequest r) =>
        {
            string id = null;
            engine.Change(s =>
            {
                FixtureGroup g = r.Id == null ? null : s.Groups.FirstOrDefault(x => x.Id == r.Id);
                if (g == null) { g = new FixtureGroup(); s.Groups.Add(g); }
                if (!string.IsNullOrWhiteSpace(r.Name)) g.Name = r.Name.Trim();
                if (r.FixtureIds != null) g.FixtureIds = r.FixtureIds.Distinct().ToList();
                g.Name ??= "Groep " + s.Groups.Count;
                id = g.Id;
            }, patchChanged: true);
            return Results.Ok(new { id });
        });
        api.MapPost("/groups/remove", (IdRequest r) =>
        {
            engine.Change(s =>
            {
                s.Groups.RemoveAll(g => g.Id == r.Id);
                s.State.Layers.RemoveAll(l => l.GroupId == r.Id);
            }, patchChanged: true);
            return Results.Ok();
        });
        api.MapPost("/layers/start", (LayerRequest r) =>
        {
            if (r.GroupId == null || r.Effect == null) return Results.BadRequest();
            Clean(r.Effect);
            Live(engine, s =>
            {
                s.State.Layers.RemoveAll(l => l.GroupId == r.GroupId);
                s.State.Layers.Add(new EffectLayer { GroupId = r.GroupId, Effect = r.Effect });
            });
            return Results.Ok();
        });
        api.MapPost("/layers/stop", (IdRequest r) =>
        {
            Live(engine, s => s.State.Layers.RemoveAll(l => r.Id == null || l.GroupId == r.Id));
            return Results.Ok();
        });

        // ---------- ready-made scenes (Voorbeeldscènes) ----------
        api.MapGet("/scene-templates", () => Results.Json(SceneTemplates.All.Select(t => new { t.Key, t.Name, t.NameEn, t.Category, t.Color }), Json.Compact));
        api.MapPost("/scene-templates/add", (TemplatesRequest r) =>
        {
            int added = 0, skipped = 0;
            engine.Change(s =>
            {
                List<string> ids = s.Patch.Select(p => p.Id).ToList();
                foreach (string key in r.Keys ?? Array.Empty<string>())
                {
                    SceneTemplate t = SceneTemplates.Find(key);
                    if (t == null) continue;
                    string name = r.Names != null && r.Names.TryGetValue(key, out string n) && !string.IsNullOrWhiteSpace(n) ? n.Trim() : t.Name;
                    if (s.Scenes.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) { skipped++; continue; }
                    s.Scenes.Add(new Scene { Name = name, Color = t.Color, FadeMs = t.FadeMs, State = t.Build(ids) });
                    added++;
                }
            });
            return Results.Ok(new { added, skipped });
        });

        // ---------- scenes ----------
        api.MapPost("/scenes/save", (SceneRequest r) =>
        {
            string id = null;
            engine.Change(s =>
            {
                Scene sc = r.Id == null ? null : s.Scenes.FirstOrDefault(x => x.Id == r.Id);
                bool isNew = sc == null;
                if (isNew) { sc = new Scene(); s.Scenes.Add(sc); }
                if (isNew || r.Capture) sc.State = SceneOps.Capture(s.State);
                if (!string.IsNullOrWhiteSpace(r.Name)) sc.Name = r.Name.Trim();
                sc.Name ??= "Scene " + s.Scenes.Count;
                if (r.Color != null) sc.Color = r.Color;
                else if (isNew) sc.Color = SceneOps.GuessColor(sc.State);
                if (r.FadeMs is int f) sc.FadeMs = Math.Clamp(f, 0, 60000);
                if (isNew || r.Capture) s.State.ActiveScene = sc.Id;
                id = sc.Id;
            });
            return Results.Ok(new { id });
        });
        api.MapPost("/scenes/recall", (IdRequest r) =>
        {
            Scene sc = engine.Read(s => s.Scenes.FirstOrDefault(x => x.Id == r.Id));
            if (sc == null) return Results.NotFound();
            engine.Change(s => { SceneOps.Apply(sc.State, s.State); s.State.ActiveScene = sc.Id; }, fade: sc.FadeMs);
            return Results.Ok();
        });
        api.MapPost("/scenes/remove", (IdRequest r) =>
        {
            engine.Change(s => { s.Scenes.RemoveAll(x => x.Id == r.Id); if (s.State.ActiveScene == r.Id) s.State.ActiveScene = null; });
            return Results.Ok();
        });
        api.MapPost("/scenes/order", (string[] ids) =>
        {
            engine.Change(s =>
            {
                var rank = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                s.Scenes = s.Scenes.OrderBy(x => rank.TryGetValue(x.Id, out int k) ? k : int.MaxValue).ToList();
            });
            return Results.Ok();
        });

        api.MapPost("/fader", (FaderRequest r) =>
        {
            engine.Change(s =>
            {
                RigFixture f = engine.Rig.Fixtures.FirstOrDefault(x => x.Patch.Id == r.Id);
                if (f == null || r.Channel < 1 || r.Channel > f.Mode.Footprint) return;
                s.State.FaderValues(r.Id, f.Mode.Footprint)[r.Channel] = (byte)Math.Clamp(r.Value, 0, 255);
                s.State.Mode = OutputMode.Faders;
                s.State.ActiveScene = null;
            });
            return Results.Ok();
        });

        api.MapPost("/faders/all", (FadersAllRequest r) =>
        {
            engine.Change(s =>
            {
                foreach (RigFixture f in engine.Rig.Fixtures)
                {
                    if (r.Ids is { Length: > 0 } && !r.Ids.Contains(f.Patch.Id)) continue;
                    byte[] v = s.State.FaderValues(f.Patch.Id, f.Mode.Footprint);
                    for (int c = 1; c <= f.Mode.Footprint; c++) v[c] = (byte)Math.Clamp(r.Value, 0, 255);
                }
                s.State.Mode = OutputMode.Faders;
            });
            return Results.Ok();
        });

        api.MapPost("/faders/from-output", () =>
        {
            var (frame, _) = engine.Snapshot();
            engine.Change(s =>
            {
                foreach (RigFixture f in engine.Rig.Fixtures)
                {
                    byte[] v = s.State.FaderValues(f.Patch.Id, f.Mode.Footprint);
                    for (int c = 1; c <= f.Mode.Footprint; c++)
                    {
                        int ch = f.BaseChannel + c;
                        v[c] = ch >= 1 && ch < frame.Length ? frame[ch] : (byte)0;
                    }
                }
            });
            return Results.Ok();
        });

        api.MapPost("/identify", (IdentifyRequest r) =>
        {
            engine.Change(s => { if (r.On) s.State.Identify.Add(r.Id); else s.State.Identify.Remove(r.Id); }, undoable: false);
            return Results.Ok();
        });

        api.MapPut("/custom-colors", (List<string> colors) =>
        {
            engine.Change(s => s.State.CustomColors = colors.Take(48).ToList());
            return Results.Ok();
        });

        // ---------- settings / output ----------
        api.MapPut("/settings", (AppSettings settings) =>
        {
            engine.Read(s =>
            {
                settings.OperatorPin = s.Settings.OperatorPin;   // only changed via /lock/setup
                settings.Locked = s.Settings.Locked;
                settings.Devices = s.Settings.Devices;   // managed by the pairing endpoints only
                settings.Mqtt ??= s.Settings.Mqtt ?? new MqttSettings();
                if (settings.Mqtt.Password == null) settings.Mqtt.Password = s.Settings.Mqtt?.Password;   // UI never has it
                return 0;
            });
            // outside the engine lock: restarting outputs, OSC or MIDI can take seconds
            string note = applySettings(settings);
            engine.Change(s => s.Settings = settings, undoable: false);
            return Results.Ok(new { note });
        });

        api.MapPost("/output/reconnect", () => { engine.Output.Reconnect(); return Results.Ok(); });

        // USB herstellen: step 1 (no admin) = forget learned modes, reset the port if the driver allows, reopen.
        // Step 2 (deep, asks for admin via UAC) = Windows restarts the dongle, like unplugging it.
        api.MapPost("/output/recover", async (RecoverRequest r) =>
        {
            if (engine.Output is not MultiOutput mo) return Results.BadRequest();
            List<string> ids = UdmxDevices.InstanceIds();
            string restart = null;
            if (r?.Deep == true)
            {
                if (ids.Count == 0) return Results.Json(new { connected = false, devicePresent = false }, Json.Compact);
                restart = await UsbRestart.RestartAsync(ids);
                if (restart == "cancelled") return Results.Json(new { connected = mo.Udmx.Connected, devicePresent = true, cancelled = true }, Json.Compact);
                await Task.Delay(1500);                 // let Windows bring the device back
            }
            long opensBefore = mo.Udmx.Opens;
            mo.Udmx.Recover();
            // wait for a real reopen (Connected is still true from before right after asking)
            for (int i = 0; i < 60 && (mo.Udmx.Opens == opensBefore || !mo.Udmx.Connected); i++) await Task.Delay(100);
            return Results.Json(new
            {
                connected = mo.Udmx.Connected,
                devicePresent = UdmxDevices.InstanceIds().Count > 0,
                restart,
                error = mo.Udmx.Error,
            }, Json.Compact);
        });
        api.MapPost("/output/usb-power", async () =>
        {
            if (engine.Output is not MultiOutput mo) return Results.BadRequest();
            List<string> ids = UdmxDevices.InstanceIds();
            if (ids.Count == 0) return Results.Json(new { result = "notfound" }, Json.Compact);
            string result = await UsbRestart.DisablePowerSavingAsync(ids);
            if (result == "ok") { await Task.Delay(1500); mo.Udmx.Recover(); }
            return Results.Json(new { result }, Json.Compact);
        });
        api.MapGet("/network/addresses",() => Results.Json(NetworkOutput.LocalAddresses(), Json.Compact));

        api.MapGet("/mqtt/status", () => Results.Json(new { status = HomeAssistant.Status, error = HomeAssistant.Error }, Json.Compact));

        // ---------- follow the screen ----------
        api.MapPut("/screen-sync", (ScreenSyncSettings set) =>
        {
            if (set == null) return Results.BadRequest();
            set.Saturation = Math.Clamp(set.Saturation, 0, 400);
            set.Smoothing = Math.Clamp(set.Smoothing, 0, 100);
            set.Monitor = Math.Max(0, set.Monitor);
            set.EdgeDepth = Math.Clamp(set.EdgeDepth, 2, 50);
            set.EdgeLeft = Math.Clamp(set.EdgeLeft, 0, 128); set.EdgeTop = Math.Clamp(set.EdgeTop, 0, 128);
            set.EdgeRight = Math.Clamp(set.EdgeRight, 0, 128); set.EdgeBottom = Math.Clamp(set.EdgeBottom, 0, 128);
            if (set.Corners is not { Length: 8 }) set.Corners = ScreenSyncSettings.DefaultCorners();
            for (int i = 0; i < 8; i++) set.Corners[i] = Math.Clamp(double.IsFinite(set.Corners[i]) ? set.Corners[i] : 0.5, 0, 1);
            set.PointSize = Math.Clamp(set.PointSize, 1, 50);
            if (set.Points != null)
                set.Points = set.Points.Take(Math.Min(2 * EffectLibrary.MaxHeads, set.Points.Length / 2 * 2))
                    .Select(v => Math.Clamp(double.IsFinite(v) ? v : 0.5, 0, 1)).ToArray();
            engine.Change(s => s.State.ScreenSync = set, undoable: false);
            return Results.Ok();
        });
        api.MapGet("/camera/devices", () => Results.Json(new
        {
            cameras = CameraCapture.Names(),
            status = CameraCapture.Status,
            error = CameraCapture.Error,
        }, Json.Compact));
        api.MapGet("/camera/snapshot", async (HttpContext ctx) =>
        {
            // a camera picture of the room stays on this PC: not served to phones/tablets
            if (ctx.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
                return Results.Text("camera preview only on this PC", statusCode: 403);
            ScreenSampler.WantPreview();
            for (int i = 0; i < 30; i++)
            {
                byte[] jpg = Program.Screen?.Camera.Jpeg();
                if (jpg != null) return Results.File(jpg, "image/jpeg");
                await Task.Delay(100);
            }
            return Results.Text(CameraCapture.Error ?? "geen beeld", statusCode: 503);
        });
        // what screen sync sees (monitor or straightened TV), for placing the lamp markers; stays on this PC
        api.MapGet("/screen/snapshot", async (HttpContext ctx) =>
        {
            if (ctx.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
                return Results.Text("screen picture only on this PC", statusCode: 403);
            ScreenSyncSettings set = engine.Read(s => s.State.ScreenSync) ?? new ScreenSyncSettings();
            if (set.Source == ScreenSource.Camera) ScreenSampler.WantPreview();
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    byte[] jpg = Program.Screen?.Snapshot(set);
                    if (jpg != null) { ctx.Response.Headers.CacheControl = "no-store"; return Results.File(jpg, "image/jpeg"); }
                }
                catch (Exception ex) { return Results.Text(ex.Message, statusCode: 503); }
                await Task.Delay(100);
            }
            return Results.Text(CameraCapture.Error ?? "geen beeld", statusCode: 503);
        });
        api.MapPost("/camera/detect", async (HttpContext ctx) =>
        {
            if (ctx.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
                return Results.Text("only on this PC", statusCode: 403);
            ScreenSampler.WantPreview();
            for (int i = 0; i < 30; i++)
            {
                var f = Program.Screen?.Camera.Decode();
                if (f is { } frame)
                {
                    double[] corners = CameraFrame.DetectScreen(frame.Pixels, frame.Width, frame.Height, frame.Stride, 3);
                    return Results.Json(new { corners }, Json.Compact);
                }
                await Task.Delay(100);
            }
            return Results.Text(CameraCapture.Error ?? "geen beeld", statusCode: 503);
        });
        api.MapGet("/screen/info", () => Results.Json(new
        {
            camera = CameraCapture.Status,
            cameraError = CameraCapture.Error,
            monitors = ScreenSampler.Monitors(),
            fresh = ScreenFeed.Fresh,
            columns = ScreenFeed.Zones.Select(c => ColorUtil.Hex(c)),
            grid = new { width = ScreenFeed.Grid.Width, height = ScreenFeed.Grid.Height, cells = ScreenFeed.Grid.Cells.Select(c => ColorUtil.Hex(c)) },
        }, Json.Compact));

        // ---------- schedule ----------
        api.MapPut("/schedule", (ScheduleSettings plan) =>
        {
            if (plan == null) return Results.BadRequest();
            plan.Items ??= new();
            plan.Latitude = Math.Clamp(plan.Latitude, -90, 90);
            plan.Longitude = Math.Clamp(plan.Longitude, -180, 180);
            foreach (ScheduleItem i in plan.Items)
            {
                if (string.IsNullOrWhiteSpace(i.Id)) i.Id = Guid.NewGuid().ToString("N")[..8];
                i.Days = (i.Days ?? new()).Where(d => d is >= 1 and <= 7).Distinct().OrderBy(d => d).ToList();
                i.OffsetMinutes = Math.Clamp(i.OffsetMinutes, -720, 720);
                i.FadeSeconds = Math.Clamp(i.FadeSeconds, 0, 4 * 3600);
                i.Command = (i.Command ?? "").Trim();
                i.Name = (i.Name ?? "").Trim();
            }
            engine.Change(s => s.Schedule = plan);
            return Results.Ok();
        });
        api.MapGet("/schedule/info", () =>
        {
            ScheduleSettings plan = engine.Read(s => s.Schedule);
            DateTime now = DateTime.Now;
            var (rise, set) = SunTimes.For(now, plan.Latitude, plan.Longitude);
            var last = ScheduleRunner.LastRun;
            return Results.Json(new
            {
                now,
                sunrise = rise, sunset = set,
                upcoming = Scheduler.Upcoming(plan, now, 12).Select(u => new { id = u.Item.Id, name = u.Item.Name, command = u.Item.Command, at = u.At }),
                last = last == null ? null : new { name = last.Value.Name, command = last.Value.Command, result = last.Value.Result, at = last.Value.At },
            }, Json.Compact);
        });
        api.MapPost("/schedule/run", (IdRequest r) =>
        {
            ScheduleItem item = engine.Read(s => s.Schedule.Items.FirstOrDefault(i => i.Id == r.Id));
            return item == null ? Results.NotFound() : Results.Json(new { result = ScheduleRunner.Run(engine, item) }, Json.Compact);
        });
        api.MapGet("/schedule/check", (string command) => Results.Json(new { ok = DmxLicht.Core.Control.Commands.Validate(engine, command) }, Json.Compact));

        // ---------- updates (GitHub Releases) ----------
        object UpdateInfo() => new
        {
            current = Log.AppVersion,
            latest = Updater.Latest?.Version,
            available = Updater.Available,
            notes = Updater.Latest?.Notes,
            url = Updater.Latest?.Url,
            published = Updater.Latest?.Published,
            state = Updater.State,
            progress = Updater.Progress,
            error = Updater.Error,
            lastCheck = Updater.LastCheck,
            repo = Updater.Repo,
        };
        api.MapGet("/update", () => Results.Json(UpdateInfo(), Json.Compact));
        api.MapPost("/update/check", async () => { await Updater.CheckAsync(); return Results.Json(UpdateInfo(), Json.Compact); });
        api.MapPost("/update/install", (HttpContext ctx) =>
        {
            if (LocalOnly(ctx) is IResult denied) return denied;   // replacing the program: only from this PC
            _ = Task.Run(() => Updater.InstallAsync(() => Program.ExitApp()));
            return Results.Json(UpdateInfo(), Json.Compact);
        });

        // ---------- pairing ----------
        api.MapGet("/pair", (HttpContext ctx) =>
        {
            bool local = ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip);
            bool paired = local || Pairing.IsPaired(engine, ctx.Request.Cookies[Pairing.Cookie]);
            return Results.Json(new { required = engine.Show.Settings.RequirePairing && !local, paired }, Json.Compact);
        });
        api.MapPost("/pair", (HttpContext ctx, PairRequest r) =>
        {
            var (token, error) = Pairing.TryPair(engine, r?.Code, r?.Name, ctx.Connection.RemoteIpAddress?.ToString());
            if (token == null) return Results.Json(new { ok = false, error }, Json.Compact, statusCode: 403);
            ctx.Response.Cookies.Append(Pairing.Cookie, token, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromDays(3650), IsEssential = true,
            });
            return Results.Json(new { ok = true }, Json.Compact);
        });
        api.MapGet("/pair/code", (HttpContext ctx) => LocalOnly(ctx) ?? Results.Json(new { code = Pairing.Code }, Json.Compact));
        api.MapPost("/pair/new-code", (HttpContext ctx) => LocalOnly(ctx) ?? Results.Json(new { code = Pairing.NewCode() }, Json.Compact));
        api.MapPost("/devices/remove", (HttpContext ctx, IdRequest r) =>
        {
            if (LocalOnly(ctx) is IResult denied) return denied;
            engine.Change(s => s.Settings.Devices?.RemoveAll(d => d.Id == r.Id), undoable: false);
            return Results.Ok();
        });

        // ---------- phone / tablet ----------
        api.MapGet("/phone", () => Results.Json(new
        {
            allowNetwork = engine.Show.Settings.AllowNetwork,
            port = Program.WebPort,
            addresses = PhoneAccess.Addresses(),
            hostname = Environment.MachineName,
            fixedName = MdnsResponder.HostName,
            fixedNameStatus = MdnsResponder.Status,
            clients = PhoneAccess.Others(),
            firewall = PhoneAccess.FirewallRuleExists(Program.WebPort),
        }, Json.Compact));
        api.MapPost("/phone/firewall", async () => Results.Json(new { result = await PhoneAccess.OpenFirewallAsync(Program.WebPort, engine.Show.Settings.Control?.OscPort ?? 8000) }, Json.Compact));

        // ---------- problem report: one zip the operator can send ----------
        api.MapGet("/report/info", () => Results.Json(new { crashes = ProblemReport.CrashCount, lastCrash = ProblemReport.LastCrash }, Json.Compact));
        api.MapPost("/report/save", async (ReportRequest r) =>
        {
            string path = await ProblemReport.SaveAndShowAsync(engine, audio, r?.Note);
            return Results.Json(new { path }, Json.Compact);
        });
        api.MapGet("/report.zip", async (HttpRequest req) =>
            Results.File(await ProblemReport.BuildAsync(engine, audio, req.Query["note"].ToString()), "application/zip",
                "Glowsmith-rapport-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip"));

        // last part of the log, newest last (for diagnosing drop-outs from a tablet too)
        api.MapGet("/log", () => Results.Text(string.Join("\n", Log.Tail(300))));
        api.MapPost("/library/reload", () => { engine.ReloadLibrary(); return Results.Ok(); });

        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
            using WebSocket ws = await ctx.WebSockets.AcceptWebSocketAsync();
            using IDisposable tracked = PhoneAccess.Track(ctx.Connection.RemoteIpAddress);
            // also ends when the app stops: an open page must not hold up closing, updating or a web restart
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, app.Lifetime.ApplicationStopping);
            await LiveLoop(ws, engine, audio, stop.Token);
        });
    }

    /// <summary>A live change: fades with the "transition" time and un-highlights the active scene.</summary>
    static void Live(LightEngine engine, Action<ShowFile> change) =>
        engine.Change(s => { change(s); s.State.ActiveScene = null; }, fade: engine.Show.State.FadeMs, cut: true);

    /// <summary>Keeps a chase from the editor within sane limits.</summary>
    static void CleanChase(CustomChase c)
    {
        if (string.IsNullOrWhiteSpace(c.Id) || c.Id.Length > 40) c.Id = Guid.NewGuid().ToString("N")[..8];
        string name = (c.Name ?? "").Trim();
        c.Name = name.Length == 0 ? "Chase" : name[..Math.Min(60, name.Length)];
        c.Columns = Math.Clamp(c.Columns, 1, 64);
        c.StepMs = Math.Clamp(c.StepMs, 20, 600_000);
        c.FadePercent = Math.Clamp(c.FadePercent, 0, 100);
        c.SpeedPercent = Math.Clamp(c.SpeedPercent, 5, 1000);
        c.BeatsPerStep = Math.Clamp(c.BeatsPerStep, 0.125, 64);
        c.RepeatOffset = Math.Clamp(c.RepeatOffset, -64, 64);
        c.Steps ??= new();
        if (c.Steps.Count > 256) c.Steps.RemoveRange(256, c.Steps.Count - 256);
        foreach (ChaseStep st in c.Steps)
        {
            st.Colors ??= new();
            if (st.Colors.Count > c.Columns) st.Colors.RemoveRange(c.Columns, st.Colors.Count - c.Columns);
            if (st.DurationMs is int d) st.DurationMs = Math.Clamp(d, 20, 600_000);
            if (st.FadePercent is int f) st.FadePercent = Math.Clamp(f, 0, 100);
            st.Level = Math.Clamp(st.Level, 0, 100);
        }
    }

    static EffectSettings Clean(EffectSettings e)
    {
        e.EffectIndex = Math.Clamp(e.EffectIndex, 0, EffectLibrary.All.Count - 1);
        e.StepMs = Math.Clamp(e.StepMs, 40, 10000);
        e.OnMs = Math.Clamp(e.OnMs, 20, 10000);
        e.OffMs = Math.Clamp(e.OffMs, 20, 10000);
        e.DelayMs = Math.Clamp(e.DelayMs, 0, 10000);
        if (string.IsNullOrWhiteSpace(e.ChaseId)) e.ChaseId = null;
        return e;
    }

    /// <summary>Settings for the UI: without the PIN, with whether one is set.</summary>
    static object PublicSettings(AppSettings a)
    {
        AppSettings c = SceneOps.Clone(a);
        bool hasPin = !string.IsNullOrEmpty(c.OperatorPin);
        c.OperatorPin = null;
        bool hasMqttPassword = !string.IsNullOrEmpty(c.Mqtt?.Password);
        foreach (PairedDevice d in c.Devices ?? new()) d.TokenHash = null;   // secrets stay on the server
        if (c.Mqtt != null) c.Mqtt.Password = null;   // never to the UI
        var node = JsonSerializer.SerializeToNode(c, Json.Compact)!.AsObject();
        node["hasPin"] = hasPin;
        node["hasMqttPassword"] = hasMqttPassword;
        return node;
    }

    static IEnumerable<(int, int, int)> UsedRanges(LightEngine engine, ShowFile s) =>
        s.Patch.Select(p =>
        {
            FixtureMode m = engine.Library.Find(p.TypeId)?.Mode(p.Mode);
            return (Dmx.Clamp(p.Universe), p.Address, m?.Footprint ?? 1);
        });

    /// <summary>Pushes the full state when it changes and live colours ~20x per second.</summary>
    static async Task LiveLoop(WebSocket ws, LightEngine engine, AudioInput audio, CancellationToken ct)
    {
        int version = 0, sent = -1;
        void OnChanged() => Interlocked.Increment(ref version);
        engine.Changed += OnChanged;
        _ = Task.Run(async () =>
        {
            var buf = new byte[1024];
            try { while (ws.State == WebSocketState.Open) { var r = await ws.ReceiveAsync(buf, ct); if (r.MessageType == WebSocketMessageType.Close) break; } }
            catch { }
        }, ct);
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                int v = Volatile.Read(ref version);
                if (v != sent)
                {
                    sent = v;
                    await Send(ws, new { type = "state", state = State(engine) }, ct);
                }
                var (frameNow, heads) = engine.Snapshot();
                IDmxOutput o = engine.Output;
                long now = engine.Ms;
                var music = new
                {
                    levels = engine.Audio,
                    bpm = Math.Round(engine.Beat.Bpm, 1),
                    beat = engine.Beat.Position(now),
                    following = now - engine.Beat.LastAudioBeatMs < 3000,
                    listening = audio.Running,
                    error = audio.Error,
                };
                TimelinePlayer tp = engine.Timeline;
                var timeline = new { id = tp.TimelineId, playing = tp.Playing, positionMs = Math.Round(tp.PositionMs), waiting = tp.WaitingAt?.Name ?? (tp.WaitingAt != null ? "" : null) };
                var outputs = (o as MultiOutput)?.All;
                var (testCh, testExcl, walk) = engine.TestInfo();
                var test = new { channels = testCh, exclusive = testExcl, walk };
                var (osc, midi, _) = Program.Controls;
                var control = osc == null ? null : new
                {
                    osc = new { port = osc.Port, error = osc.Error, last = osc.Last == null ? null : osc.Last.Address + " " + string.Join(" ", osc.Last.Args) },
                    midi = new { device = midi.Device, error = midi.Error, last = midi.Last, learning = midi.Learning, learned = midi.Learned },
                };
                string frame = Convert.ToBase64String(frameNow, 1, frameNow.Length - 1);   // all universes
                await Send(ws, new { type = "live", heads, output = o.Diagnostics, outputs, music, timeline, test, frame, control }, ct);
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception ex) { Log.Write("Live-verbinding met de interface gestopt: " + ex.Message); }   // the UI reconnects by itself
        finally { engine.Changed -= OnChanged; }
    }

    static Task Send(WebSocket ws, object msg, CancellationToken ct)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, Json.Compact));
        return ws.SendAsync(data, WebSocketMessageType.Text, true, ct);
    }

    // ---------- DTOs ----------

    static object Bootstrap(LightEngine engine) => new
    {
        version = typeof(Api).Assembly.GetName().Version?.ToString(3),
        library = engine.Library.Types.OrderBy(t => t.Custom).ThenBy(t => t.Category == "generic").ThenBy(t => t.Manufacturer).ThenBy(t => t.Model),
        palette = new
        {
            fan = Palette.Fan.Select(PaletteDto),
            specials = Palette.Specials.Select(PaletteDto),
        },
        effects = EffectLibrary.All.Select((e, i) => new
        {
            index = i,
            key = e.Key,
            name = e.Name,
            uses = Enum.GetValues<EffectParams>().Where(p => p != EffectParams.None && e.Uses.HasFlag(p)).Select(p => p.ToString()),
            category = e.Category,
            swatch = e.Swatch,
        }),
    };

    static object PaletteDto(ColorDef c) => new { key = c.Key, name = c.Name, hex = ColorUtil.Hex(c.Show) };

    /// <summary>
    /// The show for the UI, serialised inside the engine lock: the render thread (timeline, flash) and other
    /// requests change these lists, and serialising live objects outside the lock could tear or throw.
    /// </summary>
    static JsonElement State(LightEngine engine) => engine.Read(s =>
    {
        Rig rig = engine.Rig;
        return JsonSerializer.SerializeToElement(new
        {
            name = s.Name,
            undo = engine.UndoCount,
            redo = engine.RedoCount,
            patch = s.Patch,
            state = s.State,
            groups = s.Groups,
            timelines = s.Timelines,
            schedule = s.Schedule,
            scenes = s.Scenes.Select(x => new { x.Id, x.Name, x.Color, x.FadeMs, mode = x.State.Mode }),
            identify = s.State.Identify.ToArray(),
            settings = PublicSettings(s.Settings),
            rig = new
            {
                fixtures = rig.Fixtures.Select(f => new
                {
                    id = f.Patch.Id,
                    firstHead = f.FirstHead,
                    cells = f.CellCount,
                    footprint = f.Mode.Footprint,
                    lastAddress = f.LastAddress,
                    universe = f.Universe,
                    baseChannel = f.BaseChannel,
                    channels = f.Mode.Channels,
                }),
                missing = rig.Missing.Select(p => p.Id),
                conflicts = rig.Conflicts(),
                usedChannels = rig.UsedChannelCount,
                universes = rig.Universes,
                usedPerUniverse = Enumerable.Range(1, rig.Universes).Select(rig.UsedIn),
                heads = rig.Heads.Count,
            },
        }, Json.Compact);
    });
}
