using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for reading a Task file written before the timeline, converting it on first write, and the guarantee of #586:
/// a description write never reaches an Evidence entry (Spec §7.7.3, §7.7.4, ADR-0086).
/// </summary>
public sealed class TaskTimelineLegacyTests
{
    private static readonly TaskLocation DefaultLocation = new("Platform", null, false);

    /// <summary>A hand-edited frontmatter block: a quoted value and a key the parser does not know.</summary>
    private const string HandEditedFrontmatter = "---\nid: PLAT-0001\ntitle: 'Sample: a task'\nstatus: To Do\npriority: Medium\ncreator: Human\nowner_note: 'kept, as written'\n---\n";

    /// <summary>The old description: text, an Evidence section (a structured line, an undated note, a line with a note field, a sub-heading and a line under it) and a section after it.</summary>
    private const string OldDescription =
        "Do the thing.\n\n## Evidence\n"
        + "- 2026-10-08T12:00Z | Nova | dotnet test | exit 0 | .huddle/logs/nova/tool-calls.md#L12\n"
        + "Checked by hand on staging.\n"
        + "- 2026-10-08T12:30Z | Nova | dotnet build | exit 0 | note: clean build\n"
        + "### sub heading\n"
        + "line under the sub heading\n"
        + "\n## Notes\nTail.";

    /// <summary>The old Change log, with a hand-written line between two entries.</summary>
    private const string OldChangeLog =
        "## Change log\n"
        + "- 2026-10-08T11:00:00Z | Human | created\n"
        + "a hand written remark that never parsed\n"
        + "- 2026-10-08T12:00:05Z | Nova | evidence added by Nova\n"
        + "- 2026-10-08T12:30:04Z | Nova | evidence added by Nova\n";

    private static readonly string LegacyFile = HandEditedFrontmatter + OldDescription + "\n\n" + OldChangeLog;

    /// <summary>Parses a Task file that must be valid.</summary>
    private static TaskItem Parse(string text)
    {
        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result, error);
        Assert.NotNull(task);
        return task;
    }

    /// <summary>The exact shape of #586: the description of an older file no longer holds the Evidence section, and the events are the Change log lines.</summary>
    [Fact]
    public void TryParse_LegacyFile_DescriptionHasNoEvidenceAndEventsAreTheChangeLog()
    {
        TaskItem task = Parse(LegacyFile);

        Assert.Equal("Do the thing.\n\n## Notes\nTail.", task.Description);
        Assert.Equal(
            ["created", "evidence added by Nova", "evidence added by Nova"],
            task.ChangeLog.Select(static entry => entry.Summary));
    }

    /// <summary>Proof sees what it saw: every line <c>ParseSection</c> read is an Evidence entry with the same fields, in order, and the lines the description lost are notes after them.</summary>
    [Fact]
    public void TryParse_LegacyFile_EvidenceMatchesParseSection()
    {
        IReadOnlyList<EvidenceLine> expected = EvidenceLine.ParseSection(LegacyFile);

        TaskItem task = Parse(LegacyFile);

        IReadOnlyList<EvidenceLine> actual = task.Evidence;
        Assert.Equal(5, actual.Count);
        Assert.Equal(3, expected.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Author, actual[i].Author);
            Assert.Equal(expected[i].Command, actual[i].Command);
            Assert.Equal(expected[i].ExitCode, actual[i].ExitCode);
            Assert.Equal(expected[i].LogPath, actual[i].LogPath);
            Assert.Equal(expected[i].LogLine, actual[i].LogLine);
            Assert.Equal(expected[i].Note, actual[i].Note);
            if (expected[i].Command.Length > 0)
            {
                Assert.Equal(expected[i].Time, actual[i].Time);
            }
            else
            {
                Assert.Equal(expected[i].RawText, actual[i].RawText);
            }
        }

        Assert.Equal(["### sub heading", "line under the sub heading"], actual.Skip(3).Select(static line => line.RawText));
        Assert.All(actual.Skip(3), static line => Assert.Equal(0, line.Command.Length));
    }

    /// <summary>The timeline of an older file is the events and the Evidence merged by time, an undated note placed after the line before it.</summary>
    [Fact]
    public void TryParse_LegacyFile_TimelineIsMergedByTime()
    {
        TaskItem task = Parse(LegacyFile);

        Assert.Equal(
            [
                (TimelineEntryKind.Event, "created"),
                (TimelineEntryKind.Evidence, "dotnet test | exit 0 | .huddle/logs/nova/tool-calls.md#L12"),
                (TimelineEntryKind.Evidence, "note: Checked by hand on staging."),
                (TimelineEntryKind.Event, "evidence added by Nova"),
                (TimelineEntryKind.Evidence, "dotnet build | exit 0 | note: clean build"),
                (TimelineEntryKind.Evidence, "note: ### sub heading"),
                (TimelineEntryKind.Evidence, "note: line under the sub heading"),
                (TimelineEntryKind.Event, "evidence added by Nova"),
            ],
            task.Timeline.Select(static entry => (entry.Kind, entry.Text)));
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 12, 30, 4, TimeSpan.Zero), task.Updated);
    }

    /// <summary>An undated Evidence line first in the section sorts before every event, so it is not placed after later ones.</summary>
    [Fact]
    public void TryParse_LegacyFile_UndatedFirstEvidenceLineSortsFirst()
    {
        string text = HandEditedFrontmatter + "Body.\n\n## Evidence\nA remark made first.\n- 2026-10-08T12:00Z | Nova | dotnet test | exit 0\n\n## Change log\n- 2026-10-08T11:00:00Z | Human | created\n";

        TaskItem task = Parse(text);

        Assert.Equal(
            ["note: A remark made first.", "created", "dotnet test | exit 0"],
            task.Timeline.Select(static entry => entry.Text));
    }

    /// <summary>The description of an older file is what the content fingerprint always hashed, so an acceptance recorded before the change still matches: it equals the old description with its Evidence removed, lower-case section included.</summary>
    [Fact]
    public void TryParse_LegacyFile_DescriptionEqualsTheOldFingerprintRule()
    {
        string oldDescription = OldDescription + "\n\n## evidence\nsmall print\n\n## Last\nEnd.";
        string text = HandEditedFrontmatter + oldDescription + "\n\n" + OldChangeLog;

        TaskItem task = Parse(text);

        Assert.Equal(ContentFingerprint.WithoutEvidence(oldDescription), task.Description);
        Assert.Equal(
            ContentFingerprint.Of("Sample: a task", null, oldDescription),
            ContentFingerprint.Of("Sample: a task", null, task.Description));
    }

    /// <summary>No non-blank line of an older body is lost: each is in the description or in the text of a comment or Evidence entry, headings of removed sections aside.</summary>
    [Fact]
    public void TryParse_LegacyFile_NoLineIsLost()
    {
        string oldDescription = OldDescription + "\n\n## evidence\nsmall print\n\n## Last\nEnd.";
        string text = HandEditedFrontmatter + oldDescription + "\n\n" + OldChangeLog;

        TaskItem task = Parse(text);

        string kept = task.Description + "\n" + string.Join('\n', task.Comments.Select(static entry => entry.Text));
        IEnumerable<string> lines = oldDescription.Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0
                && !line.StartsWith("- 2026-", StringComparison.Ordinal)
                && !string.Equals(line, "## Evidence", StringComparison.OrdinalIgnoreCase));
        foreach (string line in lines)
        {
            Assert.True(kept.Contains(line, StringComparison.Ordinal), $"Lost: {line}");
        }

        Assert.Contains("dotnet test", kept, StringComparison.Ordinal);
        Assert.Contains("dotnet build", kept, StringComparison.Ordinal);
    }

    /// <summary>Converting an older file keeps its frontmatter byte for byte and copies Change log lines verbatim, a hand-written line staying right after the line before it.</summary>
    [Fact]
    public void Upgrade_LegacyFile_KeepsFrontmatterAndCopiesChangeLogLinesVerbatim()
    {
        string upgraded = TaskFileFormat.Upgrade(LegacyFile);

        Assert.StartsWith(HandEditedFrontmatter + "Do the thing.\n\n## Notes\nTail.\n\n## Timeline\n", upgraded, StringComparison.Ordinal);
        string[] lines = upgraded.Split('\n');
        int created = Array.IndexOf(lines, "- 2026-10-08T11:00:00Z | Human | created");
        Assert.True(created > 0);
        Assert.Equal("a hand written remark that never parsed", lines[created + 1]);
        Assert.Equal("- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 0 | .huddle/logs/nova/tool-calls.md#L12", lines[created + 2]);
        Assert.Contains("- 2026-10-08T12:00:05Z | Nova | evidence added by Nova", lines);
        Assert.DoesNotContain("## Change log", upgraded, StringComparison.Ordinal);
        Assert.DoesNotContain("## Evidence", upgraded, StringComparison.Ordinal);
        Assert.EndsWith("\n", upgraded, StringComparison.Ordinal);
    }

    /// <summary>Reading a converted file gives the same description, events, comments, Evidence and timeline as reading the older file.</summary>
    [Fact]
    public void Upgrade_LegacyFile_ReadsTheSameAfterConversion()
    {
        TaskItem before = Parse(LegacyFile);

        TaskItem after = Parse(TaskFileFormat.Upgrade(LegacyFile));

        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.ChangeLog, after.ChangeLog);
        Assert.Equal(before.Comments, after.Comments);
        Assert.Equal(before.Evidence, after.Evidence);
        Assert.Equal(before.Timeline, after.Timeline);
    }

    /// <summary>Converting twice changes nothing the second time, and a file already in the timeline format comes back as it was (with '\n' line endings).</summary>
    [Fact]
    public void Upgrade_IsIdempotent_AndLeavesATimelineFileAlone()
    {
        string once = TaskFileFormat.Upgrade(LegacyFile);

        Assert.Equal(once, TaskFileFormat.Upgrade(once));
        Assert.Equal(once, TaskFileFormat.Upgrade(once.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    /// <summary>A hand-written Change log line whose summary the timeline grammar would read as a comment is written in the timeline form, so the conversion does not change what it means.</summary>
    [Fact]
    public void Upgrade_ChangeLogLineThatLooksLikeAComment_StaysAnEvent()
    {
        string text = HandEditedFrontmatter + "Body.\n\n## Change log\n- 2026-10-08T11:00:00Z | Human | comment | hand written\n";
        TaskItem before = Parse(text);

        TaskItem after = Parse(TaskFileFormat.Upgrade(text));

        Assert.Equal(before.ChangeLog, after.ChangeLog);
        Assert.Empty(after.Comments);
    }

    /// <summary>A file with no frontmatter block is not converted, only given '\n' line endings.</summary>
    [Fact]
    public void Upgrade_NoFrontmatter_ReturnsTheTextWithLfEndings()
    {
        string upgraded = TaskFileFormat.Upgrade("just text\r\nmore");

        Assert.Equal("just text\nmore", upgraded);
    }

    /// <summary>The guarantee of #586, on a file in the timeline format: a new description keeps every Evidence entry and every byte from the Timeline heading onward.</summary>
    [Fact]
    public void ReplaceHead_TimelineFile_KeepsEvidenceAndEveryByteFromTheHeading()
    {
        string text = HandEditedFrontmatter + "Old description.\n\n## Timeline\n- 2026-10-08T11:00:00Z | Human | event | created\n- 2026-10-08T12:00:00Z | Nova | evidence | dotnet test | exit 0\n- 2026-10-08T12:01:00Z | Emre | comment | keep\\nme\nhand edited junk line\n";
        TaskItem task = Parse(text);

        string result = TaskFileFormat.ReplaceHead(text, task with { Description = "A brand new description." });

        TaskItem after = Parse(result);
        Assert.Equal("A brand new description.", after.Description);
        Assert.Equal(task.Evidence, after.Evidence);
        Assert.Equal(task.Comments, after.Comments);
        Assert.Equal(text[text.IndexOf("## Timeline", StringComparison.Ordinal)..], result[result.IndexOf("## Timeline", StringComparison.Ordinal)..]);
    }

    /// <summary>The guarantee of #586, on an older file: a new description keeps every Evidence entry, the events and the hand-written line, and the file is converted.</summary>
    [Fact]
    public void ReplaceHead_LegacyFile_KeepsEveryEvidenceEntry()
    {
        TaskItem task = Parse(LegacyFile);

        string result = TaskFileFormat.ReplaceHead(LegacyFile, task with { Description = "A brand new description." });

        TaskItem after = Parse(result);
        Assert.Equal("A brand new description.", after.Description);
        Assert.Equal(task.Evidence, after.Evidence);
        Assert.Equal(task.ChangeLog, after.ChangeLog);
        Assert.Equal(task.Timeline, after.Timeline);
        Assert.Contains("a hand written remark that never parsed", result, StringComparison.Ordinal);
        Assert.DoesNotContain("## Evidence", result, StringComparison.Ordinal);
        Assert.StartsWith(HandEditedFrontmatter, result, StringComparison.Ordinal);
    }

    /// <summary>Appending an event to an older file writes the timeline format with the old entries first and the new event last.</summary>
    [Fact]
    public void AppendEntry_LegacyFile_ConvertsAndAppendsTheEventLast()
    {
        TaskItem before = Parse(LegacyFile);
        ChangeLogEntry added = new(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero), "Emre", "priority: Medium → High");

        string result = TaskFileFormat.AppendEntry(LegacyFile, added);

        TaskItem after = Parse(result);
        Assert.Equal(before.ChangeLog.Append(added), after.ChangeLog);
        Assert.Equal(before.Evidence, after.Evidence);
        Assert.Equal(before.Description, after.Description);
        Assert.EndsWith("- 2026-10-09T08:00:00Z | Emre | event | priority: Medium → High\n", result, StringComparison.Ordinal);
        Assert.DoesNotContain("## Change log", result, StringComparison.Ordinal);
    }

    /// <summary>Appending a comment to an older file writes the timeline format with the old entries first and the comment last.</summary>
    [Fact]
    public void AppendTimelineEntry_LegacyFile_ConvertsAndAppendsTheCommentLast()
    {
        TaskItem before = Parse(LegacyFile);
        TimelineEntry comment = new(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero), "Emre", TimelineEntryKind.Comment, "Looks good.\nShip it.");

        string result = TaskFileFormat.AppendTimelineEntry(LegacyFile, comment);

        TaskItem after = Parse(result);
        Assert.Equal(before.Comments.Append(comment), after.Comments);
        Assert.Equal(before.ChangeLog, after.ChangeLog);
        Assert.Equal(comment, after.Timeline[^1]);
        Assert.EndsWith(@"- 2026-10-09T08:00:00Z | Emre | comment | Looks good.\nShip it." + "\n", result, StringComparison.Ordinal);
    }
}
