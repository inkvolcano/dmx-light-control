using System.Diagnostics;
using System.Text.Json;
using DmxLicht.Core;

namespace DmxLicht.App;

/// <summary>
/// Installs the bundled Companion module into Bitfocus Companion's user modules folder
/// (%APPDATA%\companion\modules\dmxlicht-&lt;version&gt;), replacing older DmxLicht versions.
/// </summary>
static class CompanionInstaller
{
    const string ModuleId = "dmxlicht";

    public sealed record Status(
        bool SourceFound, string SourceVersion, bool CompanionFound, string ModulesDir,
        string InstalledVersion, bool UpToDate, bool CompanionRunning);

    /// <summary>Companion's modules folder; override with DMXLICHT_COMPANION_MODULES (used by tests).</summary>
    public static string ModulesDir =>
        Environment.GetEnvironmentVariable("DMXLICHT_COMPANION_MODULES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "companion", "modules");

    /// <summary>Next to the exe in a release ("Companion-module"); in a dev build the repo's companion-module.</summary>
    public static string SourceDir()
    {
        string release = Path.Combine(AppContext.BaseDirectory, "Companion-module");
        if (IsModule(release)) return release;
        for (DirectoryInfo d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string dev = Path.Combine(d.FullName, "companion-module");
            if (IsModule(dev)) return dev;
        }
        return null;
    }

    static bool IsModule(string dir) =>
        File.Exists(Path.Combine(dir, "main.js")) && File.Exists(Path.Combine(dir, "companion", "manifest.json"))
        && Directory.Exists(Path.Combine(dir, "node_modules", "@companion-module", "base"));

    static string VersionOf(string dir)
    {
        try
        {
            using JsonDocument d = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "companion", "manifest.json")));
            return d.RootElement.GetProperty("version").GetString();
        }
        catch { return null; }
    }

    /// <summary>Installed DmxLicht module folders: exactly "dmxlicht-&lt;version&gt;" (never our temp/backup folders).</summary>
    static IEnumerable<string> Installed() =>
        Directory.Exists(ModulesDir)
            ? Directory.GetDirectories(ModulesDir, ModuleId + "-*")
                .Where(d => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), "^" + ModuleId + @"-\d+(\.\d+)*$"))
                .Where(d => File.Exists(Path.Combine(d, "companion", "manifest.json")))
            : Enumerable.Empty<string>();

    public static Status Check()
    {
        string src = SourceDir();
        string srcVersion = src == null ? null : VersionOf(src);
        string installed = Installed().Select(VersionOf).Where(v => v != null).OrderByDescending(v => v).FirstOrDefault();
        // Companion keeps its config in %APPDATA%\companion; the modules folder may not exist yet
        bool companion = Directory.Exists(Path.GetDirectoryName(ModulesDir)!);
        bool running = Process.GetProcesses().Any(p =>
        {
            try { return p.ProcessName.StartsWith("Companion", StringComparison.OrdinalIgnoreCase); } catch { return false; }
        });
        return new Status(src != null, srcVersion, companion, ModulesDir, installed, installed != null && installed == srcVersion, running);
    }

    /// <summary>Copies the module in; returns the folder it went to.</summary>
    public static string Install()
    {
        string src = SourceDir() ?? throw new InvalidOperationException("Companion module not found next to DmxLicht.exe");
        string version = VersionOf(src) ?? "0.0.0";
        Directory.CreateDirectory(ModulesDir);
        string target = Path.Combine(ModulesDir, ModuleId + "-" + version);

        // 1. copy into a temp folder (a failed copy never touches what is installed)
        string temp = Path.Combine(ModulesDir, "." + ModuleId + "-installing");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        CopyDir(src, temp);

        // 2. move old versions aside, move the new one in; on any failure put everything back
        var moved = new List<(string from, string aside)>();
        try
        {
            foreach (string old in Installed().Append(target).Distinct().Where(Directory.Exists).ToList())
            {
                string aside = Path.Combine(ModulesDir, "." + ModuleId + "-old-" + Path.GetFileName(old));
                if (Directory.Exists(aside)) Directory.Delete(aside, true);
                Directory.Move(old, aside);
                moved.Add((old, aside));
            }
            Directory.Move(temp, target);
        }
        catch
        {
            foreach (var (from, aside) in moved)
                if (!Directory.Exists(from) && Directory.Exists(aside)) Directory.Move(aside, from);
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            throw;
        }

        // 3. only now remove the old versions
        foreach (var (_, aside) in moved)
            try { Directory.Delete(aside, true); } catch (Exception ex) { Log.Write("Oude Companion-module niet verwijderd: " + ex.Message); }
        Log.Write("Companion-module geïnstalleerd in " + target);
        return target;
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
        {
            string name = Path.GetFileName(f);
            if (name is ".gitignore" or "package-lock.json") continue;
            File.Copy(f, Path.Combine(to, name), true);
        }
        foreach (string d in Directory.GetDirectories(from))
            CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
