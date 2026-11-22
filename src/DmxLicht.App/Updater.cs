using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Updates from GitHub Releases (github.com/inkvolcano/dmx-light-control): checks the latest release, downloads
/// DMX-Light-Control-&lt;version&gt;.zip, checks it against the release's .sha256 file, unpacks it next to the app and lets a
/// small script swap the files after the app has closed, then starts the new version. The Instellingen folder
/// (show, settings, songs) is never touched.
/// </summary>
static class Updater
{
    public const string Repo = "inkvolcano/dmx-light-control";
    /// <summary>Release zip names, newest first: DMX-Light-Control-&lt;version&gt;.zip since 2.1.17, DmxLicht-&lt;version&gt;.zip before.</summary>
    static readonly string[] ZipPrefixes = { "DMX-Light-Control-", "DmxLicht-" };
    static readonly HttpClient http = CreateClient();
    /// <summary>Windows: ".zip". Linux: "-linux-arm64.tar.gz" (or -linux-x64), the package Bouwen-Linux.bat makes.</summary>
    static readonly string Suffix = OperatingSystem.IsWindows() ? ".zip"
        : "-linux-" + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant() + ".tar.gz";

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

    /// <summary>At start: remove what the previous update left behind (the old exe, the downloaded zip).</summary>
    public static void CleanUp()
    {
        try { string old = Path.Combine(AppContext.BaseDirectory, "DmxLicht.old.exe"); if (File.Exists(old)) File.Delete(old); } catch { }
        try
        {
            string dir = Path.Combine(Log.Dir, "Updates");
            if (Directory.Exists(dir))
                foreach (string prefix in ZipPrefixes)
                    foreach (string f in Directory.GetFiles(dir, prefix + "*.zip"))
                        if (!IsNewer(Path.GetFileNameWithoutExtension(f)[prefix.Length..], Log.AppVersion)) File.Delete(f);
        }
        catch { }
    }

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
            var assets = j.GetProperty("assets").EnumerateArray()
                .Select(a => (name: a.GetProperty("name").GetString() ?? "", url: a.GetProperty("browser_download_url").GetString())).ToList();
            // the first name with both the zip and its checksum: the current name, else the old one
            string zip = null, sha = null;
            foreach (string prefix in ZipPrefixes)
            {
                string z = assets.FirstOrDefault(a => a.name.Equals($"{prefix}{version}{Suffix}", StringComparison.OrdinalIgnoreCase)).url;
                string s = assets.FirstOrDefault(a => a.name.Equals($"{prefix}{version}{Suffix}.sha256", StringComparison.OrdinalIgnoreCase)).url;
                if (z != null && s != null) { zip = z; sha = s; break; }
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
        if (!OperatingSystem.IsWindows())
        {
            // the program folder belongs to root (/opt/dmx-light-control); the service cannot replace itself
            State = "error";
            Error = "Automatisch bijwerken kan niet op Linux: download DMX-Light-Control-<versie>" + Suffix +
                    " en draai sudo ./install.sh (zie LEESMIJ-Linux.txt) / Automatic install is not available on Linux";
            return;
        }
        try
        {
            Release rel = Latest ?? await CheckAsync();
            if (rel == null || !IsNewer(rel.Version, Log.AppVersion)) throw new InvalidOperationException("geen nieuwere versie");
            if (rel.ZipUrl == null || rel.ShaUrl == null) throw new InvalidOperationException("de release mist het zip- of sha256-bestand");
            State = "downloading"; Progress = 0; Error = null;

            string dir = Path.Combine(Log.Dir, "Updates");
            Directory.CreateDirectory(dir);
            string zipPath = Path.Combine(dir, $"{ZipPrefixes[0]}{rel.Version}.zip");
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
            // start the new version the way this one was started (--browser, --headless, ...)
            string args = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(x => "\"" + x.Replace("\"", "") + "\""));
            string script = Path.Combine(Path.GetTempPath(), "dmxlicht-update.cmd");
            File.WriteAllText(script,
                "@echo off\r\n" +
                "title DMX Light Control bijwerken\r\n" +
                $"set PID={Environment.ProcessId}\r\n" +
                ":wait\r\n" +
                // full paths: a "find" from Git or Cygwin earlier in PATH would end the wait too soon
                "\"%SystemRoot%\\System32\\tasklist.exe\" /FI \"PID eq %PID%\" 2>nul | \"%SystemRoot%\\System32\\find.exe\" \"%PID%\" >nul && (\"%SystemRoot%\\System32\\ping.exe\" -n 2 127.0.0.1 >nul & goto wait)\r\n" +
                $"cd /d \"{appDir}\"\r\n" +
                // Windows keeps a just-closed program locked for a moment (and virus scanners may too); a locked
                // exe can still be renamed, so move it aside and put the new one in its place
                "if exist DmxLicht.old.exe del /f /q DmxLicht.old.exe >nul 2>&1\r\n" +
                "move /y DmxLicht.exe DmxLicht.old.exe >nul 2>&1\r\n" +
                $"robocopy \"{stage}\" \"{appDir}\" /E /XD Instellingen /R:30 /W:1 /NFL /NDL /NJH /NJS >nul\r\n" +
                // anything went wrong: the old version comes back, so the app always starts again
                "if not exist DmxLicht.exe move /y DmxLicht.old.exe DmxLicht.exe >nul 2>&1\r\n" +
                $"start \"\" \"{Path.Combine(appDir, "DmxLicht.exe")}\" {args}\r\n" +
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
