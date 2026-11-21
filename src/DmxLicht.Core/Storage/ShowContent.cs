using System.IO.Compression;
using System.Text.Json;
using DmxLicht.Core.Show;

namespace DmxLicht.Core.Storage;

/// <summary>The show itself (everything except app settings): what undo, show files and backups work with.</summary>
public sealed class ShowContent
{
    public string Name { get; set; }
    public List<PatchedFixture> Patch { get; set; } = new();
    public ShowState State { get; set; } = new();
    public List<FixtureGroup> Groups { get; set; } = new();
    public List<Scene> Scenes { get; set; } = new();
    public List<Timeline> Timelines { get; set; } = new();
    public ScheduleSettings Schedule { get; set; } = new();

    public static ShowContent From(ShowFile s)
    {
        ShowContent c = SceneOps.Clone(new ShowContent
        {
            Name = s.Name, Patch = s.Patch, State = s.State, Groups = s.Groups, Scenes = s.Scenes, Timelines = s.Timelines, Schedule = s.Schedule,
        });
        // temporary layers (flash, timeline) are not part of the show: undo/backups must not bring them back
        c.State.Layers?.RemoveAll(l => l.Source != null);
        return c;
    }

    /// <summary>Puts a copy of this content into <paramref name="s"/> (settings stay).</summary>
    public void ApplyTo(ShowFile s)
    {
        ShowContent c = SceneOps.Clone(this);
        s.Name = c.Name;
        s.Patch = c.Patch ?? new();
        s.State = c.State ?? new();
        s.Groups = c.Groups ?? new();
        s.Scenes = c.Scenes ?? new();
        s.Timelines = c.Timelines ?? new();
        s.Schedule = c.Schedule ?? new();
    }
}

/// <summary>Named show files in Instellingen\Shows, automatic backups, and backup zips.</summary>
public static class ShowLibrary
{
    public static string ShowsDir => Path.Combine(Log.Dir, "Shows");
    public static string BackupsDir => Path.Combine(Log.Dir, "Backups");

    public static string SafeName(string name)
    {
        string s = string.Concat((name ?? "").Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return s.Length == 0 ? "Show" : s.Length > 60 ? s[..60] : s;
    }

    public static List<(string name, DateTime modified)> List() =>
        Directory.Exists(ShowsDir)
            ? Directory.GetFiles(ShowsDir, "*.json").Select(f => (Path.GetFileNameWithoutExtension(f), File.GetLastWriteTime(f)))
                .OrderByDescending(x => x.Item2).ToList()
            : new();

    public static void Save(ShowContent c, string name)
    {
        Directory.CreateDirectory(ShowsDir);
        c.Name = SafeName(name);
        File.WriteAllText(Path.Combine(ShowsDir, c.Name + ".json"), JsonSerializer.Serialize(c, Json.Options));
    }

    public static ShowContent Load(string name) =>
        JsonSerializer.Deserialize<ShowContent>(File.ReadAllText(Path.Combine(ShowsDir, SafeName(name) + ".json")), Json.Options);

    public static void Delete(string name)
    {
        string f = Path.Combine(ShowsDir, SafeName(name) + ".json");
        if (File.Exists(f)) File.Delete(f);
    }

    /// <summary>Copies show.json to Backups (keeps the newest <paramref name="keep"/>).</summary>
    public static void AutoBackup(string showJson, int keep = 30)
    {
        try
        {
            if (!File.Exists(showJson)) return;
            Directory.CreateDirectory(BackupsDir);
            File.Copy(showJson, Path.Combine(BackupsDir, "show-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), true);
            foreach (string old in Directory.GetFiles(BackupsDir, "show-*.json").OrderByDescending(f => f).Skip(keep)) File.Delete(old);
        }
        catch (Exception ex) { Log.Write("Back-up mislukt: " + ex.Message); }
    }

    static readonly string[] Skip = { "WebView2", "Backups" };

    /// <summary>Zip of the Instellingen folder (show, own fixture types, shows, songs, log), without browser cache and old backups.</summary>
    public static void WriteBackupZip(Stream output, string cleanShowJson = null)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        if (cleanShowJson != null)
            using (var w = new StreamWriter(zip.CreateEntry("show.json", CompressionLevel.Fastest).Open())) w.Write(cleanShowJson);
        foreach (string file in Directory.GetFiles(Log.Dir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(Log.Dir, file);
            if (Skip.Any(s => rel.StartsWith(s + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) || rel.EndsWith(".tmp")) continue;
            if (cleanShowJson != null && rel.Equals("show.json", StringComparison.OrdinalIgnoreCase)) continue;   // written above, without secrets
            try
            {
                using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var dst = zip.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Fastest).Open();
                src.CopyTo(dst);
            }
            catch (IOException) { /* file in use: skip */ }
        }
    }

    /// <summary>
    /// Restores a backup zip: own fixture types, show files and songs are copied in; the show content is returned
    /// (the caller applies it). App settings in the zip are ignored.
    /// </summary>
    public static ShowContent RestoreZip(Stream input)
    {
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        ShowContent content = null;
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (rel.Contains("..") || Path.IsPathRooted(rel) || e.Length == 0) continue;
            if (rel.Equals("show.json", StringComparison.OrdinalIgnoreCase))
            {
                using var r = new StreamReader(e.Open());
                ShowFile f = JsonSerializer.Deserialize<ShowFile>(r.ReadToEnd(), Json.Options);
                content = f == null ? null : ShowContent.From(f);
                continue;
            }
            string top = rel.Split(Path.DirectorySeparatorChar)[0];
            if (top is not ("Fixtures" or "Shows" or "Audio")) continue;
            string target = Path.Combine(Log.Dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, overwrite: true);
        }
        return content ?? throw new InvalidDataException("No show.json in this backup");
    }
}
