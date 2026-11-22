using System.ComponentModel;
using System.Diagnostics;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Lets Windows restart the uDMX (like unplugging it) with pnputil. This needs administrator rights,
/// so Windows shows a UAC prompt; the user can say no.
/// </summary>
static class UsbRestart
{
    /// <summary>
    /// Switches off Windows' USB power saving for the dongle: selective suspend in the power plan, the device's
    /// own idle settings, and "allow the computer to turn off this device" on every hub up to the root, then
    /// restarts the dongle. A frequent cause of dongles that drop off now and then, mostly on laptops.
    /// Needs administrator rights (UAC). Returns "ok", "cancelled" or an error text.
    /// </summary>
    public static async Task<string> DisablePowerSavingAsync(IReadOnlyList<string> instanceIds)
    {
        // Linux: the udev rule (60-udmx.rules) already keeps the dongle's USB power on
        if (!OperatingSystem.IsWindows()) return "unsupported";
        string ids = string.Join(",", instanceIds.Select(i => "'" + i.Replace("'", "''") + "'"));
        string ps = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            $sub = '2a737441-1930-4402-8d77-b2bebba308a3'; $set = '48e6b7a6-50f5-4782-a5d4-53bb8f07e226'
            powercfg /SETACVALUEINDEX SCHEME_CURRENT $sub $set 0
            powercfg /SETDCVALUEINDEX SCHEME_CURRENT $sub $set 0
            powercfg /SETACTIVE SCHEME_CURRENT
            foreach ($id in @({{ids}})) {
              $k = "HKLM:\SYSTEM\CurrentControlSet\Enum\$id\Device Parameters"
              foreach ($n in 'EnhancedPowerManagementEnabled','SelectiveSuspendEnabled','AllowIdleIrpInD3','DeviceSelectiveSuspended','DeviceIdleEnabled') {
                New-ItemProperty -Path $k -Name $n -Value 0 -PropertyType DWord -Force | Out-Null }
              $p = (Get-PnpDeviceProperty -InstanceId $id -KeyName DEVPKEY_Device_Parent).Data
              while ($p -and $p -match '^USB\\') {
                $pk = "HKLM:\SYSTEM\CurrentControlSet\Enum\$p"
                $cap = [int](Get-ItemProperty $pk -Name PnPCapabilities).PnPCapabilities
                New-ItemProperty -Path $pk -Name PnPCapabilities -Value ($cap -bor 0x18) -PropertyType DWord -Force | Out-Null
                $p = (Get-PnpDeviceProperty -InstanceId $p -KeyName DEVPKEY_Device_Parent).Data }
              pnputil /restart-device "$id" | Out-Null
            }
            exit 0
            """;
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(ps));
        var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded)
        {
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using Process p = Process.Start(psi);
            if (p == null) return "could not start powershell";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await p.WaitForExitAsync(cts.Token);
            Log.Write("USB-stroombesparing voor de uDMX uitgezet, exitcode " + p.ExitCode);
            return p.ExitCode == 0 ? "ok" : "exit code " + p.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Log.Write("USB-stroombesparing uitzetten: beheerdersrechten geweigerd");
            return "cancelled";
        }
        catch (Exception ex)
        {
            Log.Write("USB-stroombesparing uitzetten mislukt: " + ex.Message);
            return ex.Message;
        }
    }

    /// <summary>"ok", "cancelled" or an error text.</summary>
    public static async Task<string> RestartAsync(IReadOnlyList<string> instanceIds)
    {
        // Linux: the normal "USB herstellen" already resets the port through libusb
        if (!OperatingSystem.IsWindows()) return "unsupported";
        // pnputil /restart-device exists from Windows 10 2004; older: disable + enable
        string script = string.Join(" & ", instanceIds.Select(id =>
            $"(pnputil /restart-device \"{id}\" || (pnputil /disable-device \"{id}\" & pnputil /enable-device \"{id}\"))"));
        var psi = new ProcessStartInfo("cmd.exe", "/c " + script)
        {
            Verb = "runas",               // UAC prompt
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using Process p = Process.Start(psi);
            if (p == null) return "could not start pnputil";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await p.WaitForExitAsync(cts.Token);
            Log.Write("uDMX herstart via Windows (pnputil), exitcode " + p.ExitCode);
            return p.ExitCode == 0 ? "ok" : "pnputil exit code " + p.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)   // user said no to UAC
        {
            Log.Write("uDMX herstarten: beheerdersrechten geweigerd");
            return "cancelled";
        }
        catch (Exception ex)
        {
            Log.Write("uDMX herstarten mislukt: " + ex.Message);
            return ex.Message;
        }
    }
}
