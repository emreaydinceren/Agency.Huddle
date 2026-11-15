using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="EvidenceLine"/>: parsing and formatting Evidence lines under a Task's "## Evidence" section.</summary>
public sealed class EvidenceLineTests
{
    private const string SpecExample = "2026-10-08T17:04Z | Maya | dotnet test | exit 0 | logs/tool-calls/Maya/2026-10-08.jsonl#L212";

    /// <summary>Parses a line that is expected to be valid and returns it, failing the test when it is not.</summary>
    private static EvidenceLine Parsed(string text)
    {
        Assert.True(EvidenceLine.TryParse(text, out EvidenceLine? evidence));
        Assert.NotNull(evidence);
        return evidence;
    }

    /// <summary>The Spec example parses into every field and formats back to the same bytes.</summary>
    [Fact]
    public void TryParse_SpecExample_ParsesEveryFieldAndFormatsIdentically()
    {
        EvidenceLine line = Parsed(SpecExample);

        Assert.Equal(new DateTimeOffset(2026, 10, 8, 17, 4, 0, TimeSpan.Zero), line.Time);
        Assert.Equal("Maya", line.Author);
        Assert.Equal("dotnet test", line.Command);
        Assert.Equal(0, line.ExitCode);
        Assert.Equal("logs/tool-calls/Maya/2026-10-08.jsonl", line.LogPath);
        Assert.Equal(212, line.LogLine);
        Assert.Equal(SpecExample, line.Format());
    }

    /// <summary>A note is a field of its own: the line round-trips, the command stays exact, and the note may come with or without a log reference.</summary>
    [Theory]
    [InlineData("2026-10-08T17:04Z | Maya | dotnet test | exit 0 | logs/a.jsonl#L2 | note: 14 tests, green")]
    [InlineData("2026-10-08T17:04Z | Maya | dotnet test | exit 0 | note: 14 tests, green")]
    public void TryParse_LineWithANote_KeepsTheCommandExact(string text)
    {
        EvidenceLine line = Parsed(text);

        Assert.Equal("dotnet test", line.Command);
        Assert.Equal(0, line.ExitCode);
        Assert.Equal("14 tests, green", line.Note);
        Assert.Equal(text, line.Format());
    }

    /// <summary>A command equal to the wanted one, or followed by a space and a bracketed note (the form written before #340), runs it.</summary>
    [Theory]
    [InlineData("dotnet test")]
    [InlineData("  dotnet test ")]
    [InlineData("dotnet test (repo root C:\\HuddleZero\\repos\\bakery-quote)")]
    [InlineData("dotnet test (note)")]
    public void RunsCommand_ExactOrWithBracketedNote_ReturnsTrue(string stored)
    {
        EvidenceLine line = new(default, "Maya", stored, 0, null, null, string.Empty);

        Assert.True(line.RunsCommand("dotnet test"));
    }

    /// <summary>A longer flag form, a different word, a bracket with no space, or a missing closing bracket does not run the command.</summary>
    [Theory]
    [InlineData("dotnet test --filter X")]
    [InlineData("dotnet testing (x)")]
    [InlineData("dotnet test(x)")]
    [InlineData("dotnet test (x")]
    [InlineData("dotnet build")]
    [InlineData("")]
    public void RunsCommand_OtherForms_ReturnsFalse(string stored)
    {
        EvidenceLine line = new(default, "Maya", stored, 0, null, null, string.Empty);

        Assert.False(line.RunsCommand("dotnet test"));
    }

    /// <summary>A failing check records its non-zero exit code.</summary>
    [Fact]
    public void TryParse_FailingTest_RecordsExitOne()
    {
        EvidenceLine line = Parsed("2026-10-08T17:04Z | Maya | dotnet test | exit 1");

        Assert.Equal(1, line.ExitCode);
        Assert.Equal("dotnet test", line.Command);
        Assert.Equal("2026-10-08T17:04Z | Maya | dotnet test | exit 1", line.Format());
    }

    /// <summary>A line without a log reference leaves LogPath and LogLine null and formats without one.</summary>
    [Fact]
    public void TryParse_NoLogReference_LeavesLogFieldsNull()
    {
        EvidenceLine line = Parsed("2026-10-08T17:04Z | Maya | dotnet test | exit 0");

        Assert.Null(line.LogPath);
        Assert.Null(line.LogLine);
        Assert.Equal("2026-10-08T17:04Z | Maya | dotnet test | exit 0", line.Format());
    }

    /// <summary>A free-text note with no pipes parses with RawText set and an empty Command, and formats as the note itself.</summary>
    [Fact]
    public void TryParse_PlainNote_KeepsTextAsRawText()
    {
        const string note = "Reviewed the migration plan with Ana";

        EvidenceLine line = Parsed(note);

        Assert.Equal(note, line.RawText);
        Assert.Equal(string.Empty, line.Command);
        Assert.Null(line.ExitCode);
        Assert.Equal(note, line.Format());
    }

    /// <summary>A time with seconds or a missing Z is malformed and is rejected.</summary>
    [Theory]
    [InlineData("2026-10-08T17:04:05Z | Maya | dotnet test | exit 0")]
    [InlineData("2026-10-08T17:04 | Maya | dotnet test | exit 0")]
    [InlineData("2026-10-8 17:04Z | Maya | dotnet test | exit 0")]
    public void TryParse_MalformedTime_IsRejected(string text)
    {
        bool parsed = EvidenceLine.TryParse(text, out EvidenceLine? evidence);

        Assert.False(parsed);
        Assert.Null(evidence);
    }

    /// <summary>ParseSection reads only the lines under "## Evidence" and keeps unparseable lines there as notes.</summary>
    [Fact]
    public void ParseSection_IgnoresOtherSections_AndKeepsNotes()
    {
        const string markdown = """
            # Task body

            ## Change log
            2026-10-08T16:00Z | Maya | not evidence | exit 9 | logs/x.jsonl#L1

            ## Evidence
            2026-10-08T17:04Z | Maya | dotnet test | exit 0 | logs/tool-calls/Maya/2026-10-08.jsonl#L212
            Ana checked the diff by hand
            2026-10-8 17:30 | Ana | dotnet test | exit 1

            2026-10-08T17:30Z | Ana | dotnet build | exit 0

            ## Notes
            2026-10-08T18:00Z | Ana | dotnet format | exit 0
            """;

        IReadOnlyList<EvidenceLine> lines = EvidenceLine.ParseSection(markdown);

        Assert.Equal(4, lines.Count);
        Assert.Equal("dotnet test", lines[0].Command);
        Assert.Equal(212, lines[0].LogLine);
        Assert.Equal("Ana checked the diff by hand", lines[1].RawText);
        Assert.Equal(string.Empty, lines[1].Command);
        Assert.Equal("2026-10-8 17:30 | Ana | dotnet test | exit 1", lines[2].RawText);
        Assert.Equal(string.Empty, lines[2].Command);
        Assert.Equal("dotnet build", lines[3].Command);
        Assert.Equal(0, lines[3].ExitCode);
    }

    /// <summary>A list-item structured line and a list-item note parse like their plain forms, and the marker is not part of RawText.</summary>
    [Theory]
    [InlineData("- ")]
    [InlineData("* ")]
    [InlineData("+ ")]
    public void ParseSection_ListItemLines_ParseLikePlainLines(string marker)
    {
        string markdown = "## Evidence\n" + marker + SpecExample + "\n" + marker + "Ana checked the diff\n";

        IReadOnlyList<EvidenceLine> lines = EvidenceLine.ParseSection(markdown);

        Assert.Equal(2, lines.Count);
        Assert.Equal(Parsed(SpecExample), lines[0]);
        Assert.Equal("Ana checked the diff", lines[1].RawText);
        Assert.Equal(string.Empty, lines[1].Command);
    }

    /// <summary>A section with old plain lines followed by new list items parses every line, in order.</summary>
    [Fact]
    public void ParseSection_MixedPlainAndListItems_ParsesAllInOrder()
    {
        const string markdown = "## Evidence\n2026-10-08T17:04Z | Maya | dotnet build | exit 0\n- 2026-10-08T17:05Z | Maya | dotnet test | exit 1\n- a note\n";

        IReadOnlyList<EvidenceLine> lines = EvidenceLine.ParseSection(markdown);

        Assert.Equal(["dotnet build", "dotnet test", string.Empty], lines.Select(static line => line.Command));
        Assert.Equal(1, lines[1].ExitCode);
        Assert.Equal("a note", lines[2].RawText);
    }

    /// <summary>ParseSection returns an empty list when the document has no Evidence section.</summary>
    [Fact]
    public void ParseSection_NoEvidenceSection_ReturnsEmpty()
    {
        IReadOnlyList<EvidenceLine> lines = EvidenceLine.ParseSection("# Task\n\n## Change log\n2026-10-08T16:00Z | Maya | dotnet test | exit 0\n");

        Assert.Empty(lines);
    }
}
