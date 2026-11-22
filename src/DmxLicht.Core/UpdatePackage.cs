using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DmxLicht.Core;

/// <summary>
/// The pure part of updating (no network, no files): which release file belongs to this computer, how to read a
/// .sha256 file, and how the Linux app asks its root update helper to install a package. The app's Updater does the rest.
/// </summary>
public static class UpdatePackage
{
    /// <summary>Release file names, newest first: DMX-Light-Control-&lt;version&gt; since 2.1.17, DmxLicht-&lt;version&gt; before.</summary>
    public static readonly string[] Prefixes = { "DMX-Light-Control-", "DmxLicht-" };

    /// <summary>The root-owned helper install.sh puts on a Pi; a sudoers rule lets the service run only this.</summary>
    public const string LinuxHelper = "/usr/local/lib/dmx-light-control/update.sh";

    /// <summary>Windows: ".zip". Linux: "-linux-arm64.tar.gz" (or -linux-x64), the package Bouwen-Linux.bat makes.</summary>
    public static string Suffix(bool windows, Architecture arch) =>
        windows ? ".zip" : "-linux-" + arch.ToString().ToLowerInvariant() + ".tar.gz";

    /// <summary>The file name a download is saved under: always the current prefix.</summary>
    public static string FileName(string version, string suffix) => Prefixes[0] + version + suffix;

    /// <summary>
    /// The package and its checksum file among a release's assets: the first prefix that has both (a release with only
    /// one of the two is not installable). Nulls when there is none.
    /// </summary>
    public static (string Package, string Sha) Pick(IEnumerable<(string Name, string Url)> assets, string version, string suffix)
    {
        var list = assets.ToList();
        foreach (string prefix in Prefixes)
        {
            string name = prefix + version + suffix;
            string pkg = list.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)).Url;
            string sha = list.FirstOrDefault(a => string.Equals(a.Name, name + ".sha256", StringComparison.OrdinalIgnoreCase)).Url;
            if (pkg != null && sha != null) return (pkg, sha);
        }
        return (null, null);
    }

    /// <summary>"&lt;64 hex&gt;  name" (sha256sum style) or just the hash: the hash in lower case, or null when it is not one.</summary>
    public static string ParseSha256(string text)
    {
        string first = (text ?? "").Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first == null) return null;
        first = first.TrimStart('*').ToLowerInvariant();
        return Regex.IsMatch(first, "^[0-9a-f]{64}$") ? first : null;
    }

    /// <summary>A bare release number like "2.1.44": the only kind the update helper accepts (no "v", no suffix).</summary>
    public static bool IsPlainVersion(string version) =>
        version != null && Regex.IsMatch(version, @"^[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,6}$");

    /// <summary>
    /// The arguments for "sudo": -n (never ask for a password; fail instead), the helper, the version and the package.
    /// Each is its own argument, so nothing in them is ever read by a shell.
    /// </summary>
    public static IReadOnlyList<string> HelperArgs(string version, string package)
    {
        if (!IsPlainVersion(version)) throw new ArgumentException("geen geldig versienummer: " + version, nameof(version));
        if (string.IsNullOrEmpty(package) || !package.StartsWith('/')) throw new ArgumentException("het pakket moet een volledig pad zijn", nameof(package));
        return new[] { "-n", LinuxHelper, version, package };
    }

    /// <summary>
    /// The result line the helper leaves after installing ("ok 2.1.44" or "failed 2.1.44: reason"):
    /// (ok, version, reason), or null when the text is not one.
    /// </summary>
    public static (bool Ok, string Version, string Reason)? ParseResult(string text)
    {
        Match m = Regex.Match((text ?? "").Trim(), @"^(ok|failed) ([0-9.]+)(?:: ?(.*))?$", RegexOptions.Singleline);
        if (!m.Success) return null;
        return (m.Groups[1].Value == "ok", m.Groups[2].Value, m.Groups[3].Success ? m.Groups[3].Value.Trim() : null);
    }
}
