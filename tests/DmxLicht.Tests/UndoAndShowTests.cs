using DmxLicht.Core;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

public class UndoAndShowTests
{
    static LightEngine Engine() => new(new ShowFile
    {
        Patch = { new PatchedFixture { Id = "a", TypeId = "generic-rgb", Mode = "3ch", Address = 1 } },
    }, new CaptureOutput(), null);

    [Fact]
    public void UndoAndRedoChanges()
    {
        using LightEngine e = Engine();
        e.Change(s => s.State.BrightnessPercent = 40);
        Thread.Sleep(900);                              // separate steps
        e.Change(s => s.Patch.Add(new PatchedFixture { Id = "b", TypeId = "generic-rgb", Mode = "3ch", Address = 4 }), patchChanged: true);
        Assert.Equal(2, e.Rig.Fixtures.Count);

        Assert.True(e.Undo());
        Assert.Single(e.Rig.Fixtures);                 // rig rebuilt
        Assert.Equal(40, e.Show.State.BrightnessPercent);
        Assert.True(e.Undo());
        Assert.Equal(100, e.Show.State.BrightnessPercent);
        Assert.False(e.Undo());

        Assert.True(e.Redo());
        Assert.Equal(40, e.Show.State.BrightnessPercent);
        Assert.True(e.Redo());
        Assert.Equal(2, e.Rig.Fixtures.Count);
        Assert.False(e.Redo());
    }

    [Fact]
    public void QuickChangesAreOneUndoStepAndSettingsAreNotUndone()
    {
        using LightEngine e = Engine();
        for (int v = 90; v >= 10; v -= 10) e.Change(s => s.State.BrightnessPercent = v);   // like dragging a slider
        e.Change(s => s.Settings.Language = "en", undoable: false);
        Assert.Equal(1, e.UndoCount);
        e.Undo();
        Assert.Equal(100, e.Show.State.BrightnessPercent);
        Assert.Equal("en", e.Show.Settings.Language);
    }

    [Fact]
    public void ReplacingTheShowKeepsSettingsAndCanBeUndone()
    {
        using LightEngine e = Engine();
        e.Show.Settings.Language = "en";
        e.Replace(new ShowContent { Name = "Kerst", Scenes = { new Scene { Name = "Rood" } } });
        Assert.Equal("Kerst", e.Show.Name);
        Assert.Empty(e.Rig.Fixtures);
        Assert.Equal("en", e.Show.Settings.Language);
        e.Undo();
        Assert.Single(e.Rig.Fixtures);
        Assert.Empty(e.Show.Scenes);
    }

    [Fact]
    public void BackupZipRoundTrip()
    {
        var show = new ShowFile { Name = "Test", Scenes = { new Scene { Name = "Blauw" } } };
        string dir = Log.Dir;
        Directory.CreateDirectory(dir);
        string showPath = Path.Combine(dir, "show.json");
        string before = File.Exists(showPath) ? File.ReadAllText(showPath) : null;
        try
        {
            show.Save(showPath);
            using var ms = new MemoryStream();
            ShowLibrary.WriteBackupZip(ms);
            ms.Position = 0;
            ShowContent c = ShowLibrary.RestoreZip(ms);
            Assert.Equal("Test", c.Name);
            Assert.Equal("Blauw", Assert.Single(c.Scenes).Name);
        }
        finally
        {
            if (before != null) File.WriteAllText(showPath, before); else File.Delete(showPath);
        }
    }
}
