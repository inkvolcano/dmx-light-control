using DmxLicht.Core;
using DmxLicht.Core.Effects;
using DmxLicht.Core.Engine;
using DmxLicht.Core.Show;
using DmxLicht.Core.Storage;

namespace DmxLicht.Tests;

/// <summary>A clock the test moves by hand.</summary>
sealed class ManualClock : ITimelineClock
{
    public double PositionMs { get; set; }
    public bool Playing { get; set; }
    public double LengthMs { get; set; }
    public void Play() => Playing = true;
    public void Pause() => Playing = false;
    public void Seek(double ms) => PositionMs = ms;
    public void Dispose() { }
}

public class TimelineTests
{
    static (ShowFile show, Timeline t, ManualClock clock, TimelinePlayer player, List<int> fades) Setup()
    {
        var show = new ShowFile();
        var red = new Scene { Id = "red", FadeMs = 500 };
        red.State.Looks["a"] = new FixtureLook { On = true, Color = ColorRef.Palette("red") };
        var blue = new Scene { Id = "blue", FadeMs = 0 };
        blue.State.Looks["a"] = new FixtureLook { On = true, Color = ColorRef.Palette("blue") };
        show.Scenes.AddRange(new[] { red, blue });

        var t = Timeline.CreateDefault("Song", "Scenes", "Effects");
        t.LengthMs = 10000;
        t.Tracks[0].Clips.Add(new TimelineClip { Kind = ClipKind.Scene, SceneId = "red", StartMs = 0, LengthMs = 4000 });
        t.Tracks[0].Clips.Add(new TimelineClip { Kind = ClipKind.Scene, SceneId = "blue", StartMs = 4000, LengthMs = 4000 });
        t.Tracks[1].Clips.Add(new TimelineClip { Kind = ClipKind.Effect, StartMs = 2000, LengthMs = 3000, Effect = new EffectSettings { EffectIndex = 26 } });
        show.Timelines.Add(t);

        var clock = new ManualClock();
        var player = new TimelinePlayer(_ => clock);
        var fades = new List<int>();
        return (show, t, clock, player, fades);
    }

    [Fact]
    public void ASongThatRunsOutLoopsAndPlayAfterTheEndStartsOver()
    {
        var (show, t, clock, player, fades) = Setup();
        clock.LengthMs = 9500;                 // the song is shorter than the timeline (length rounded up)
        t.Loop = true;
        player.Play(t);
        player.Tick(show, fades.Add);
        clock.PositionMs = 9400; player.Tick(show, fades.Add);
        clock.Playing = false;                 // the audio player stopped at the end of the file
        player.Tick(show, fades.Add);
        Assert.True(clock.Playing);
        Assert.Equal(0, clock.PositionMs);

        t.Loop = false;
        clock.PositionMs = 9400; player.Tick(show, fades.Add);
        clock.Playing = false; player.Tick(show, fades.Add);
        Assert.False(clock.Playing);
        player.Play(t);                        // Play at the end: from the start, not "resume" at the end
        Assert.Equal(0, clock.PositionMs);
        Assert.True(clock.Playing);
    }

    [Fact]
    public void SwitchingTimelinesRemovesTheOldTimelinesEffects()
    {
        var (show, t, clock, player, fades) = Setup();
        player.Play(t);
        clock.PositionMs = 2500; player.Tick(show, fades.Add);
        Assert.Contains(show.State.Layers, l => l.Source == TimelinePlayer.Source);
        var other = Timeline.CreateDefault("Other", "Scenes", "Effects");
        other.LengthMs = 5000;
        show.Timelines.Add(other);
        player.Stop();
        player.Play(other);                    // what /timeline/play does
        player.Tick(show, fades.Add);
        Assert.DoesNotContain(show.State.Layers, l => l.Source == TimelinePlayer.Source);
    }

    [Fact]
    public void ScenesComeInAtTheirStartWithTheirFade()
    {
        var (show, t, clock, player, fades) = Setup();
        player.Play(t);
        player.Tick(show, fades.Add);
        Assert.Equal("red", show.State.ActiveScene);
        Assert.Equal(new[] { 500 }, fades);

        clock.PositionMs = 3999; player.Tick(show, fades.Add);
        Assert.Equal("red", show.State.ActiveScene);
        clock.PositionMs = 4001; player.Tick(show, fades.Add);
        Assert.Equal("blue", show.State.ActiveScene);
        Assert.Single(fades);                       // blue has no fade
    }

    [Fact]
    public void EffectClipsRunOnlyForTheirLengthAndLeaveManualLayersAlone()
    {
        var (show, t, clock, player, fades) = Setup();
        player.Play(t);
        clock.PositionMs = 1000; player.Tick(show, fades.Add);
        Assert.DoesNotContain(show.State.Layers, l => l.Source == TimelinePlayer.Source);
        show.State.Layers.Add(new EffectLayer { GroupId = "front", Effect = new EffectSettings() }); // user starts one by hand

        clock.PositionMs = 2500; player.Tick(show, fades.Add);
        EffectLayer mine = Assert.Single(show.State.Layers, l => l.Source == TimelinePlayer.Source);
        Assert.Equal("*", mine.GroupId);
        Assert.Equal(26, mine.Effect.EffectIndex);
        Assert.Contains(show.State.Layers, l => l.GroupId == "front");   // the user's effect keeps running

        // a scene sets the whole look (like recalling it by hand), but the timeline's own effect keeps running
        clock.PositionMs = 4100; player.Tick(show, fades.Add);
        Assert.Single(show.State.Layers, l => l.Source == TimelinePlayer.Source);
        Assert.DoesNotContain(show.State.Layers, l => l.GroupId == "front");

        clock.PositionMs = 5000; player.Tick(show, fades.Add);
        Assert.DoesNotContain(show.State.Layers, l => l.Source == TimelinePlayer.Source);
    }

    [Fact]
    public void SeekingIntoAClipAppliesItsSceneWithoutALongFade()
    {
        var (show, t, clock, player, fades) = Setup();
        player.Play(t, 3000);
        player.Tick(show, fades.Add);
        Assert.Equal("red", show.State.ActiveScene);
        Assert.Empty(fades);
    }

    [Fact]
    public void LoopsAndStops()
    {
        var (show, t, clock, player, fades) = Setup();
        t.Loop = true;
        player.Play(t);
        clock.PositionMs = 10001; player.Tick(show, fades.Add);
        Assert.Equal(0, clock.PositionMs);
        Assert.Equal("red", show.State.ActiveScene);

        clock.PositionMs = 2500; player.Tick(show, fades.Add);
        player.Stop(); player.Tick(show, fades.Add);
        Assert.DoesNotContain(show.State.Layers, l => l.Source == TimelinePlayer.Source);
        Assert.Null(player.TimelineId);
    }

    [Fact]
    public void PlaysOnlyBetweenStartAndStopPoints()
    {
        var (show, t, clock, player, fades) = Setup();
        t.StartMs = 4000; t.EndMs = 6000;
        player.Play(t);
        Assert.Equal(4000, clock.PositionMs);                 // starts at the start point
        player.Tick(show, fades.Add);
        Assert.Equal("blue", show.State.ActiveScene);         // the scene that belongs at 4 s

        clock.PositionMs = 6100; player.Tick(show, fades.Add);
        Assert.False(clock.Playing);                           // stops at the stop point
        Assert.Equal(6000, clock.PositionMs);

        player.Play(t);                                        // play again: from the start point
        Assert.Equal(4000, clock.PositionMs);
        t.Loop = true;
        clock.PositionMs = 6050; player.Tick(show, fades.Add);
        Assert.Equal(4000, clock.PositionMs);                  // loops the region only
        Assert.True(clock.Playing);
    }

    [Fact]
    public void WaitPointPausesUntilGo()
    {
        var (show, t, clock, player, fades) = Setup();
        t.Markers.Add(new TimelineMarker { AtMs = 3000, Name = "Na speech", Kind = "wait" });
        t.Markers.Add(new TimelineMarker { AtMs = 5000, Name = "Refrein", Kind = "cue" });
        player.Play(t);
        clock.PositionMs = 2900; player.Tick(show, fades.Add);
        clock.PositionMs = 3040; player.Tick(show, fades.Add);
        Assert.False(clock.Playing);
        Assert.Equal(3000, clock.PositionMs);
        Assert.Equal("Na speech", player.WaitingAt?.Name);

        player.Play(t);                                        // GO
        Assert.Null(player.WaitingAt);
        Assert.True(clock.Playing);
        clock.PositionMs = 3100; player.Tick(show, fades.Add);
        Assert.True(clock.Playing);                            // not caught by the same point again
        clock.PositionMs = 5100; player.Tick(show, fades.Add);
        Assert.True(clock.Playing);                            // a cue point does not stop
    }

    [Fact]
    public void JumpingOverAWaitPointDoesNotStop()
    {
        var (show, t, clock, player, fades) = Setup();
        t.Markers.Add(new TimelineMarker { AtMs = 3000, Kind = "wait" });
        player.Play(t);
        player.Tick(show, fades.Add);
        clock.PositionMs = 7000; player.Tick(show, fades.Add);   // e.g. seek with the mouse
        Assert.True(clock.Playing);
    }

    [Fact]
    public void NextAndPreviousJumpBetweenPoints()
    {
        var (show, t, clock, player, fades) = Setup();
        t.StartMs = 1000; t.EndMs = 9000;
        t.Markers.Add(new TimelineMarker { AtMs = 4000, Kind = "cue" });
        player.Play(t);
        player.Next(t); Assert.Equal(4000, clock.PositionMs);
        player.Next(t); Assert.Equal(9000, clock.PositionMs);
        player.Previous(t); Assert.Equal(4000, clock.PositionMs);
        clock.PositionMs = 4500;
        player.Previous(t); Assert.Equal(1000, clock.PositionMs);   // within 1 s after a point: the one before
    }
}
