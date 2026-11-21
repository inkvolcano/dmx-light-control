using System.Text.Json;
using DmxLicht.Core.Show;

namespace DmxLicht.Core.Storage;

/// <summary>App preferences (not part of the light show itself).</summary>
public sealed class AppSettings
{
    /// <summary>UI language code: "nl", "en", ...</summary>
    public string Language { get; set; } = "nl";
    /// <summary>uDMX send rate: 0 = calm (8/s), 1 = normal (16/s), 2 = fast (25/s).</summary>
    public int DongleSpeed { get; set; } = 1;
    /// <summary>Let tablets/phones on the network open the control page.</summary>
    public bool AllowNetwork { get; set; }
    public int Port { get; set; } = 5150;
    /// <summary>Keep Windows awake while the app runs (sleep cuts the USB dongle).</summary>
    public bool PreventSleep { get; set; } = true;
    public DmxLicht.Core.Output.OutputSettings Outputs { get; set; } = new();
    public DmxLicht.Core.Control.ControlSettings Control { get; set; } = new();
    /// <summary>PIN that unlocks the full app while <see cref="Locked"/>; never sent to the UI.</summary>
    public string OperatorPin { get; set; }
    /// <summary>Simple operator view; lamps, settings and editing need the PIN.</summary>
    public bool Locked { get; set; }
    /// <summary>What the easy (phone) mode shows.</summary>
    public EasySettings Easy { get; set; } = new();
    /// <summary>Menu tabs that are switched off in Settings ("effects", "screen", ..., "easy" = the Easy button).
    /// Settings itself is always shown.</summary>
    public List<string> HiddenPages { get; set; } = new();
    /// <summary>Home Assistant over MQTT.</summary>
    public MqttSettings Mqtt { get; set; } = new();
    /// <summary>Look for a new version on GitHub at start and once a day (installing always needs a click).</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>Phones/tablets must be paired once (code or QR) before they can control the light.</summary>
    public bool RequirePairing { get; set; } = true;
    /// <summary>Fixed name on the network: http://&lt;name&gt;.local (multicast DNS).</summary>
    public string HostName { get; set; } = "dmxlicht";
    /// <summary>Paired phones and tablets (managed by the pairing endpoints, never by a settings save).</summary>
    public List<PairedDevice> Devices { get; set; } = new();

    public int DongleIntervalMs => DongleSpeed switch { 0 => 125, 2 => 40, _ => 60 };
}

/// <summary>A phone or tablet that may control the light.</summary>
public sealed class PairedDevice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    /// <summary>SHA-256 of the device's secret (the secret itself only lives in the device's cookie).</summary>
    public string TokenHash { get; set; }
    public DateTime Paired { get; set; } = DateTime.Now;
    public DateTime LastSeen { get; set; } = DateTime.Now;
}

/// <summary>Connection to an MQTT broker (Home Assistant's Mosquitto add-on, for example).</summary>
public sealed class MqttSettings
{
    public bool Enabled { get; set; }
    /// <summary>Broker address, e.g. the Home Assistant IP or "homeassistant.local".</summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public string User { get; set; } = "";
    /// <summary>Never sent to the UI; null from the UI = keep the saved one.</summary>
    public string Password { get; set; }
    /// <summary>Topic prefix for DmxLicht's own topics.</summary>
    public string Prefix { get; set; } = "dmxlicht";
    /// <summary>Home Assistant's discovery prefix.</summary>
    public string Discovery { get; set; } = "homeassistant";
    /// <summary>Device name in Home Assistant.</summary>
    public string Name { get; set; } = "Glowsmith";
}

/// <summary>Sections of the easy (phone/operator) mode; the basics (on/off, brightness, scenes) are always there.</summary>
public sealed class EasySettings
{
    public bool Colors { get; set; } = true;
    public bool Effects { get; set; } = true;
    public bool Groups { get; set; } = true;
    public bool Timelines { get; set; } = true;
    public bool Music { get; set; } = true;
    public bool Strobe { get; set; } = true;
}

/// <summary>Everything saved to Instellingen\show.json.</summary>
public sealed class ShowFile
{
    public int Version { get; set; } = 1;
    /// <summary>Name of the show (as saved under Shows), or null for an unsaved show.</summary>
    public string Name { get; set; }
    public List<PatchedFixture> Patch { get; set; } = new();
    public ShowState State { get; set; } = new();
    public List<FixtureGroup> Groups { get; set; } = new();
    public List<Scene> Scenes { get; set; } = new();
    public List<Timeline> Timelines { get; set; } = new();
    /// <summary>Planned actions (times, days, sunrise/sunset).</summary>
    public ScheduleSettings Schedule { get; set; } = new();
    public AppSettings Settings { get; set; } = new();

    public static string DefaultPath => Path.Combine(Log.Dir, "show.json");

    public static ShowFile Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                ShowFile f = JsonSerializer.Deserialize<ShowFile>(File.ReadAllText(path), Json.Options) ?? new ShowFile();
                // a flash or timeline effect that ran when the app closed is not part of the show
                f.State?.Layers?.RemoveAll(l => l.Source != null);
                return f;
            }
        }
        catch (Exception ex)
        {
            Log.Write("show.json niet leesbaar, kopie bewaard: " + ex.Message);
            try { File.Copy(path, path + ".kapot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
        }
        return null;
    }

    /// <summary>Writes via a temp file so a crash never leaves a half-written show.</summary>
    public void Save(string path) => WriteJson(path, ToJson());

    /// <summary>The show as text (do this under the engine lock; writing the file can happen outside it).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json.Options);

    /// <summary>Writes via a temp file so a crash never leaves half a show.json.</summary>
    public static void WriteJson(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
