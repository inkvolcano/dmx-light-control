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
/// Linux (the Pi service): the program folder belongs to root, so the app downloads and checks the
/// -linux-&lt;arch&gt;.tar.gz and hands it to the root update helper (linux/update.sh, allowed by a sudoers rule that
/// install.sh writes). The helper checks it again against GitHub, runs the package's install.sh in its own systemd
/// unit and puts the old version back if the new one does not start.
/// </summary>
static class Updater
{
    public const string Repo = "inkvolcano/dmx-light-control";
    static readonly string[] ZipPrefixes = UpdatePackage.Prefixes;
    static readonly HttpClient http = CreateClient();
    /// <summary>Windows: ".zip". Linux: "-linux-arm64.tar.gz" (or -linux-x64), the package Bouwen-Linux.bat makes.</summary>
    static readonly string Suffix = UpdatePackage.Suffix(OperatingSystem.IsWindows(), System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
    /// <summary>What the Linux update helper leaves in the data folder after installing (read and removed at start).</summary>
    static string ResultFile => Path.Combine(Log.Dir, "update-result.txt");
    static bool? helperReady;
    /// <summary>Linux: the last update failed and was rolled back; stays shown until the next install attempt.</summary>
    static string lastFailure;

    /// <summary>A release; PiUrl = its Raspberry Pi package (linux-arm64), when the release has one.</summary>
    public sealed record Release(string Version, string Notes, string Url, string ZipUrl, string ShaUrl, DateTime? Published, string PiUrl = null);

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

    /// <summary>
    /// Can the install button work here? Windows: always. Linux: only when install.sh (2.1.44 or newer) put the update
    /// helper and its sudoers rule in place; older installs keep the manual "sudo bash install.sh" hint.
    /// </summary>
    public static bool CanInstall => OperatingSystem.IsWindows() || (helperReady ??= ProbeHelper());

    /// <summary>Linux: is the helper there, and may this user run it with sudo without a password? (sudo -n -l only asks.)</summary>
    static bool ProbeHelper()
    {
        if (OperatingSystem.IsWindows() || !File.Exists(UpdatePackage.LinuxHelper)) return false;
        try
        {
            var psi = new ProcessStartInfo("sudo") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string a in new[] { "-n", "-l", UpdatePackage.LinuxHelper }) psi.ArgumentList.Add(a);
            using Process p = Process.Start(psi);
            _ = p.StandardOutput.ReadToEndAsync(); _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>At start: remove what the previous update left behind (the old exe, the downloaded zip).</summary>
    public static void CleanUp()
    {
        try { string old = Path.Combine(AppContext.BaseDirectory, "DmxLicht.old.exe"); if (File.Exists(old)) File.Delete(old); } catch { }
        try
        {
            string dir = Path.Combine(Log.Dir, "Updates");
            if (Directory.Exists(dir))
                foreach (string prefix in ZipPrefixes)
                    foreach (string ext in new[] { ".zip", Suffix }.Distinct())
                        foreach (string f in Directory.GetFiles(dir, prefix + "*" + ext))
                            if (!IsNewer(Path.GetFileName(f)[prefix.Length..], Log.AppVersion)) File.Delete(f);
        }
        catch { }
        // Linux: how the last update went (the helper writes it; after a failed one the old version runs again)
        try
        {
            if (!OperatingSystem.IsWindows() && File.Exists(ResultFile))
            {
                var r = UpdatePackage.ParseResult(File.ReadAllText(ResultFile));
                File.Delete(ResultFile);
                if (r is { Ok: true } ok) Log.Write("Bijgewerkt naar " + ok.Version);
                else if (r is { } bad)
                {
                    // State stays idle: the old version runs fine, the message only says why it is still the old one
                    Error = lastFailure = $"Bijwerken naar {bad.Version} is mislukt, de vorige versie draait weer: {bad.Reason} (journalctl -u dmx-light-control-update)";
                    Log.Write(Error);
                }
            }
        }
        catch { }
    }

    public static async Task<Release> CheckAsync()
    {
        State = "checking"; Error = lastFailure;
        try
        {
            using var r = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) { Latest = null; State = "idle"; LastCheck = DateTime.Now; return null; }
            r.EnsureSuccessStatusCode();
            using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            JsonElement j = doc.RootElement;
            string version = Clean(j.GetProperty("tag_name").GetString());
            var assets = j.GetProperty("assets").EnumerateArray()
                .Select(a => (Name: a.GetProperty("name").GetString() ?? "", Url: a.GetProperty("browser_download_url").GetString())).ToList();
            // the first name with both the package and its checksum: the current name, else the old one
            (string zip, string sha) = UpdatePackage.Pick(assets, version, Suffix);
            string pi = assets.FirstOrDefault(a => a.Name.EndsWith("-linux-arm64.tar.gz", StringComparison.OrdinalIgnoreCase)).Url;
            Latest = new Release(version, j.TryGetProperty("body", out var b) ? b.GetString() : "", j.GetProperty("html_url").GetString(), zip, sha,
                j.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String ? p.GetDateTime() : null, pi);
            LastCheck = DateTime.Now;
            State = "idle";
            if (!OperatingSystem.IsWindows()) helperReady = ProbeHelper();   // a manual install.sh may have added the helper since
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
        if (!CanInstall)
        {
            // an install from 2.1.43 or older: no update helper yet, and the service cannot replace the root-owned program
            State = "error";
            Error = "Automatisch bijwerken kan hier nog niet: download DMX-Light-Control-<versie>" + Suffix +
                    " en draai een keer sudo bash install.sh (zie LEESMIJ-Linux.txt); daarna werkt de knop / run install.sh by hand once first";
            return;
        }
        try
        {
            Release rel = Latest ?? await CheckAsync();
            if (rel == null || !IsNewer(rel.Version, Log.AppVersion)) throw new InvalidOperationException("geen nieuwere versie");
            if (rel.ZipUrl == null || rel.ShaUrl == null)
                throw new InvalidOperationException($"de release mist {UpdatePackage.FileName(rel.Version, Suffix)} of het sha256-bestand");
            State = "downloading"; Progress = 0; Error = lastFailure = null;

            string dir = Path.Combine(Log.Dir, "Updates");
            Directory.CreateDirectory(dir);
            string zipPath = Path.Combine(dir, UpdatePackage.FileName(rel.Version, Suffix));
            string expected = UpdatePackage.ParseSha256(await http.GetStringAsync(rel.ShaUrl))
                ?? throw new InvalidOperationException("het sha256-bestand van de release is onleesbaar");

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
            if (!OperatingSystem.IsWindows()) { await HandToHelperAsync(rel.Version, zipPath); return; }
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

    /// <summary>
    /// Linux: "sudo -n update.sh &lt;version&gt; &lt;package&gt;". While we wait, the helper checks and unpacks the package (a
    /// failure there changes nothing); then it starts the install in its own systemd unit, which stops this service,
    /// installs and starts it again. So on success this process is simply stopped a moment later.
    /// </summary>
    static async Task HandToHelperAsync(string version, string package)
    {
        var psi = new ProcessStartInfo("sudo") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in UpdatePackage.HelperArgs(version, package)) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi) ?? throw new InvalidOperationException("sudo start niet");
        Task<string> stdout = p.StandardOutput.ReadToEndAsync(), stderr = p.StandardError.ReadToEndAsync();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
        {
            try { await p.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
                throw new InvalidOperationException("de update-helper reageert niet (5 minuten)");
            }
        }
        string err = (await stderr).Trim(), outp = (await stdout).Trim();
        if (p.ExitCode != 0)
        {
            string why = err.Split('\n').LastOrDefault(l => l.Trim().Length > 0)?.Trim();
            if (err.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                helperReady = false;
                why = "sudo vraagt een wachtwoord; draai sudo bash install.sh opnieuw";
            }
            throw new InvalidOperationException("bijwerken niet gestart: " + (why ?? "fout " + p.ExitCode));
        }
        Log.Write($"Bijwerken naar {version}: de dienst stopt en start zo opnieuw" + (outp.Length > 0 ? " (" + outp + ")" : ""));
        // normally systemd stops us within seconds; still running long after that: say so instead of "installing" forever
        await Task.Delay(TimeSpan.FromMinutes(4));
        if (State == "installing")
        {
            State = "error";
            Error = "de update is gestart maar de dienst is niet herstart; kijk in journalctl -u dmx-light-control-update";
        }
    }
}
