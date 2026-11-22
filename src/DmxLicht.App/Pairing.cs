using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Storage;

namespace DmxLicht.App;

/// <summary>
/// Pairing phones and tablets: a 6-digit code (also inside the QR code) turns into a long secret in a cookie on
/// that device. Without it, devices on the network get only the pairing screen. This PC itself, trigger links
/// with the key, and Companion (key) are always allowed. Codes are single-use; guessing is rate-limited.
/// </summary>
static class Pairing
{
    public const string Cookie = "dmxdev";
    static string code = NewCodeValue();
    static readonly ConcurrentDictionary<string, (int Count, DateTime Since)> attempts = new();

    public static string Code => code;
    /// <summary>Raised with the new code (the Linux build shows it in the journal and in koppelcode.txt).</summary>
    public static event Action<string> CodeChanged;
    public static string NewCode()
    {
        code = NewCodeValue();
        CodeChanged?.Invoke(code);
        return code;
    }
    static string NewCodeValue() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Is this request from a paired device (by its cookie)? Updates "last seen" at most every 10 minutes.</summary>
    public static bool IsPaired(LightEngine engine, string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        string h = Hash(token);
        PairedDevice d = engine.Read(s => s.Settings.Devices?.FirstOrDefault(x => x.TokenHash == h));
        if (d == null) return false;
        if (DateTime.Now - d.LastSeen > TimeSpan.FromMinutes(10))
            engine.Change(s => { var x = s.Settings.Devices.FirstOrDefault(y => y.Id == d.Id); if (x != null) x.LastSeen = DateTime.Now; }, undoable: false);
        return true;
    }

    /// <summary>Checks the code; on success stores a new device and returns its secret (for the cookie), else null.</summary>
    public static (string Token, string Error) TryPair(LightEngine engine, string given, string name, string ip)
    {
        var now = DateTime.Now;
        var a = attempts.AddOrUpdate(ip ?? "?", _ => (1, now), (_, o) => now - o.Since > TimeSpan.FromMinutes(1) ? (1, now) : (o.Count + 1, o.Since));
        if (a.Count > 8) return (null, "too many attempts, wait a minute");
        if (string.IsNullOrWhiteSpace(given) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given.Trim()), Encoding.UTF8.GetBytes(code)))
            return (null, "wrong code");
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var dev = new PairedDevice { Name = string.IsNullOrWhiteSpace(name) ? "Telefoon" : name.Trim()[..Math.Min(60, name.Trim().Length)], TokenHash = Hash(token) };
        engine.Change(s => { s.Settings.Devices ??= new(); s.Settings.Devices.Add(dev); }, undoable: false);
        NewCode();   // single use
        attempts.TryRemove(ip ?? "?", out _);
        DmxLicht.Core.Log.Write("Apparaat gekoppeld: " + dev.Name + " (" + ip + ")");
        return (token, null);
    }
}
