using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for <see cref="ContextFill"/>, <see cref="ContextFillReading"/> and <see cref="ContextFillBand"/>.</summary>
public sealed class ContextFillTests
{
    /// <summary>A reported reading comes back with its Used, Size and the floored whole percent.</summary>
    [Fact]
    public void Report_ThenGet_ReturnsUsedSizeAndFlooredPercent()
    {
        ContextFill fill = new();

        fill.Report("agent-1", "room-a", 124_999, 200_000);

        ContextFillReading? reading = fill.Get("agent-1", "room-a");
        Assert.NotNull(reading);
        Assert.Equal(124_999, reading.Used);
        Assert.Equal(200_000, reading.Size);
        Assert.Equal(62, reading.Percent);
        Assert.False(reading.Shared);
    }

    /// <summary>An Adapter that does not report a window size gives no percentage.</summary>
    [Fact]
    public void Report_SizeZero_PercentIsNull()
    {
        ContextFill fill = new();

        fill.Report("agent-1", "room-a", 5_000, 0);

        ContextFillReading? reading = fill.Get("agent-1", "room-a");
        Assert.NotNull(reading);
        Assert.Null(reading.Percent);
    }

    /// <summary>A reading above the window is capped at 100 rather than overflowing the bar.</summary>
    [Fact]
    public void Percent_UsedAboveSize_IsCappedAt100()
    {
        ContextFillReading reading = new(250_000, 200_000, Shared: false);

        Assert.Equal(100, reading.Percent);
    }

    /// <summary>A Room's own reading wins over the Agent's shared one.</summary>
    [Fact]
    public void Get_RoomEntryAndSharedEntry_RoomWins()
    {
        ContextFill fill = new();
        fill.Report("agent-1", null, 10, 100);
        fill.Report("agent-1", "room-a", 40, 100);

        ContextFillReading? reading = fill.Get("agent-1", "room-a");

        Assert.NotNull(reading);
        Assert.Equal(40, reading.Used);
        Assert.False(reading.Shared);
    }

    /// <summary>With only a shared entry, any Room gets it, marked shared.</summary>
    [Fact]
    public void Get_OnlySharedEntry_ReturnsItMarkedShared()
    {
        ContextFill fill = new();
        fill.Report("agent-1", null, 10, 100);

        ContextFillReading? reading = fill.Get("agent-1", "any-room");

        Assert.NotNull(reading);
        Assert.Equal(10, reading.Used);
        Assert.True(reading.Shared);
    }

    /// <summary>An Agent or Room nobody has reported for has no reading.</summary>
    [Fact]
    public void Get_NothingReported_ReturnsNull()
    {
        ContextFill fill = new();
        fill.Report("agent-1", "room-a", 10, 100);

        Assert.Null(fill.Get("agent-2", "room-a"));
        Assert.Null(fill.Get("agent-1", "room-b"));
    }

    /// <summary>Forget removes exactly the one (Agent, Room) entry.</summary>
    [Fact]
    public void Forget_RemovesOnlyThatEntry()
    {
        ContextFill fill = new();
        fill.Report("agent-1", "room-a", 10, 100);
        fill.Report("agent-1", "room-b", 20, 100);

        fill.Forget("agent-1", "room-a");

        Assert.Null(fill.Get("agent-1", "room-a"));
        Assert.NotNull(fill.Get("agent-1", "room-b"));
    }

    /// <summary>ClearAgent removes every entry of that Agent and leaves other Agents alone.</summary>
    [Fact]
    public void ClearAgent_RemovesAllOfThatAgentOnly()
    {
        ContextFill fill = new();
        fill.Report("agent-1", "room-a", 10, 100);
        fill.Report("agent-1", null, 20, 100);
        fill.Report("agent-2", "room-a", 30, 100);

        fill.ClearAgent("agent-1");

        Assert.Null(fill.Get("agent-1", "room-a"));
        Assert.Null(fill.Get("agent-1", "room-b"));
        Assert.NotNull(fill.Get("agent-2", "room-a"));
    }

    /// <summary>Changed fires once for a new value and not for an identical repeat; a different value fires again.</summary>
    [Fact]
    public void Changed_FiresOnNewValueOnly_NotOnIdenticalRepeat()
    {
        ContextFill fill = new();
        int count = 0;
        fill.Changed += () => count++;

        fill.Report("agent-1", "room-a", 10, 100);
        fill.Report("agent-1", "room-a", 10, 100);
        Assert.Equal(1, count);

        fill.Report("agent-1", "room-a", 11, 100);
        Assert.Equal(2, count);
    }

    /// <summary>A Changed handler can read the store back from another thread, because the event is raised outside the lock.</summary>
    [Fact]
    public async Task Changed_HandlerCanCallGetFromAnotherThread_WithoutDeadlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ContextFill fill = new();
        ContextFillReading? seen = null;
        fill.Changed += () => seen = Task.Run(() => fill.Get("agent-1", "room-a"), ct).GetAwaiter().GetResult();

        Task report = Task.Run(() => fill.Report("agent-1", "room-a", 10, 100), ct);
        Task finished = await Task.WhenAny(report, Task.Delay(TimeSpan.FromSeconds(10), ct));

        Assert.Same(report, finished);
        Assert.Equal(10, seen?.Used);
    }

    /// <summary>Forgetting an entry that is not there raises nothing.</summary>
    [Fact]
    public void Forget_Unknown_RaisesNothing()
    {
        ContextFill fill = new();
        bool raised = false;
        fill.Changed += () => raised = true;

        fill.Forget("agent-1", "room-a");
        fill.ClearAgent("agent-1");

        Assert.False(raised);
    }

    /// <summary>A negative reading is rejected.</summary>
    [Fact]
    public void Report_Negative_Throws()
    {
        ContextFill fill = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => fill.Report("agent-1", "room-a", -1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => fill.Report("agent-1", "room-a", 1, -100));
    }

    /// <summary>The bands start at 75 (Warning) and 90 (Error).</summary>
    /// <param name="percent">The fill as a whole percent.</param>
    /// <param name="expected">The band it falls in.</param>
    [Theory]
    [InlineData(0, "Normal")]
    [InlineData(74, "Normal")]
    [InlineData(75, "Warning")]
    [InlineData(89, "Warning")]
    [InlineData(90, "Error")]
    [InlineData(100, "Error")]
    public void Band_Of_PercentBoundaries(int percent, string expected)
    {
        Assert.Equal(expected, ContextFillBand.Of(percent).ToString());
    }
}
