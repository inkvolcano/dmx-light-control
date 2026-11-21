using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DmxLicht.Core;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Output;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.App;

/// <summary>
/// One zip with everything needed to diagnose a problem from a distance: the log (also the previous one),
/// crash files, app/Windows info, the dongle as Windows sees it (driver, status, USB power saving,
/// plug/unplug events), output diagnostics and the show without PIN or trigger key.
/// </summary>
static class ProblemReport
{
    public static int CrashCount => Directory.Exists(Log.CrashDir) ? Directory.GetFiles(Log.CrashDir, "crash-*.txt").Length : 0;

    public static DateTime? LastCrash =>
        Directory.Exists(Log.CrashDir)
            ? Directory.GetFiles(Log.CrashDir, "crash-*.txt").Select(File.GetLastWriteTime).DefaultIfEmpty().Max() is DateTime d && d != default ? d : null
            : null;

    /// <summary>Builds the zip in memory.</summary>
    public static async Task<byte[]> BuildAsync(LightEngine engine, AudioInput audio, string note)
    {
        Log.Write("Probleemrapport gemaakt");
        string usb = await UsbInfoAsync();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Text(zip, "LEESMIJ.txt",
                "Glowsmith probleemrapport\r\n=========================\r\n" +
                "Gemaakt: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" +
                (string.IsNullOrWhiteSpace(note) ? "" : "Wat er gebeurde (door de gebruiker):\r\n" + note.Trim() + "\r\n\r\n") +
                "info.txt      - versie, Windows, uitgangen, lampen\r\n" +
                "usb.txt       - de dongle zoals Windows hem ziet (driver, status, stroombesparing, in/uitsteken)\r\n" +
                "dmx-log*.txt  - logboek van de app\r\n" +
                "Crashes/      - foutmeldingen bij crashes\r\n" +
                "show.json     - de show (zonder PIN en sleutel)\r\n");
            Text(zip, "info.txt", Info(engine, audio));
            Text(zip, "usb.txt", usb);
            foreach (string p in new[] { Log.OldFilePath, Log.FilePath }) Copy(zip, p, Path.GetFileName(p));
            if (Directory.Exists(Log.CrashDir))
                foreach (string p in Directory.GetFiles(Log.CrashDir, "crash-*.txt").OrderByDescending(x => x).Take(20))
                    Copy(zip, p, "Crashes/" + Path.GetFileName(p));
            Text(zip, "show.json", engine.Read(s =>
            {
                ShowFile c = SceneOps.Clone(s);
                c.Settings.OperatorPin = string.IsNullOrEmpty(c.Settings.OperatorPin) ? null : "(verborgen)";
                if (c.Settings.Control != null) c.Settings.Control.TriggerKey = "(verborgen)";
                if (!string.IsNullOrEmpty(c.Settings.Mqtt?.Password)) c.Settings.Mqtt.Password = "(verborgen)";
                return JsonSerializer.Serialize(c, Json.Options);
            }));
        }
        return ms.ToArray();
    }

    /// <summary>Saves the report on the desktop (else in Instellingen) and shows it in Explorer.</summary>
    public static async Task<string> SaveAndShowAsync(LightEngine engine, AudioInput audio, string note)
    {
        byte[] data = await BuildAsync(engine, audio, note);
        string name = "Glowsmith-rapport-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip";
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Log.Dir;
        string path = Path.Combine(dir, name);
        try { await File.WriteAllBytesAsync(path, data); }
        catch { path = Path.Combine(Log.Dir, name); await File.WriteAllBytesAsync(path, data); }
        try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); } catch { }
        return path;
    }

    static string Info(LightEngine engine, AudioInput audio)
    {
        var b = new StringBuilder();
        b.AppendLine("Versie:       Glowsmith " + Log.AppVersion);
        b.AppendLine("Map:          " + AppDomain.CurrentDomain.BaseDirectory);
        b.AppendLine("Windows:      " + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " 64-bit" : " 32-bit"));
        b.AppendLine(".NET:         " + Environment.Version);
        b.AppendLine("Taal:         " + System.Globalization.CultureInfo.CurrentCulture.Name);
        b.AppendLine("App draait:   " + (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"d\.hh\:mm\:ss"));
        b.AppendLine("Geheugen:     " + Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024) + " MB");
        b.AppendLine("Accu:         " + Battery());
        b.AppendLine("Crashes:      " + CrashCount);
        b.AppendLine();
        b.AppendLine("Uitgangen:");
        if (engine.Output is MultiOutput mo)
            foreach (OutputDiagnostics d in mo.All)
                b.AppendLine("  " + JsonSerializer.Serialize(d, Json.Compact));
        else b.AppendLine("  " + JsonSerializer.Serialize(engine.Output.Diagnostics, Json.Compact));
        b.AppendLine();
        engine.Read(s =>
        {
            b.AppendLine("Instellingen: dongle-snelheid " + s.Settings.DongleSpeed + ", wakker houden " + s.Settings.PreventSleep +
                         ", netwerk " + s.Settings.AllowNetwork + ", vergrendeld " + s.Settings.Locked);
            b.AppendLine("Lampen:       " + s.Patch.Count + " (" + engine.Rig.UsedChannelCount + " kanalen, " + engine.Rig.Heads.Count + " koppen)");
            foreach (var p in s.Patch) b.AppendLine("  " + p.Address.ToString().PadLeft(3) + "  " + p.TypeId + " / " + p.Mode + "  " + p.Name);
            var conflicts = engine.Rig.Conflicts();
            if (conflicts.Any()) b.AppendLine("Adresconflicten: " + JsonSerializer.Serialize(conflicts, Json.Compact));
            b.AppendLine("Scenes: " + s.Scenes.Count + ", tijdlijnen: " + s.Timelines.Count + ", eigen chases: " + s.State.Chases.Count);
            b.AppendLine("Modus nu: " + s.State.Mode + ", helderheid " + s.State.BrightnessPercent + "%, blackout " + s.State.Blackout);
            return 0;
        });
        b.AppendLine("Geluid:       luistert " + audio.Running + (audio.Error != null ? ", fout: " + audio.Error : ""));
        b.AppendLine("Home Assistant: " + HomeAssistant.Status + (HomeAssistant.Error != null ? " (" + HomeAssistant.Error + ")" : ""));
        b.AppendLine();
        // Windows' own view: did the dongle leave the USB bus, did the PC sleep, did the app crash?
        b.Append(AppHealth.WindowsEvents());
        return b.ToString();
    }

    static string Battery()
    {
        try
        {
            PowerStatus p = SystemInformation.PowerStatus;
            return p.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) ? "geen (desktop)"
                : (p.PowerLineStatus == PowerLineStatus.Online ? "aan de lader" : "OP ACCU") + ", " + (int)(p.BatteryLifePercent * 100) + "%";
        }
        catch { return "?"; }
    }

    /// <summary>What Windows knows about the dongle; plain PowerShell queries, nothing is changed.</summary>
    static async Task<string> UsbInfoAsync()
    {
        const string ps = """
            $ErrorActionPreference = 'SilentlyContinue'
            "== uDMX-apparaten (VID_16C0 PID_05DC) =="
            $devs = Get-PnpDevice | Where-Object { $_.InstanceId -match 'VID_16C0&PID_05DC' }
            if (-not $devs) { "(geen gevonden - nooit ingestoken, of andere VID/PID)" }
            foreach ($d in $devs) {
              "Naam: $($d.FriendlyName)  Status: $($d.Status)  Aanwezig: $($d.Present)  Klasse: $($d.Class)"
              "Id: $($d.InstanceId)"
              foreach ($k in 'DEVPKEY_Device_DriverProvider','DEVPKEY_Device_DriverVersion','DEVPKEY_Device_DriverInfPath','DEVPKEY_Device_ProblemCode','DEVPKEY_Device_Parent','DEVPKEY_Device_LocationInfo') {
                $v = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName $k).Data; "  $k = $v" }
              $p = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName DEVPKEY_Device_Parent).Data
              $chain = @()
              while ($p) { $pd = Get-PnpDevice -InstanceId $p; $chain += "$($pd.FriendlyName) [$p]"; $p = (Get-PnpDeviceProperty -InstanceId $p -KeyName DEVPKEY_Device_Parent).Data; if ($chain.Count -gt 6) { break } }
              "  USB-pad: " + ($chain -join '  <-  ')
              $reg = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Enum\$($d.InstanceId)\Device Parameters"
              "  Stroombesparing apparaat: EnhancedPowerManagementEnabled=$($reg.EnhancedPowerManagementEnabled) SelectiveSuspendEnabled=$($reg.SelectiveSuspendEnabled) DeviceIdleEnabled=$($reg.DeviceIdleEnabled)"
              ""
            }
            "== USB selectieve onderbreking (energieplan, 0 = uit) =="
            powercfg /QUERY SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 | Select-String 'Current|Huidige|Index'
            ""
            "== In-/uitsteken van de dongle (laatste 7 dagen, Windows-logboek) =="
            $since = (Get-Date).AddDays(-7)
            $ev = Get-WinEvent -FilterHashtable @{ LogName='Microsoft-Windows-Kernel-PnP/Configuration'; StartTime=$since } -MaxEvents 3000 |
                  Where-Object { $_.Message -match 'VID_16C0' } | Select-Object -First 60
            if (-not $ev) { "(geen gebeurtenissen gevonden)" }
            foreach ($e in $ev) { "$($e.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss'))  [$($e.Id)]  " + ($e.Message -split "`n")[0] }
            ""
            "== Andere USB-problemen (laatste 7 dagen) =="
            Get-WinEvent -FilterHashtable @{ LogName='System'; StartTime=$since } -MaxEvents 5000 |
              Where-Object { $_.ProviderName -match 'USB|Kernel-PnP' -and $_.Level -le 3 } | Select-Object -First 30 |
              ForEach-Object { "$($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss'))  $($_.ProviderName) [$($_.Id)]  " + ($_.Message -split "`n")[0] }
            """;
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(ps)))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using Process p = Process.Start(psi)!;
            Task<string> output = p.StandardOutput.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { p.Kill(); } catch { } return "USB-gegevens: duurde te lang\r\n" + await output; }
            return await output;
        }
        catch (Exception ex) { return "USB-gegevens niet op te vragen: " + ex.Message; }
    }

    static void Text(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(true));
        w.Write(text);
    }

    static void Copy(ZipArchive zip, string path, string name)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = zip.CreateEntry(name).Open();
            src.CopyTo(dst);
        }
        catch { }
    }
}
