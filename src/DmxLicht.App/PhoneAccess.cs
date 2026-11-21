using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>Everything for using the app from a phone or tablet: addresses, who is connected, the firewall.</summary>
static class PhoneAccess
{
    public sealed record Address(string Ip, string Network, bool Preferred);
    public sealed record Client(string Ip, DateTime Since);

    static readonly ConcurrentDictionary<Guid, Client> clients = new();

    /// <summary>Registers a live connection (the UI's WebSocket); dispose to unregister.</summary>
    public static IDisposable Track(IPAddress ip)
    {
        var id = Guid.NewGuid();
        clients[id] = new Client(Clean(ip), DateTime.Now);
        return new Untrack(id);
    }

    sealed class Untrack(Guid id) : IDisposable { public void Dispose() => clients.TryRemove(id, out _); }

    static string Clean(IPAddress ip) => ip == null ? "?" : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();

    /// <summary>Other devices that have the app open now (not this PC).</summary>
    public static List<Client> Others() =>
        clients.Values.Where(c => c.Ip != "127.0.0.1" && c.Ip != "::1")
            .GroupBy(c => c.Ip).Select(g => g.OrderBy(c => c.Since).First()).OrderBy(c => c.Since).ToList();

    /// <summary>IPv4 addresses a phone can use: the Wi-Fi/Ethernet with a gateway first, virtual adapters last.</summary>
    public static List<Address> Addresses()
    {
        var list = new List<Address>();
        try
        {
            foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                IPInterfaceProperties p = n.GetIPProperties();
                bool gateway = p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                bool virt = n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) || n.Name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase)
                            || n.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase);
                foreach (UnicastIPAddressInformation a in p.UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = a.Address.ToString();
                    if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                    list.Add(new Address(ip, n.Name, gateway && !virt));
                }
            }
        }
        catch { }
        return list.OrderByDescending(a => a.Preferred).ToList();
    }

    static string RuleName(int port) => "DmxLicht (telefoon, poort " + port + ")";

    /// <summary>Whether our firewall rule exists (reading needs no admin rights).</summary>
    public static bool FirewallRuleExists(int port)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{RuleName(port)}\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using Process p = Process.Start(psi)!;
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Lets phones in (UAC prompt): an inbound rule for this port, only from the local network (remoteip=localsubnet),
    /// on every network profile because home Wi-Fi is often marked "public". Returns "ok", "cancelled" or an error.
    /// </summary>
    public static async Task<string> OpenFirewallAsync(int port, int oscPort)
    {
        string name = RuleName(port), osc = "DmxLicht (OSC, UDP " + oscPort + ")", mdns = "DmxLicht (vaste naam, mDNS)";
        string exe = Environment.ProcessPath ?? "";
        string args = $"/c netsh advfirewall firewall delete rule name=\"{name}\" >nul 2>&1 & " +
                      $"netsh advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=TCP localport={port} remoteip=localsubnet profile=any & " +
                      $"netsh advfirewall firewall delete rule name=\"{osc}\" >nul 2>&1 & " +
                      $"netsh advfirewall firewall add rule name=\"{osc}\" dir=in action=allow protocol=UDP localport={oscPort} remoteip=localsubnet profile=any & " +
                      $"netsh advfirewall firewall delete rule name=\"{mdns}\" >nul 2>&1 & " +
                      $"netsh advfirewall firewall add rule name=\"{mdns}\" dir=in action=allow protocol=UDP localport=5353 remoteip=localsubnet profile=any program=\"{exe}\"";
        var psi = new ProcessStartInfo("cmd.exe", args) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
        try
        {
            using Process p = Process.Start(psi);
            if (p == null) return "could not start netsh";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await p.WaitForExitAsync(cts.Token);
            Log.Write("Firewall-regel voor telefoons (poort " + port + "), exitcode " + p.ExitCode);
            return p.ExitCode == 0 ? "ok" : "exit code " + p.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return "cancelled"; }
        catch (Exception ex) { Log.Write("Firewall-regel mislukt: " + ex.Message); return ex.Message; }
    }
}
