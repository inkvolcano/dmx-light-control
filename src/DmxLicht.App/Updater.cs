using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Updates from GitHub Releases (github.com/inkvolcano/dmx-light-control): checks the latest release, downloads
/// DmxLicht-&lt;version&gt;.zip, checks it against the release's .sha256 file, unpacks it next to the app and lets a
/// small script swap the files after the app has closed, then starts the new version. The Instellingen folder
/// (show, settings, songs) is never touched.
/// </summary>
static class Updater
{
    public const string Repo = "inkvolcano/dmx-light-control";
    static readonly HttpClient http = CreateClient();

    public sealed record Release(string Version, string Notes, string Url, string ZipUrl, string ShaUrl, DateTime? Published);

    public static string State { get; private set; } = "idle";   // idle, checking, downloading, ready, installing, error
    public static string Error { get; private set; }
    public static int Progress { get; private set; }
    public static Release Latest { get; private set; }
    public static DateTime? LastCheck { get; private set; }

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DmxLicht", Log.AppVersion));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>"2.1.6" &gt; "2.1.5"?</summary>
    public static bool IsNewer(string candidate, string current) =>
        Version.TryParse(Clean(candidate), out Version a) && Version.TryParse(Clean(current), out Version b) && a > b;
    static string Clean(string v) => (v ?? "").Trim().TrimStart('v', 'V').Split('+', '-')[0];

    public static bool Available => Latest != null && IsNewer(Latest.Version, Log.AppVersion);

    public static async Task<Release> CheckAsync()
    {
        State = "checking"; Error = null;
        try
        {
            using var r = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) { Latest = null; State = "idle"; LastCheck = DateTime.Now; return null; }
            r.EnsureSuccessStatusCode();
            using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            JsonElement j = doc.RootElement;
            string version = Clean(j.GetProperty("tag_name").GetString());
            string zip = null, sha = null;
            foreach (JsonElement a in j.GetProperty("assets").EnumerateArray())
            {
                string name = a.GetProperty("name").GetString() ?? "";
                string url = a.GetProperty("browser_download_url").GetString();
                if (name.Equals($"DmxLicht-{version}.zip", StringComparison.OrdinalIgnoreCase)) zip = url;
                else if (name.Equals($"DmxLicht-{version}.zip.sha256", StringComparison.OrdinalIgnoreCase)) sha = url;
            }
            Latest = new Release(version, j.TryGetProperty("body", out var b) ? b.GetString() : "", j.GetProperty("html_url").GetString(), zip, sha,
                j.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String ? p.GetDateTime() : null);
            LastCheck = DateTime.Now;
            State = "idle";
            if (Available) Log.Write("Nieuwe versie beschikbaar: " + version);
            return Latest;
        }
        catch (Exception ex)
        {
            State = "error"; Error = "controleren mislukt: " + ex.Message;
            return null;
        }
    }

    /// <summary>Downloads, verifies and unpacks the latest release; then starts the swap and closes the app.</summary>
    public static async Task InstallAsync(Action exitApp)
    {
        if (State is "downloading" or "installing") return;
        try
        {
            Release rel = Latest ?? await CheckAsync();
            if (rel == null || !IsNewer(rel.Version, Log.AppVersion)) throw new InvalidOperationException("geen nieuwere versie");
            if (rel.ZipUrl == null || rel.ShaUrl == null) throw new InvalidOperationException("de release mist het zip- of sha256-bestand");
            State = "downloading"; Progress = 0; Error = null;

            string dir = Path.Combine(Log.Dir, "Updates");
            Directory.CreateDirectory(dir);
            string zipPath = Path.Combine(dir, $"DmxLicht-{rel.Version}.zip");
            string expected = (await http.GetStringAsync(rel.ShaUrl)).Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();

            using (HttpResponseMessage r = await http.GetAsync(rel.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                r.EnsureSuccessStatusCode();
                long total = r.Content.Headers.ContentLength ?? 0;
                await using Stream src = await r.Content.ReadAsStreamAsync();
                await using var dst = File.Create(zipPath);
                var buf = new byte[81920]; long done = 0; int n;
                while ((n = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n));
                    done += n;
                    if (total > 0) Progress = (int)(done * 100 / total);
                }
            }
            string actual;
            await using (var fs = File.OpenRead(zipPath)) actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
            if (actual != expected) { File.Delete(zipPath); throw new InvalidOperationException("controlegetal klopt niet: download beschadigd of niet echt"); }

            State = "installing";
            string stage = Path.Combine(dir, "nieuw-" + rel.Version);
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            ZipFile.ExtractToDirectory(zipPath, stage);
            if (!File.Exists(Path.Combine(stage, "DmxLicht.exe"))) throw new InvalidOperationException("DmxLicht.exe ontbreekt in de update");

            string appDir = AppContext.BaseDirectory.TrimEnd('\\');
            string script = Path.Combine(Path.GetTempPath(), "dmxlicht-update.cmd");
            File.WriteAllText(script,
                "@echo off\r\n" +
                "title DmxLicht bijwerken\r\n" +
                $"set PID={Environment.ProcessId}\r\n" +
                ":wait\r\n" +
                "tasklist /FI \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                $"robocopy \"{stage}\" \"{appDir}\" /E /XD Instellingen /R:10 /W:1 /NFL /NDL /NJH /NJS >nul\r\n" +
                $"start \"\" \"{Path.Combine(appDir, "DmxLicht.exe")}\"\r\n" +
                $"rmdir /s /q \"{stage}\" >nul 2>&1\r\n" +
                "del \"%~f0\"\r\n");
            Log.Write($"Bijwerken naar {rel.Version}: app sluit en start opnieuw");
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            await Task.Delay(500);
            exitApp();
        }
        catch (Exception ex)
        {
            State = "error"; Error = ex.Message;
            Log.Write("Bijwerken mislukt: " + ex.Message);
        }
    }
}
