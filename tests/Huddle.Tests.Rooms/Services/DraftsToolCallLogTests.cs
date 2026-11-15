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

        return [.. File.ReadAllLines(path).Select(static line => JsonElement.Parse(line))];
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

    /// <summary>An Adapter notice row is shown in the Draft but is not a tool call, so it is never written to the log; a real call beside it still is.</summary>
    [Fact]
    public void Activity_Notice_ShownButNeverLogged()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "huddle-notice-1", "room1", "agentA", "Ada", "Auto mode unavailable", ToolActivityStatus.Notice);
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Completed);

        Assert.Equal(
            [("huddle-notice-1", ToolActivityStatus.Notice), ("t1", ToolActivityStatus.Completed)],
            Assert.Single(drafts.ForRoom("room1")).ToolCalls.Select(call => (call.ToolCallId, call.Status)));
        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("Bash", entry.GetProperty("tool").GetString());
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

    /// <summary>Arguments given on an in-progress update and not repeated on the completed update are logged on the completed line.</summary>
    [Fact]
    public void Activity_ArgumentsOnEarlierUpdate_LoggedOnCompletion()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.InProgress, arguments: """{"command":"dotnet test"}""");
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("""{"command":"dotnet test"}""", entry.GetProperty("arguments").GetString());
    }

    /// <summary>A later non-null arguments value replaces an earlier one, and a null leaves it alone.</summary>
    [Fact]
    public void Activity_ArgumentsReplacedByNonNullKeptByNull()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.InProgress, arguments: """{"command":"d"}""");
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.InProgress, arguments: """{"command":"dotnet"}""");
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.InProgress, arguments: null);
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("""{"command":"dotnet"}""", entry.GetProperty("arguments").GetString());
    }

    /// <summary>With no arguments, the path and path:line fallback still applies.</summary>
    [Fact]
    public void Activity_NoArguments_FallsBackToPathAndLine()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "Read", ToolActivityStatus.Completed, path: "src/a.cs");
        drafts.Activity("m1", "t2", "room1", "agentA", "Ada", "Read", ToolActivityStatus.Completed, path: "src/b.cs", line: 7);

        List<JsonElement> lines = ReadLog(dir);
        Assert.Equal(["src/a.cs", "src/b.cs:7"], lines.Select(static line => line.GetProperty("arguments").GetString()));
    }

    /// <summary>A call with both a file and an input logs the file first and the input after it, so the file survives the cut.</summary>
    [Fact]
    public void Activity_PathAndArguments_LogsPathThenInput()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "mcp__team__memory", ToolActivityStatus.InProgress, arguments: """{"command":"read","file":"notes.md"}""");
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed, path: "C:/data/notes.md", line: 3);

        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("""C:/data/notes.md:3 {"command":"read","file":"notes.md"}""", entry.GetProperty("arguments").GetString());
    }

    /// <summary>A result given on the completed update is logged, and a call without one logs result as JSON null.</summary>
    [Fact]
    public void Activity_Result_LoggedOrNull()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", "mcp__team__add_evidence", ToolActivityStatus.InProgress);
        drafts.Activity("m1", "t1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed, result: "Nothing was added.");
        drafts.Activity("m1", "t2", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.Completed);

        List<JsonElement> lines = ReadLog(dir);
        Assert.Equal("Nothing was added.", lines[0].GetProperty("result").GetString());
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("result").ValueKind);
    }

    /// <summary>Arguments survive the six-row trim: a call whose row was trimmed still logs them when it completes.</summary>
    [Fact]
    public void Activity_ArgumentsOfTrimmedCall_StillLogged()
    {
        using TempDataDir dir = new();
        Drafts drafts = new(new ToolCallLogWriter(dir.Options()), new ManualTimeProvider(Start));

        drafts.Activity("m1", "t0", "room1", "agentA", "Ada", "Bash", ToolActivityStatus.InProgress, arguments: """{"command":"first"}""");
        for (int i = 1; i <= Drafts.MaxToolCalls; i++)
        {
            drafts.Activity("m1", $"t{i}", "room1", "agentA", "Ada", $"Tool{i}", ToolActivityStatus.InProgress);
        }

        drafts.Activity("m1", "t0", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        JsonElement entry = Assert.Single(ReadLog(dir));
        Assert.Equal("""{"command":"first"}""", entry.GetProperty("arguments").GetString());
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
