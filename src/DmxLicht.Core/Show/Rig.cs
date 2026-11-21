using DmxLicht.Core.Fixtures;

namespace DmxLicht.Core.Show;

/// <summary>
/// Universes: each is 512 channels. Frames hold all universes one after another: channel 1 of universe 2 is
/// frame index 513. A show with only universe 1 keeps a 513-byte frame (index 0 unused), as before.
/// </summary>
public static class Dmx
{
    public const int MaxUniverses = 8;
    public static int Clamp(int universe) => Math.Clamp(universe, 1, MaxUniverses);
    /// <summary>Frame index just before channel 1 of <paramref name="universe"/>.</summary>
    public static int Offset(int universe) => (Clamp(universe) - 1) * 512;
    public static int FrameSize(int universes) => 1 + 512 * Math.Clamp(universes, 1, MaxUniverses);
}

/// <summary>One lamp in the patch: which type and mode, and at which DMX address.</summary>
public sealed class PatchedFixture
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; }
    public string TypeId { get; set; }
    public string Mode { get; set; }
    /// <summary>DMX start address 1..512 within its universe.</summary>
    public int Address { get; set; } = 1;
    /// <summary>Universe 1..8 (each is its own DMX line / Art-Net or sACN universe).</summary>
    public int Universe { get; set; } = 1;
    /// <summary>Position on the stage view (0..1), set by dragging.</summary>
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

/// <summary>A patched fixture with its type and mode looked up.</summary>
public sealed class RigFixture
{
    public PatchedFixture Patch { get; init; }
    public FixtureType Type { get; init; }
    public FixtureMode Mode { get; init; }
    /// <summary>Index of this fixture's first head in <see cref="Rig.Heads"/>.</summary>
    public int FirstHead { get; init; }
    public int CellCount => Mode?.CellCount ?? 0;
    public int LastAddress => Patch.Address + (Mode?.Footprint ?? 1) - 1;
    public int Universe => Dmx.Clamp(Patch.Universe);
    /// <summary>Frame index before this fixture's first channel (universe offset + address - 1).</summary>
    public int BaseChannel => Dmx.Offset(Universe) + Patch.Address - 1;
}

/// <summary>One controllable colour point: a whole lamp, or one segment of a multi-segment lamp.</summary>
public readonly record struct Head(int Fixture, int Cell);

/// <summary>Two fixtures sharing DMX addresses.</summary>
public readonly record struct AddressConflict(string FixtureA, string FixtureB, int From, int To);

/// <summary>The patch resolved against the fixture library: what the frame builder works from.</summary>
public sealed class Rig
{
    public IReadOnlyList<RigFixture> Fixtures { get; }
    /// <summary>Effects run over the heads in this order (fixture order, then segment).</summary>
    public IReadOnlyList<Head> Heads { get; }
    /// <summary>
    /// Show colours the lamp has no LED for using the LEDs it does have (white/amber/UV mixed from RGB).
    /// Off only for the v1.4 compatibility path, which dropped them.
    /// </summary>
    public bool FoldMissingEmitters { get; init; } = true;
    /// <summary>Patched fixtures whose type is not in the library.</summary>
    public IReadOnlyList<PatchedFixture> Missing { get; }

    /// <summary>Group id to the indexes (in <see cref="Fixtures"/>) of its fixtures, in rig order.</summary>
    public IReadOnlyDictionary<string, int[]> Groups { get; private set; } = new Dictionary<string, int[]>();

    public Rig(IEnumerable<(PatchedFixture patch, FixtureType type, FixtureMode mode)> fixtures, IEnumerable<PatchedFixture> missing = null)
    {
        var list = new List<RigFixture>();
        var heads = new List<Head>();
        foreach (var (patch, type, mode) in fixtures)
        {
            var rf = new RigFixture { Patch = patch, Type = type, Mode = mode, FirstHead = heads.Count };
            for (int c = 0; c < rf.CellCount; c++) heads.Add(new Head(list.Count, c));
            list.Add(rf);
        }
        Fixtures = list;
        Heads = heads;
        Missing = (missing ?? Enumerable.Empty<PatchedFixture>()).ToList();
    }

    public static Rig Build(IEnumerable<PatchedFixture> patch, FixtureLibrary library, IEnumerable<FixtureGroup> groups = null)
    {
        var ok = new List<(PatchedFixture, FixtureType, FixtureMode)>();
        var missing = new List<PatchedFixture>();
        foreach (PatchedFixture p in patch)
        {
            FixtureType t = library.Find(p.TypeId);
            FixtureMode m = t?.Mode(p.Mode);
            if (m == null) missing.Add(p);
            else ok.Add((p, t, m));
        }
        var rig = new Rig(ok, missing);
        if (groups != null)
        {
            var index = rig.Fixtures.Select((f, i) => (f.Patch.Id, i)).ToDictionary(x => x.Id, x => x.i);
            rig.Groups = groups.ToDictionary(g => g.Id,
                g => g.FixtureIds.Where(index.ContainsKey).Select(id => index[id]).OrderBy(i => i).ToArray());
        }
        return rig;
    }

    /// <summary>Number of universes the frame needs (the highest one in use).</summary>
    public int Universes => Fixtures.Select(f => f.Universe).DefaultIfEmpty(1).Max();
    public int FrameSize => Dmx.FrameSize(Universes);

    /// <summary>Highest frame index in use (universe 1 only: the highest channel, as before).</summary>
    public int UsedChannelCount => Math.Clamp(Fixtures.Select(f => f.BaseChannel + (f.Mode?.Footprint ?? 1)).DefaultIfEmpty(1).Max(), 1, FrameSize - 1);

    /// <summary>Highest channel in use within one universe (0 = none).</summary>
    public int UsedIn(int universe) =>
        Fixtures.Where(f => f.Universe == universe).Select(f => Math.Min(512, f.LastAddress)).DefaultIfEmpty(0).Max();

    public IEnumerable<AddressConflict> Conflicts()
    {
        for (int i = 0; i < Fixtures.Count; i++)
            for (int j = i + 1; j < Fixtures.Count; j++)
            {
                RigFixture a = Fixtures[i], b = Fixtures[j];
                if (a.Universe != b.Universe) continue;
                int from = Math.Max(a.Patch.Address, b.Patch.Address), to = Math.Min(a.LastAddress, b.LastAddress);
                if (from <= to) yield return new AddressConflict(a.Patch.Id, b.Patch.Id, from, to);
            }
    }

    /// <summary>First universe and address where the fixture fits, starting at <paramref name="fromUniverse"/>; (-1, -1) when full.</summary>
    public static (int Universe, int Address) NextFree(IEnumerable<(int universe, int address, int footprint)> used, int footprint, int fromUniverse = 1)
    {
        var all = used.ToList();
        for (int u = Dmx.Clamp(fromUniverse); u <= Dmx.MaxUniverses; u++)
        {
            int a = NextFreeAddress(all.Where(x => Dmx.Clamp(x.universe) == u).Select(x => (x.address, x.footprint)), footprint);
            if (a > 0) return (u, a);
        }
        return (-1, -1);
    }

    /// <summary>First address where a fixture with <paramref name="footprint"/> channels fits without overlap.</summary>
    public static int NextFreeAddress(IEnumerable<(int address, int footprint)> used, int footprint)
    {
        var taken = used.OrderBy(u => u.address).ToList();
        int candidate = 1;
        foreach (var (address, fp) in taken)
        {
            if (candidate + footprint - 1 < address) break;
            candidate = Math.Max(candidate, address + fp);
        }
        return candidate + footprint - 1 <= 512 ? candidate : -1;
    }
}
