using System.Text.Json;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// <see cref="ToolCallLogWriter"/>: the lasting, per-Persona, per-UTC-day JSON-lines log of tool calls
/// (Spec Tool-call log, RT-8). The tests pin the returned file and line, the day roll-over, path
/// sanitising, concurrent appends and argument truncation.
/// </summary>
public sealed class ToolCallLogWriterTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an entry at <paramref name="time"/> with the given arguments text.</summary>
    private static ToolCallLogEntry Entry(DateTimeOffset time, string arguments = "{}")
    {
        return new ToolCallLogEntry(time, "room-1", "task-1", "Bash", arguments, "completed", 42);
    }

    /// <summary>Two appends on the same UTC day land in the same file and return lines 1 and 2 with a forward-slash relative path.</summary>
    [Fact]
    public void Append_TwoCallsSameDay_ReturnsLines1And2()
    {
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef first = writer.Append("Maya", Entry(Noon));
        ToolCallLogRef second = writer.Append("Maya", Entry(Noon.AddMinutes(1)));

        Assert.Equal("logs/tool-calls/Maya/2026-10-08.jsonl", first.RelativePath);
        Assert.Equal(first.RelativePath, second.RelativePath);
        Assert.Equal(1, first.Line);
        Assert.Equal(2, second.Line);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(dir.Path, "logs", "tool-calls", "Maya", "2026-10-08.jsonl")).Length);
    }

    /// <summary>The line carries camelCase keys and an ISO-8601 UTC time.</summary>
    [Fact]
    public void Append_WritesCamelCaseJsonWithUtcTime()
    {
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef reference = writer.Append("Maya", Entry(new DateTimeOffset(2026, 10, 8, 14, 0, 0, TimeSpan.FromHours(2))));

        string line = File.ReadAllLines(Path.Combine(dir.Path, "logs", "tool-calls", "Maya", "2026-10-08.jsonl"))[reference.Line - 1];
        using JsonDocument json = JsonDocument.Parse(line);
        JsonElement root = json.RootElement;
        Assert.Equal("room-1", root.GetProperty("room").GetString());
        Assert.Equal("task-1", root.GetProperty("task").GetString());
        Assert.Equal("Bash", root.GetProperty("tool").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal(42, root.GetProperty("durationMs").GetInt64());
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero), root.GetProperty("time").GetDateTimeOffset());
        Assert.Equal("2026-10-08T12:00:00Z", root.GetProperty("time").GetString());
    }

    /// <summary>A call on the next UTC day goes to a new file and starts again at line 1.</summary>
    [Fact]
    public void Append_NewUtcDay_StartsNewFileAtLine1()
    {
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef late = writer.Append("Maya", Entry(new DateTimeOffset(2026, 10, 8, 23, 59, 0, TimeSpan.Zero)));
        ToolCallLogRef next = writer.Append("Maya", Entry(new DateTimeOffset(2026, 10, 9, 0, 1, 0, TimeSpan.Zero)));

        Assert.Equal(1, late.Line);
        Assert.Equal("logs/tool-calls/Maya/2026-10-09.jsonl", next.RelativePath);
        Assert.Equal(1, next.Line);
    }

    /// <summary>A Persona name with a path separator is sanitised so the file stays inside the log folder.</summary>
    [Fact]
    public void Append_NameWithSlash_ReplacesInvalidCharactersWithUnderscore()
    {
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef reference = writer.Append("a/b", Entry(Noon));

        Assert.Equal("logs/tool-calls/a_b/2026-10-08.jsonl", reference.RelativePath);
        Assert.True(File.Exists(Path.Combine(dir.Path, "logs", "tool-calls", "a_b", "2026-10-08.jsonl")));
    }

    /// <summary>Fifty concurrent appends return fifty distinct lines and leave fifty valid JSON lines.</summary>
    [Fact]
    public async Task Append_FiftyConcurrentCalls_ReturnsDistinctLinesAndValidFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef[] refs = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => writer.Append("Maya", Entry(Noon, $"call {i}")), ct)));

        Assert.Equal(50, refs.Select(static r => r.Line).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 50), refs.Select(static r => r.Line).Order());
        string[] lines = File.ReadAllLines(Path.Combine(dir.Path, "logs", "tool-calls", "Maya", "2026-10-08.jsonl"));
        Assert.Equal(50, lines.Length);
        foreach (string line in lines)
        {
            using JsonDocument json = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        }
    }

    /// <summary>Arguments longer than 200 characters are cut to 200 and end with an ellipsis.</summary>
    [Fact]
    public void Append_LongArguments_AreTruncatedWithEllipsis()
    {
        using TempDataDir dir = new();
        ToolCallLogWriter writer = new(dir.Options());

        ToolCallLogRef reference = writer.Append("Maya", Entry(Noon, new string('x', 500)));

        string line = File.ReadAllLines(Path.Combine(dir.Path, "logs", "tool-calls", "Maya", "2026-10-08.jsonl"))[reference.Line - 1];
        using JsonDocument json = JsonDocument.Parse(line);
        string arguments = json.RootElement.GetProperty("arguments").GetString() ?? string.Empty;
        Assert.Equal(new string('x', 199) + "…", arguments);
    }
}
