using System.Text.Json;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// <see cref="Drafts"/> writing every finished tool call to the lasting <see cref="ToolCallLogWriter"/>
/// log (RT-8): once per call, with elapsed time, and independent of the six-row in-memory bound.
/// </summary>
public sealed class DraftsToolCallLogTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Reads every logged JSON line for the persona "Ada" on the test day.</summary>
    private static List<JsonElement> ReadLog(TempDataDir dir)
    {
        string path = Path.Combine(dir.Path, "logs", "tool-calls", "Ada", "2026-10-08.jsonl");
        if (!File.Exists(path))
        {
            return [];
        }

        return [.. File.ReadAllLines(path).Select(static line => JsonDocument.Parse(line).RootElement.Clone())];
    }

    /// <summary>A call that goes pending then completed is logged once, with the milliseconds between first sighting and completion.</summary>
    [Fact]
    public void Activity_PendingThenCompleted_LogsOnceWithElapsedMs()
    {
        using TempDataDir dir = new();
        ManualTimeProvider clock = new(Start);
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), clock);

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Pending);
        Assert.Empty(ReadLog(dir));
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        List<JsonElement> lines = ReadLog(dir);
        JsonElement entry = Assert.Single(lines);
        Assert.Equal(1500, entry.GetProperty("durationMs").GetInt64());
        Assert.Equal("completed", entry.GetProperty("status").GetString());
        Assert.Equal("Bash", entry.GetProperty("tool").GetString());
        Assert.Equal("room1", entry.GetProperty("room").GetString());
    }

    /// <summary>A call that fails is logged with the failed status.</summary>
    [Fact]
    public void Activity_Failed_LogsFailedStatus()
    {
        using TempDataDir dir = new();
        ManualTimeProvider clock = new(Start);
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), clock);

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Edit", ToolActivityStatus.InProgress);
        clock.Advance(TimeSpan.FromMilliseconds(20));
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Failed);

        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("failed", entry.GetProperty("status").GetString());
        Assert.Equal(20, entry.GetProperty("durationMs").GetInt64());
    }

    /// <summary>A repeated update of an already finished call does not log a second line.</summary>
    [Fact]
    public void Activity_RepeatedFinalUpdate_LogsOnce()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Completed);
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Completed);
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Failed);

        Assert.Single(ReadLog(dir));
    }

    /// <summary>Every call of a Turn is logged even though only the last six stay in the Draft.</summary>
    [Fact]
    public void Activity_MoreThanSixCalls_LogsAllWhileDraftKeepsSix()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        for (int i = 0; i < 9; i++)
        {
            drafts.Activity("m1", $"t{i}", "room1", "agentA", "Ada", $"Tool{i}", ToolActivityStatus.Completed);
        }

        Assert.Equal(9, ReadLog(dir).Count);
        Assert.Equal(Drafts.MaxToolCalls, Assert.Single(drafts.ForRoom("room1")).ToolCalls.Count);
    }

    /// <summary>A log that cannot be written never throws into the Turn path.</summary>
    [Fact]
    public void Activity_LogUnwritable_DoesNotThrow()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "logs"), "blocker");
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Completed);

        Assert.Equal(ToolActivityStatus.Completed, Assert.Single(Assert.Single(drafts.ForRoom("room1")).ToolCalls).Status);
    }
}
