using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for the timeline grammar of <see cref="TaskFileFormat"/> (Spec §7.7.1, ADR-0086): reading, writing and the reserved headings.</summary>
public sealed class TaskTimelineFormatTests
{
    private static readonly TaskLocation DefaultLocation = new("Platform", null, false);

    /// <summary>The five required keys, closed, as the start of every file in these tests.</summary>
    private const string Frontmatter = "---\nid: PLAT-0001\ntitle: Sample\nstatus: To Do\npriority: Medium\ncreator: Human\n---\n";

    /// <summary>Parses a Task file that must be valid.</summary>
    private static TaskItem Parse(string text)
    {
        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result, error);
        Assert.NotNull(task);
        return task;
    }

    /// <summary>A body with a Timeline holding one entry of each kind, an older three-field line and a junk line: the description stops at the heading and every line is read by its kind.</summary>
    [Fact]
    public void TryParse_Timeline_SplitsDescriptionAndReadsEachKind()
    {
        string text = Frontmatter + string.Join(
            '\n',
            "Body text.",
            "",
            "## Timeline",
            "- 2026-09-24T11:20:00Z | Emre | event | created",
            "- 2026-09-24T16:41:03Z | Nova | evidence | dotnet test Auth.slnx | exit 0 | .huddle/logs/nova/tool-calls.md#L12",
            @"- 2026-09-24T16:42:10Z | Emre | comment | Looks right.\nPlease cover a \| pipe and a \\ slash.",
            "- 2026-09-24T17:00:00Z | Emre | an older three field line",
            "this line is junk",
            "");

        TaskItem task = Parse(text);

        Assert.Equal("Body text.", task.Description);
        Assert.Equal(
            [
                new ChangeLogEntry(new DateTimeOffset(2026, 9, 24, 11, 20, 0, TimeSpan.Zero), "Emre", "created"),
                new ChangeLogEntry(new DateTimeOffset(2026, 9, 24, 17, 0, 0, TimeSpan.Zero), "Emre", "an older three field line"),
            ],
            task.ChangeLog);
        Assert.Equal(
            [
                new TimelineEntry(new DateTimeOffset(2026, 9, 24, 16, 41, 3, TimeSpan.Zero), "Nova", TimelineEntryKind.Evidence, "dotnet test Auth.slnx | exit 0 | .huddle/logs/nova/tool-calls.md#L12"),
                new TimelineEntry(new DateTimeOffset(2026, 9, 24, 16, 42, 10, TimeSpan.Zero), "Emre", TimelineEntryKind.Comment, "Looks right.\nPlease cover a | pipe and a \\ slash."),
            ],
            task.Comments);
    }

    /// <summary>The kind is matched case-sensitively: <c>Comment</c> is not a kind, so the line is an older Change log line whose summary starts with it.</summary>
    [Fact]
    public void TryParse_KindInWrongCase_IsNotAComment()
    {
        string text = Frontmatter + "Body.\n\n## Timeline\n- 2026-09-24T11:20:00Z | Emre | Comment | hi\n";

        TaskItem task = Parse(text);

        Assert.Empty(task.Comments);
        Assert.Equal("Comment | hi", Assert.Single(task.ChangeLog).Summary);
    }

    /// <summary>A <c>## Timeline</c> line inside a fenced block of the description is not the heading: the real one, later, is.</summary>
    [Fact]
    public void TryParse_TimelineHeadingInsideFence_IsNotTheSplit()
    {
        string text = Frontmatter + "Body.\n\n```\n## Timeline\n```\n\n## Timeline\n- 2026-09-24T11:20:00Z | Emre | event | created\n";

        TaskItem task = Parse(text);

        Assert.Equal("Body.\n\n```\n## Timeline\n```", task.Description);
        Assert.Equal("created", Assert.Single(task.ChangeLog).Summary);
    }

    /// <summary>The heading is found case-insensitively and with surrounding spaces, like the Change log heading always was.</summary>
    [Fact]
    public void TryParse_TimelineHeadingInOtherCase_IsTheSplit()
    {
        string text = Frontmatter + "Body.\n\n  ## TIMELINE  \n- 2026-09-24T11:20:00Z | Emre | comment | hi\n";

        TaskItem task = Parse(text);

        Assert.Equal("Body.", task.Description);
        Assert.Equal("hi", Assert.Single(task.Comments).Text);
    }

    /// <summary>An <c>evidence</c> entry may carry the minute-precision time an older Evidence line had.</summary>
    [Fact]
    public void TryParse_EvidenceWithMinuteTime_IsRead()
    {
        string text = Frontmatter + "Body.\n\n## Timeline\n- 2026-09-24T16:41Z | Nova | evidence | dotnet test | exit 0\n";

        TaskItem task = Parse(text);

        TimelineEntry entry = Assert.Single(task.Comments);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 16, 41, 0, TimeSpan.Zero), entry.At);
        Assert.Equal("dotnet test", Assert.Single(task.Evidence).Command);
    }

    /// <summary>Each kind written by FormatTimelineEntry reads back as the same entry, including a comment with CRLF, a pipe and a backslash.</summary>
    [Fact]
    public void FormatTimelineEntry_EachKind_RoundTripsThroughTryParse()
    {
        DateTimeOffset at = new(2026, 9, 24, 16, 42, 10, TimeSpan.Zero);
        TimelineEntry comment = new(at, "Emre | the \\ boss", TimelineEntryKind.Comment, "one\r\ntwo | three\\four\n\\n literal");
        TimelineEntry evidence = new(at.AddSeconds(1), "Nova", TimelineEntryKind.Evidence, "dotnet test | exit 0 | note: ok");
        TimelineEntry eventEntry = new(at.AddSeconds(2), "Nova", TimelineEntryKind.Event, "status: To Do → In Progress");
        string text = Frontmatter + "Body.\n\n## Timeline\n"
            + string.Join('\n', new[] { comment, evidence, eventEntry }.Select(TaskFileFormat.FormatTimelineEntry)) + "\n";

        TaskItem task = Parse(text);

        Assert.Equal(
            [comment with { Text = "one\ntwo | three\\four\n\\n literal" }, evidence],
            task.Comments);
        Assert.Equal(new ChangeLogEntry(eventEntry.At, "Nova", eventEntry.Text), Assert.Single(task.ChangeLog));
    }

    /// <summary>A comment of several lines is still one file line: every line break is the two characters backslash and n.</summary>
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    public void FormatTimelineEntry_CommentLineBreak_IsEscapedToOneLine(string text)
    {
        TimelineEntry entry = new(new DateTimeOffset(2026, 9, 24, 16, 42, 10, TimeSpan.Zero), "Emre", TimelineEntryKind.Comment, text);

        string line = TaskFileFormat.FormatTimelineEntry(entry);

        Assert.Equal(@"- 2026-09-24T16:42:10Z | Emre | comment | a\nb", line);
    }

    /// <summary>An event is written as <c>- {at} | {actor} | event | {summary}</c>, which is what FormatEntry writes.</summary>
    [Fact]
    public void FormatEntry_WritesTheEventKind()
    {
        ChangeLogEntry entry = new(new DateTimeOffset(2026, 9, 24, 14, 5, 12, TimeSpan.Zero), "Nova", "status: To Do → In Progress");

        string line = TaskFileFormat.FormatEntry(entry);

        Assert.Equal("- 2026-09-24T14:05:12Z | Nova | event | status: To Do → In Progress", line);
    }

    /// <summary>The timeline is the comments and the events merged by time, a comment before an event at the same second, and Updated is its last entry.</summary>
    [Fact]
    public void Timeline_MergesByTime_CommentBeforeEventOnATie_AndUpdatedIsTheLastEntry()
    {
        string text = Frontmatter + string.Join(
            '\n',
            "Body.",
            "",
            "## Timeline",
            "- 2026-09-24T11:00:00Z | Emre | event | created",
            "- 2026-09-24T12:00:00Z | Nova | event | status: To Do → In Progress",
            "- 2026-09-24T12:00:00Z | Emre | comment | same second",
            "- 2026-09-24T13:00:00Z | Emre | comment | last",
            "");

        TaskItem task = Parse(text);

        Assert.Equal(["created", "same second", "status: To Do → In Progress", "last"], task.Timeline.Select(static entry => entry.Text));
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 13, 0, 0, TimeSpan.Zero), task.Updated);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 11, 0, 0, TimeSpan.Zero), task.Created);
    }

    /// <summary>Compose writes the timeline, and the text it writes reads back as the same comments, events and description.</summary>
    [Fact]
    public void Compose_Timeline_RoundTripsThroughTryParse()
    {
        string text = Frontmatter + "Body.\n\n## Timeline\n- 2026-09-24T11:00:00Z | Emre | event | created\n- 2026-09-24T12:00:00Z | Emre | comment | hi\\nthere\n- 2026-09-24T12:30:00Z | Nova | evidence | dotnet test | exit 0\n";
        TaskItem task = Parse(text);

        string composed = TaskFileFormat.Compose(task);

        TaskItem again = Parse(composed);
        Assert.Equal(task.Description, again.Description);
        Assert.Equal(task.ChangeLog, again.ChangeLog);
        Assert.Equal(task.Comments, again.Comments);
        Assert.EndsWith("## Timeline\n- 2026-09-24T11:00:00Z | Emre | event | created\n- 2026-09-24T12:00:00Z | Emre | comment | hi\\nthere\n- 2026-09-24T12:30:00Z | Nova | evidence | dotnet test | exit 0", composed, StringComparison.Ordinal);
    }

    /// <summary>Each reserved heading is found in any case and with spaces around it, and is returned in its canonical spelling.</summary>
    [Theory]
    [InlineData("Body\n## Timeline\nmore", "## Timeline")]
    [InlineData("Body\n  ## change LOG  \nmore", "## Change log")]
    [InlineData("Body\n## evidence\nmore", "## Evidence")]
    [InlineData("Body\r\n## Evidence\r\nmore", "## Evidence")]
    [InlineData("## Evidence\n## Timeline", "## Evidence")]
    [InlineData("## Timeline\n## Evidence", "## Timeline")]
    public void FindReservedHeading_ReservedHeading_ReturnsCanonicalSpelling(string description, string expected)
    {
        string? heading = TaskFileFormat.FindReservedHeading(description);

        Assert.Equal(expected, heading);
    }

    /// <summary>A reserved heading inside a fenced block, an ordinary heading, and a heading with extra text are not reserved.</summary>
    [Theory]
    [InlineData("Body\n```\n## Timeline\n## Change log\n## Evidence\n```\nmore")]
    [InlineData("Body\n~~~\n## Evidence\n~~~")]
    [InlineData("Body\n## Notes\nmore")]
    [InlineData("Body\n## Timeline of events\nmore")]
    [InlineData("")]
    public void FindReservedHeading_NoReservedHeadingOutsideFences_ReturnsNull(string description)
    {
        string? heading = TaskFileFormat.FindReservedHeading(description);

        Assert.Null(heading);
    }
}
