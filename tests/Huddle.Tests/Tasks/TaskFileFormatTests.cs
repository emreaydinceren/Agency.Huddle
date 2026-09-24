using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskFileFormat"/>: parsing the frontmatter (Spec §7.2-§7.3 steps 1-2).</summary>
public sealed class TaskFileFormatTests
{
    private static readonly TaskLocation DefaultLocation = new("Platform", null, false);

    /// <summary>The Spec §7.1 example text, one line per array entry, joined with '\n' by <see cref="SpecExampleText"/>.</summary>
    private static readonly string[] SpecExampleLines =
    [
        "---",
        "id: PLAT-0042",
        "title: 'Support SAML login'",
        "status: In Progress",
        "priority: Urgent",
        "creator: Emre",
        "assignee: Nova",
        "origin: 01J8Z4Q6M2",
        "parent: PLAT-0030",
        "blocked_by:",
        "  - PLAT-0011",
        "duplicate_of:",
        "tags:",
        "  - security",
        "start_date: 2026-10-01",
        "due_date: 2026-10-15",
        "---",
        "Implement SAML 2.0 provider integration alongside the existing OAuth2 flow.",
        "",
        "## Change log",
        "- 2026-09-24T11:20:00Z | Emre | created",
        "- 2026-09-24T14:05:12Z | Nova | status: To Do → In Progress",
    ];

    /// <summary>Parsing the Spec §7.1 example reads every frontmatter field and passes Location and Path through.</summary>
    [Fact]
    public void TryParse_SpecExample_ReadsEveryField()
    {
        string text = SpecExampleText();
        TaskLocation location = new("Platform", "Auth", false);
        _ = TaskId.TryParse("PLAT-0042", out TaskId expectedId);
        _ = TaskId.TryParse("PLAT-0030", out TaskId expectedParent);
        _ = TaskId.TryParse("PLAT-0011", out TaskId expectedBlocker);

        bool result = TaskFileFormat.TryParse(text, "tasks/PLAT-0042.md", location, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Equal(expectedId, task.Id);
        Assert.Equal("Support SAML login", task.Title);
        Assert.Equal(TaskState.InProgress, task.Status);
        Assert.Equal(TaskPriority.Urgent, task.Priority);
        Assert.Equal("Emre", task.Creator);
        Assert.Equal("Nova", task.Assignee);
        Assert.Equal("01J8Z4Q6M2", task.OriginRoomId);
        Assert.Equal(expectedParent, task.Parent);
        Assert.Equal([expectedBlocker], task.BlockedBy);
        Assert.Null(task.DuplicateOf);
        Assert.Equal(["security"], task.Tags);
        Assert.Equal(new DateOnly(2026, 10, 1), task.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 15), task.DueDate);
        Assert.StartsWith("Implement SAML 2.0 provider integration alongside the existing OAuth2 flow.", task.Description, StringComparison.Ordinal);
        Assert.Equal(location, task.Location);
        Assert.Equal("tasks/PLAT-0042.md", task.Path);
        Assert.Empty(error);
    }

    /// <summary>Each required key's absence fails, naming the key in the error.</summary>
    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("status")]
    [InlineData("priority")]
    [InlineData("creator")]
    public void TryParse_MissingRequiredKey_FailsNamingIt(string key)
    {
        string text = SpecExampleWithLine(LineForKey(key), replacement: null);

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains(key, error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An unrecognised status value fails, naming the key and the bad value.</summary>
    [Fact]
    public void TryParse_InvalidStatus_Fails()
    {
        string text = SpecExampleWithLine("status: In Progress", "status: Doing");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("status", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Doing", error, StringComparison.Ordinal);
    }

    /// <summary>A duplicate key, compared case-insensitively, fails and names the key.</summary>
    [Theory]
    [MemberData(nameof(DuplicateTitleCases))]
    public void TryParse_DuplicateKey_Fails(string secondTitleKey)
    {
        string text = SpecExampleWithLineInserted("title: 'Support SAML login'", $"{secondTitleKey}: Second title");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("title", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Data for <see cref="TryParse_DuplicateKey_Fails"/>: a second, non-empty title line, same case and mixed case.</summary>
    public static TheoryData<string> DuplicateTitleCases() => new() { "title", "Title" };

    /// <summary>A file with no frontmatter block fails with the specified message.</summary>
    [Fact]
    public void TryParse_NoFrontmatter_Fails()
    {
        string text = "Just some text with no frontmatter block.\n";

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Equal("has no frontmatter block between --- lines", error);
    }

    /// <summary>Unrecognised keys are kept, in the order they were read, in UnknownFields.</summary>
    [Fact]
    public void TryParse_UnknownKeys_KeptInOrder()
    {
        string text = SpecExampleWithLinesInserted("due_date: 2026-10-15", "owner: x", "estimate: 3");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Collection(
            task.UnknownFields,
            kvp =>
            {
                Assert.Equal("owner", kvp.Key);
                Assert.Equal("x", kvp.Value);
            },
            kvp =>
            {
                Assert.Equal("estimate", kvp.Key);
                Assert.Equal("3", kvp.Value);
            });
    }

    /// <summary>CRLF input parses to the same field values, description, unknown fields and change log count as LF input.</summary>
    [Fact]
    public void TryParse_CrlfInput_ParsesTheSame()
    {
        string lfText = SpecExampleText();
        string crlfText = lfText.Replace("\n", "\r\n", StringComparison.Ordinal);

        bool lfResult = TaskFileFormat.TryParse(lfText, "p.md", DefaultLocation, out TaskItem? lfTask, out _);
        bool crlfResult = TaskFileFormat.TryParse(crlfText, "p.md", DefaultLocation, out TaskItem? crlfTask, out _);

        Assert.True(lfResult);
        Assert.True(crlfResult);
        Assert.NotNull(lfTask);
        Assert.NotNull(crlfTask);
        Assert.Equal(lfTask.Id, crlfTask.Id);
        Assert.Equal(lfTask.Title, crlfTask.Title);
        Assert.Equal(lfTask.Status, crlfTask.Status);
        Assert.Equal(lfTask.Priority, crlfTask.Priority);
        Assert.Equal(lfTask.Creator, crlfTask.Creator);
        Assert.Equal(lfTask.Assignee, crlfTask.Assignee);
        Assert.Equal(lfTask.OriginRoomId, crlfTask.OriginRoomId);
        Assert.Equal(lfTask.Parent, crlfTask.Parent);
        Assert.Equal(lfTask.BlockedBy, crlfTask.BlockedBy);
        Assert.Equal(lfTask.DuplicateOf, crlfTask.DuplicateOf);
        Assert.Equal(lfTask.Tags, crlfTask.Tags);
        Assert.Equal(lfTask.StartDate, crlfTask.StartDate);
        Assert.Equal(lfTask.DueDate, crlfTask.DueDate);
        Assert.Equal(lfTask.Description, crlfTask.Description);
        Assert.Equal(lfTask.UnknownFields.Count, crlfTask.UnknownFields.Count);
        for (int i = 0; i < lfTask.UnknownFields.Count; i++)
        {
            Assert.Equal(lfTask.UnknownFields[i].Key, crlfTask.UnknownFields[i].Key);
            Assert.Equal(lfTask.UnknownFields[i].Value, crlfTask.UnknownFields[i].Value);
        }

        Assert.Equal(lfTask.ChangeLog.Count, crlfTask.ChangeLog.Count);
    }

    /// <summary>duplicate_of without status Duplicate fails, naming duplicate_of.</summary>
    [Fact]
    public void TryParse_DuplicateOfWithoutDuplicateStatus_Fails()
    {
        string text = SpecExampleWithLine("duplicate_of:", "duplicate_of: PLAT-0002");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("duplicate_of", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>status Duplicate without duplicate_of fails, naming duplicate_of.</summary>
    [Fact]
    public void TryParse_DuplicateStatusWithoutDuplicateOf_Fails()
    {
        string text = SpecExampleWithLine("status: In Progress", "status: Duplicate");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("duplicate_of", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A tag containing a comma, written as a block list item, fails naming tags and the bad value.</summary>
    [Fact]
    public void TryParse_BadTag_Fails()
    {
        string text = SpecExampleWithLine("  - security", "  - a,b");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("tags", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a,b", error, StringComparison.Ordinal);
    }

    /// <summary>A date that doesn't match yyyy-MM-dd fails, naming due_date and the bad value.</summary>
    [Fact]
    public void TryParse_BadDate_Fails()
    {
        string text = SpecExampleWithLine("due_date: 2026-10-15", "due_date: 15/10/2026");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("due_date", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15/10/2026", error, StringComparison.Ordinal);
    }

    /// <summary>A title longer than 200 characters after trimming fails, naming title.</summary>
    [Fact]
    public void TryParse_TitleOver200_Fails()
    {
        string longTitle = new('a', 201);
        string text = SpecExampleWithLine("title: 'Support SAML login'", $"title: {longTitle}");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("title", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A parent equal to the Task's own id fails, naming parent.</summary>
    [Fact]
    public void TryParse_SelfReference_Fails()
    {
        string text = SpecExampleWithLine("parent: PLAT-0030", "parent: PLAT-0042");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Contains("parent", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The Spec §7.1 example splits into the description and two Change log entries.</summary>
    [Fact]
    public void TryParse_ChangeLog_SplitsDescriptionAndEntries()
    {
        string text = SpecExampleText();

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out _);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Equal("Implement SAML 2.0 provider integration alongside the existing OAuth2 flow.", task.Description);
        Assert.Equal(2, task.ChangeLog.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 11, 20, 0, TimeSpan.Zero), task.ChangeLog[0].At);
        Assert.Equal("Emre", task.ChangeLog[0].Actor);
        Assert.Equal("created", task.ChangeLog[0].Summary);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 14, 5, 12, TimeSpan.Zero), task.ChangeLog[1].At);
        Assert.Equal("Nova", task.ChangeLog[1].Actor);
        Assert.Equal("status: To Do → In Progress", task.ChangeLog[1].Summary);
    }

    /// <summary>When two lines trim to the heading, the split happens at the last one.</summary>
    [Fact]
    public void TryParse_TwoHeadings_SplitsAtTheLast()
    {
        string text = string.Join(
            '\n',
            [
                "---",
                "id: PLAT-0001",
                "title: Sample",
                "status: To Do",
                "priority: Medium",
                "creator: Human",
                "---",
                "Description paragraph one.",
                "",
                "## Change log",
                "This isn't real change log content, more text.",
                "",
                "## Change log",
                "- 2026-01-01T00:00:00Z | Human | created",
            ]) + "\n";

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out _);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Contains("This isn't real change log content, more text.", task.Description, StringComparison.Ordinal);
        Assert.Single(task.ChangeLog);
        Assert.Equal("created", task.ChangeLog[0].Summary);
    }

    /// <summary>A heading line inside a fenced code block is not the split point.</summary>
    [Fact]
    public void TryParse_HeadingInsideFence_IsNotTheSplit()
    {
        string text = string.Join(
            '\n',
            [
                "---",
                "id: PLAT-0001",
                "title: Sample",
                "status: To Do",
                "priority: Medium",
                "creator: Human",
                "---",
                "Description text.",
                "",
                "```",
                "## Change log",
                "```",
                "",
                "## Change log",
                "- 2026-01-01T00:00:00Z | Human | created",
            ]) + "\n";

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out _);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Contains("```", task.Description, StringComparison.Ordinal);
        Assert.Single(task.ChangeLog);
        Assert.Equal("created", task.ChangeLog[0].Summary);
    }

    /// <summary>A log line that doesn't match the entry grammar is kept but ignored, not an error.</summary>
    [Fact]
    public void TryParse_UnparseableLogLine_IgnoredNotError()
    {
        string text = MinimalTextWithChangeLog("not a valid log line", "- 2026-01-01T00:00:00Z | Human | created");

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out _);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Single(task.ChangeLog);
        Assert.Equal("created", task.ChangeLog[0].Summary);
    }

    /// <summary>FormatEntry escapes '\' to '\\', '|' to '\|', and any line break to a single space.</summary>
    [Fact]
    public void FormatEntry_EscapesPipeBackslashAndNewline()
    {
        const string Backslash = "\\";
        const string Pipe = "|";
        string actor = "A" + Backslash + "B";
        string summary = "x" + Pipe + "y" + "\n" + "z" + Backslash;
        ChangeLogEntry entry = new(new DateTimeOffset(2026, 9, 24, 14, 5, 12, TimeSpan.Zero), actor, summary);

        string formatted = TaskFileFormat.FormatEntry(entry);

        string expectedActor = "A" + Backslash + Backslash + "B";
        string expectedSummary = "x" + Backslash + Pipe + "y" + " " + "z" + Backslash + Backslash;
        Assert.Equal("- 2026-09-24T14:05:12Z | " + expectedActor + " | " + expectedSummary, formatted);
    }

    /// <summary>An entry containing a literal backslash immediately followed by a literal pipe round-trips through Format and Parse.</summary>
    [Fact]
    public void TryParse_EscapedEntry_RoundTrips()
    {
        const string Backslash = "\\";
        const string Pipe = "|";
        string actor = "A" + Backslash + "B";
        string summary = "x" + Backslash + Pipe + "y";
        DateTimeOffset at = new(2026, 9, 24, 14, 5, 12, TimeSpan.Zero);
        ChangeLogEntry original = new(at, actor, summary);
        string logLine = TaskFileFormat.FormatEntry(original);
        string text = MinimalTextWithChangeLog(logLine);

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out _);

        Assert.True(result);
        Assert.NotNull(task);
        ChangeLogEntry parsed = Assert.Single(task.ChangeLog);
        Assert.Equal(at, parsed.At);
        Assert.Equal(actor, parsed.Actor);
        Assert.Equal(summary, parsed.Summary);
    }

    /// <summary>AppendEntry adds the heading, then the formatted entry, to text with no existing Change log.</summary>
    [Fact]
    public void AppendEntry_NoHeading_AddsHeadingThenEntry()
    {
        string fileText = string.Join(
            '\n',
            [
                "---",
                "id: PLAT-0001",
                "title: Sample",
                "status: To Do",
                "priority: Medium",
                "creator: Human",
                "---",
                "Description text.",
            ]) + "\n";
        ChangeLogEntry entry = TestTasks.Entry("2026-09-24T14:05:12Z", "Human", "created");

        string result = TaskFileFormat.AppendEntry(fileText, entry);

        Assert.Contains("Description text.", result, StringComparison.Ordinal);
        Assert.Contains(TaskFileFormat.ChangeLogHeading, result, StringComparison.Ordinal);
        Assert.EndsWith(TaskFileFormat.FormatEntry(entry), result.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>ClosedAt is the last "closed" entry's At not followed by a "reopened" entry, only when Location.Closed is true.</summary>
    [Theory]
    [MemberData(nameof(ClosedAtCases))]
    public void ClosedAt_LastClosedNotFollowedByReopened_WhenLocationClosed(ChangeLogEntry[] entries, bool locationClosed, DateTimeOffset? expectedClosedAt)
    {
        string[] logLines = [.. entries.Select(TaskFileFormat.FormatEntry)];
        string text = MinimalTextWithChangeLog(logLines);
        TaskLocation location = new("Platform", null, locationClosed);

        bool result = TaskFileFormat.TryParse(text, "p.md", location, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Equal(expectedClosedAt, task.ClosedAt);
    }

    /// <summary>Data for <see cref="ClosedAt_LastClosedNotFollowedByReopened_WhenLocationClosed"/>.</summary>
    public static TheoryData<ChangeLogEntry[], bool, DateTimeOffset?> ClosedAtCases()
    {
        DateTimeOffset created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset closed1 = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset reopened1 = new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset closed2 = new(2026, 1, 4, 0, 0, 0, TimeSpan.Zero);

        return new()
        {
            {
                [new ChangeLogEntry(created, "Human", "created"), new ChangeLogEntry(closed1, "Human", "closed")],
                true,
                closed1
            },
            {
                [
                    new ChangeLogEntry(created, "Human", "created"),
                    new ChangeLogEntry(closed1, "Human", "closed"),
                    new ChangeLogEntry(reopened1, "Human", "reopened"),
                ],
                false,
                null
            },
            {
                [
                    new ChangeLogEntry(created, "Human", "created"),
                    new ChangeLogEntry(closed1, "Human", "closed"),
                    new ChangeLogEntry(reopened1, "Human", "reopened"),
                    new ChangeLogEntry(closed2, "Human", "closed"),
                ],
                true,
                closed2
            },
            {
                [new ChangeLogEntry(created, "Human", "created"), new ChangeLogEntry(closed1, "Human", "closed")],
                false,
                null
            },
        };
    }

    /// <summary>Compose writes the frontmatter keys in the Spec §7.2 order, then any unknown keys.</summary>
    [Fact]
    public void Compose_KeysInSpecOrder()
    {
        TaskItem task = TestTasks.Make(
            id: "PLAT-0042",
            title: "Support SAML login",
            status: TaskState.InProgress,
            priority: TaskPriority.Urgent,
            creator: "Emre",
            assignee: "Nova",
            originRoomId: "01J8Z4Q6M2",
            parent: Id("PLAT-0030"),
            blockedBy: [Id("PLAT-0011")],
            tags: ["security"],
            startDate: new DateOnly(2026, 10, 1),
            dueDate: new DateOnly(2026, 10, 15));

        string composed = TaskFileFormat.Compose(task);
        string[] lines = composed.Split('\n');

        string[] keysInOrder =
        [
            "id", "title", "status", "priority", "creator", "assignee", "origin",
            "parent", "blocked_by", "duplicate_of", "tags", "start_date", "due_date",
        ];
        int lastIndex = -1;
        foreach (string key in keysInOrder)
        {
            int index = Array.FindIndex(lines, line => line.StartsWith(key + ":", StringComparison.Ordinal));
            Assert.True(index > lastIndex, $"Key '{key}' is out of Spec §7.2 order.");
            lastIndex = index;
        }
    }

    /// <summary>Unsafe scalars are single-quoted with '' escaping; a plain word is written unquoted.</summary>
    [Theory]
    [InlineData("a: b", "creator: 'a: b'")]
    [InlineData("#x", "creator: '#x'")]
    [InlineData("it's", "creator: 'it''s'")]
    [InlineData(" leading", "creator: ' leading'")]
    [InlineData("PlainWord", "creator: PlainWord")]
    public void Compose_QuotesUnsafeScalars(string value, string expectedLine)
    {
        TaskItem task = TestTasks.Make(creator: value);

        string composed = TaskFileFormat.Compose(task);

        string line = composed.Split('\n').First(l => l.StartsWith("creator:", StringComparison.Ordinal));
        Assert.Equal(expectedLine, line);
    }

    /// <summary>A scalar equal to a block scalar indicator, or empty after trimming, is quoted so it doesn't read back as a block scalar.</summary>
    [Theory]
    [InlineData(">")]
    [InlineData(">-")]
    [InlineData(">+")]
    [InlineData("|")]
    [InlineData("|-")]
    [InlineData("|+")]
    [InlineData(" ")]
    public void Compose_QuotesBlockScalarIndicators(string value)
    {
        TaskItem task = TestTasks.Make(title: value);

        string composed = TaskFileFormat.Compose(task);

        string line = composed.Split('\n').First(l => l.StartsWith("title:", StringComparison.Ordinal));
        Assert.Equal($"title: '{value}'", line);
    }

    /// <summary>List fields (blocked_by, tags) are written as block lists.</summary>
    [Fact]
    public void Compose_ListsAsBlockLists()
    {
        TaskItem task = TestTasks.Make(tags: ["security", "urgent"], blockedBy: [Id("PLAT-0011"), Id("PLAT-0012")]);

        string composed = TaskFileFormat.Compose(task);
        string[] lines = composed.Split('\n');

        int tagsIndex = Array.IndexOf(lines, "tags:");
        Assert.True(tagsIndex >= 0);
        Assert.Equal("  - security", lines[tagsIndex + 1]);
        Assert.Equal("  - urgent", lines[tagsIndex + 2]);

        int blockedByIndex = Array.IndexOf(lines, "blocked_by:");
        Assert.True(blockedByIndex >= 0);
        Assert.Equal("  - PLAT-0011", lines[blockedByIndex + 1]);
        Assert.Equal("  - PLAT-0012", lines[blockedByIndex + 2]);
    }

    /// <summary>Empty optional keys are omitted, except duplicate_of, which is written bare when status isn't Duplicate.</summary>
    [Fact]
    public void Compose_OmitsEmptyOptionalKeys()
    {
        TaskItem task = TestTasks.Make();

        string composed = TaskFileFormat.Compose(task);
        string[] lines = composed.Split('\n');

        Assert.DoesNotContain(lines, l => l.StartsWith("assignee:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("origin:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("parent:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("blocked_by:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("tags:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("start_date:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("due_date:", StringComparison.Ordinal));
        Assert.Contains("duplicate_of:", lines);
    }

    /// <summary>Composed text uses '\n' line endings only.</summary>
    [Fact]
    public void Compose_UsesLfOnly()
    {
        TaskItem task = TestTasks.Make(description: "Line one.\nLine two.");

        string composed = TaskFileFormat.Compose(task);

        Assert.DoesNotContain("\r", composed, StringComparison.Ordinal);
    }

    /// <summary>Compose(Parse(Compose(x))) equals Compose(x) for the Spec §7.1 example.</summary>
    [Fact]
    public void Compose_ParseRoundTrip_IsStable()
    {
        TaskLocation location = new("Platform", "Auth", false);
        bool parsedOnce = TaskFileFormat.TryParse(SpecExampleText(), "p.md", location, out TaskItem? task, out _);
        Assert.True(parsedOnce);
        Assert.NotNull(task);

        string composedOnce = TaskFileFormat.Compose(task);
        bool parsedTwice = TaskFileFormat.TryParse(composedOnce, "p.md", location, out TaskItem? task2, out _);
        Assert.True(parsedTwice);
        Assert.NotNull(task2);

        string composedTwice = TaskFileFormat.Compose(task2);

        Assert.Equal(composedOnce, composedTwice);
    }

    /// <summary>Unknown fields are written after every known key, in the order they were read.</summary>
    [Fact]
    public void Compose_UnknownFields_WrittenAfterKnownKeys()
    {
        TaskItem task = TestTasks.Make(unknownFields: [new("owner", "x"), new("estimate", "3")]);

        string composed = TaskFileFormat.Compose(task);
        string[] lines = composed.Split('\n');

        int duplicateOfIndex = Array.FindIndex(lines, l => l.StartsWith("duplicate_of:", StringComparison.Ordinal));
        int ownerIndex = Array.IndexOf(lines, "owner: x");
        int estimateIndex = Array.IndexOf(lines, "estimate: 3");

        Assert.True(duplicateOfIndex >= 0);
        Assert.True(ownerIndex > duplicateOfIndex);
        Assert.True(estimateIndex > ownerIndex);
    }

    /// <summary>ContainsChangeLogHeading detects the heading line only outside a fenced code block.</summary>
    [Theory]
    [InlineData("Some text.\n\n## Change log\n", true)]
    [InlineData("Some text.\n\n```\n## Change log\n```\n", false)]
    public void ContainsChangeLogHeading_DetectsOnlyOutsideFences(string description, bool expected)
    {
        bool result = TaskFileFormat.ContainsChangeLogHeading(description);

        Assert.Equal(expected, result);
    }

    /// <summary>Parses a TaskId from a known-valid string, for building expected values in tests.</summary>
    private static TaskId Id(string text)
    {
        _ = TaskId.TryParse(text, out TaskId id);
        return id;
    }

    /// <summary>A minimal valid Task file with a Change log section containing the given raw log lines.</summary>
    private static string MinimalTextWithChangeLog(params string[] logLines)
    {
        List<string> lines =
        [
            "---",
            "id: PLAT-0001",
            "title: Sample",
            "status: To Do",
            "priority: Medium",
            "creator: Human",
            "---",
            "Description text.",
            "",
            TaskFileFormat.ChangeLogHeading,
        ];
        lines.AddRange(logLines);
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>The Spec §7.1 example text, joined with '\n' line endings.</summary>
    private static string SpecExampleText() => string.Join('\n', SpecExampleLines) + "\n";

    /// <summary>The exact §7.1 example line for a required key, so a failure fixture can remove or replace it.</summary>
    private static string LineForKey(string key) => key switch
    {
        "id" => "id: PLAT-0042",
        "title" => "title: 'Support SAML login'",
        "status" => "status: In Progress",
        "priority" => "priority: Urgent",
        "creator" => "creator: Emre",
        _ => throw new ArgumentException("Unknown required key.", nameof(key)),
    };

    /// <summary>The §7.1 example with one line replaced (or removed, when <paramref name="replacement"/> is null).</summary>
    private static string SpecExampleWithLine(string originalLine, string? replacement)
    {
        List<string> lines = [.. SpecExampleLines];
        int index = lines.IndexOf(originalLine);
        if (replacement is null)
        {
            lines.RemoveAt(index);
        }
        else
        {
            lines[index] = replacement;
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>The §7.1 example with one extra line inserted right after <paramref name="afterLine"/>.</summary>
    private static string SpecExampleWithLineInserted(string afterLine, string insertedLine) =>
        SpecExampleWithLinesInserted(afterLine, insertedLine);

    /// <summary>The §7.1 example with one or more extra lines inserted, in order, right after <paramref name="afterLine"/>.</summary>
    private static string SpecExampleWithLinesInserted(string afterLine, params string[] insertedLines)
    {
        List<string> lines = [.. SpecExampleLines];
        int index = lines.IndexOf(afterLine);
        lines.InsertRange(index + 1, insertedLines);
        return string.Join('\n', lines) + "\n";
    }
}
