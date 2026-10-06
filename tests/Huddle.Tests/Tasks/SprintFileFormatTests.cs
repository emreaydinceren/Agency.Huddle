using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="SprintFileFormat"/>: the pure parse, append, remove and name rules of <c>sprints.md</c> (Sprints spec §6).</summary>
public sealed class SprintFileFormatTests
{
    private const string ExampleText = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    private static string Lines(params string[] lines) => string.Join('\n', lines);

    /// <summary>The §6 example parses to its two Sprints, oldest first, with no problems.</summary>
    [Fact]
    public void ParseFile_SpecExample_ReadsBothSprints()
    {
        SprintFile file = SprintFileFormat.ParseFile(ExampleText);

        Assert.Equal(2, file.Sprints.Count);
        Assert.Equal(new Sprint(new SprintId(1), "Sprint 1"), file.Sprints[0]);
        Assert.Equal(new Sprint(new SprintId(2), "Auth hardening"), file.Sprints[1]);
        Assert.Empty(file.Problems);
    }

    /// <summary>Headings, blank lines, prose and a half-typed line are ignored without a problem.</summary>
    [Fact]
    public void ParseFile_OtherLines_AreIgnored()
    {
        string text = Lines(
            "# Sprints",
            "",
            "Some notes about the plan.",
            "- S1 | Sprint 1",
            "- a plain bullet",
            "- S2",
            "- S3 |Name without space",
            "-S4 | no space after dash",
            "- S5 | ",
            "  - S6 | indented",
            "");

        SprintFile file = SprintFileFormat.ParseFile(text);

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal("Sprint 1", sprint.Name);
        Assert.Empty(file.Problems);
    }

    /// <summary>The id is case-insensitive and the name is everything after the first separator, trimmed.</summary>
    [Fact]
    public void ParseFile_LowerCaseIdAndNameWithSeparator_Parse()
    {
        SprintFile file = SprintFileFormat.ParseFile("- s02 |   Plan | build | ship  \n");

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal(new SprintId(2), sprint.Id);
        Assert.Equal("Plan | build | ship", sprint.Name);
    }

    /// <summary>Sprints come back ordered by number, whatever the order of the lines.</summary>
    [Fact]
    public void ParseFile_LinesOutOfOrder_SortedByNumber()
    {
        SprintFile file = SprintFileFormat.ParseFile(Lines("- S10 | Ten", "- S2 | Two", "- S1 | One", ""));

        Assert.Equal(["S1", "S2", "S10"], file.Sprints.Select(s => s.Id.ToString()));
    }

    /// <summary>A repeated id is a problem on the later line, and the first line wins.</summary>
    [Fact]
    public void ParseFile_DuplicateId_FirstWins()
    {
        string text = Lines("# Sprints", "", "- S1 | First", "- S2 | Other", "- S1 | Second", "");

        SprintFile file = SprintFileFormat.ParseFile(text);

        Assert.Equal(["First", "Other"], file.Sprints.Select(s => s.Name));
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(new SprintProblem(5, "Line 5: duplicate id S1; the first one is used."), problem);
    }

    /// <summary>A repeated name, ignoring case, is a problem on the later line, and the first line wins.</summary>
    [Fact]
    public void ParseFile_DuplicateNameIgnoringCase_FirstWins()
    {
        string text = Lines("- S1 | Auth hardening", "- S2 | AUTH HARDENING", "");

        SprintFile file = SprintFileFormat.ParseFile(text);

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal(new SprintId(1), sprint.Id);
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(new SprintProblem(2, "Line 2: duplicate name 'AUTH HARDENING'; ignored."), problem);
    }

    /// <summary>A name that fails a §6.1 rule is a problem carrying the rule's text, and the line is not a Sprint.</summary>
    [Theory]
    [InlineData("current", "A sprint name can't be 'current' or 'none'.")]
    [InlineData("None", "A sprint name can't be 'current' or 'none'.")]
    [InlineData("S3", "A sprint name can't look like an id (S3).")]
    [InlineData("   ", "A sprint name must be 1-60 characters on one line.")]
    public void ParseFile_InvalidName_ReportsProblem(string name, string expectedReason)
    {
        string text = Lines("- S1 | Fine", $"- S2 | {name}", "");

        SprintFile file = SprintFileFormat.ParseFile(text);

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal("Fine", sprint.Name);
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(new SprintProblem(2, $"Line 2: the name is not valid ({expectedReason}); ignored."), problem);
    }

    /// <summary>A name over 60 characters is a problem, and exactly 60 is accepted.</summary>
    [Fact]
    public void ParseFile_NameLength_SixtyIsTheLimit()
    {
        string sixty = new('a', 60);
        string sixtyOne = new('b', 61);

        SprintFile file = SprintFileFormat.ParseFile(Lines($"- S1 | {sixty}", $"- S2 | {sixtyOne}", ""));

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal(sixty, sprint.Name);
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(2, problem.Line);
        Assert.StartsWith("Line 2: the name is not valid (", problem.Text, StringComparison.Ordinal);
    }

    /// <summary>An id of zero looks like a Sprint line but is not valid, so it is reported and skipped.</summary>
    [Fact]
    public void ParseFile_ZeroId_ReportsProblem()
    {
        SprintFile file = SprintFileFormat.ParseFile(Lines("- S0 | Zero", "- S1 | One", ""));

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal("One", sprint.Name);
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(new SprintProblem(1, "Line 1: the id 'S0' is not valid; ignored."), problem);
    }

    /// <summary>A line whose name is invalid does not claim its id, so a later line with that id is the Sprint.</summary>
    [Fact]
    public void ParseFile_InvalidLineDoesNotClaimItsId()
    {
        SprintFile file = SprintFileFormat.ParseFile(Lines("- S1 | current", "- S1 | Real", ""));

        Sprint sprint = Assert.Single(file.Sprints);
        Assert.Equal("Real", sprint.Name);
        SprintProblem problem = Assert.Single(file.Problems);
        Assert.Equal(1, problem.Line);
    }

    /// <summary>CRLF input parses to the same Sprints as LF input, and line numbers are unchanged.</summary>
    [Fact]
    public void ParseFile_Crlf_SameResultAsLf()
    {
        string lf = Lines("# Sprints", "", "- S1 | One", "- S1 | Dup", "- S2 | Two", "");
        string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        SprintFile fromLf = SprintFileFormat.ParseFile(lf);
        SprintFile fromCrlf = SprintFileFormat.ParseFile(crlf);

        Assert.Equal(fromLf.Sprints, fromCrlf.Sprints);
        Assert.Equal(fromLf.Problems, fromCrlf.Problems);
        Assert.Equal(["One", "Two"], fromCrlf.Sprints.Select(s => s.Name));
        Assert.Equal(4, Assert.Single(fromCrlf.Problems).Line);
    }

    /// <summary>An empty file has no Sprints and no problems.</summary>
    [Fact]
    public void ParseFile_Empty_NothingFound()
    {
        SprintFile file = SprintFileFormat.ParseFile("");

        Assert.Empty(file.Sprints);
        Assert.Empty(file.Problems);
    }

    /// <summary>A missing file starts as a heading, a blank line and the Sprint line.</summary>
    [Fact]
    public void AppendLine_MissingFile_CreatesHeadingAndLine()
    {
        string text = SprintFileFormat.AppendLine(null, new SprintId(1), "Sprint 1");

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", text);
    }

    /// <summary>A file that ends in a line ending gets the new line directly after it, and nothing before it changes.</summary>
    [Fact]
    public void AppendLine_TrailingLineEnding_AppendsOnly()
    {
        string text = SprintFileFormat.AppendLine(ExampleText, new SprintId(3), "Release");

        Assert.Equal(ExampleText + "- S3 | Release\n", text);
    }

    /// <summary>A file whose last line has no line ending gets one added first.</summary>
    [Fact]
    public void AppendLine_NoTrailingLineEnding_AddsOneFirst()
    {
        string existing = "# Sprints\n\n- S1 | Sprint 1";

        string text = SprintFileFormat.AppendLine(existing, new SprintId(2), "Two");

        Assert.Equal(existing + "\n- S2 | Two\n", text);
    }

    /// <summary>A CRLF file is appended to with CRLF, including the line ending added after an unterminated last line.</summary>
    [Theory]
    [InlineData("# Sprints\r\n\r\n- S1 | Sprint 1\r\n")]
    [InlineData("# Sprints\r\n\r\n- S1 | Sprint 1")]
    public void AppendLine_CrlfFile_UsesCrlf(string existing)
    {
        string text = SprintFileFormat.AppendLine(existing, new SprintId(2), "Two");

        Assert.Equal("# Sprints\r\n\r\n- S1 | Sprint 1\r\n- S2 | Two\r\n", text);
    }

    /// <summary>The appended name is trimmed, and the result parses back to the new Sprint.</summary>
    [Fact]
    public void AppendLine_NameIsTrimmed_AndParsesBack()
    {
        string text = SprintFileFormat.AppendLine(ExampleText, new SprintId(3), "  Release  ");

        Assert.EndsWith("- S3 | Release\n", text, StringComparison.Ordinal);
        Assert.Equal("Release", SprintFileFormat.ParseFile(text).Sprints[^1].Name);
    }

    /// <summary>Removing a middle line leaves every other byte alone.</summary>
    [Fact]
    public void RemoveLine_MiddleLine_OthersByteIdentical()
    {
        string text = "# Sprints\n\nnotes\n- S1 | One\n- S2 | Two\n\n- S3 | Three\n";

        string result = SprintFileFormat.RemoveLine(text, new SprintId(2));

        Assert.Equal("# Sprints\n\nnotes\n- S1 | One\n\n- S3 | Three\n", result);
    }

    /// <summary>Removing the first Sprint line leaves the heading and the rest alone.</summary>
    [Fact]
    public void RemoveLine_FirstSprintLine_OthersByteIdentical()
    {
        string result = SprintFileFormat.RemoveLine(ExampleText, new SprintId(1));

        Assert.Equal("# Sprints\n\n- S2 | Auth hardening\n", result);
    }

    /// <summary>Removing the last line, which ends in a line ending, removes just that line.</summary>
    [Fact]
    public void RemoveLine_LastLineWithLineEnding_RemovesLine()
    {
        string result = SprintFileFormat.RemoveLine(ExampleText, new SprintId(2));

        Assert.Equal("# Sprints\n\n- S1 | Sprint 1\n", result);
    }

    /// <summary>Removing a last line with no line ending also takes the line ending before it, so no stray blank line remains.</summary>
    [Theory]
    [InlineData("# Sprints\n\n- S1 | One\n- S2 | Two", "# Sprints\n\n- S1 | One")]
    [InlineData("# Sprints\r\n\r\n- S1 | One\r\n- S2 | Two", "# Sprints\r\n\r\n- S1 | One")]
    public void RemoveLine_LastLineWithoutLineEnding_TakesPrecedingLineEnding(string text, string expected)
    {
        string result = SprintFileFormat.RemoveLine(text, new SprintId(2));

        Assert.Equal(expected, result);
    }

    /// <summary>A CRLF file keeps every other CRLF, and the removed line goes with its own CRLF.</summary>
    [Fact]
    public void RemoveLine_CrlfFile_RemovesWithItsCrlf()
    {
        string text = "# Sprints\r\n\r\n- S1 | One\r\n- S2 | Two\r\n- S3 | Three\r\n";

        string result = SprintFileFormat.RemoveLine(text, new SprintId(2));

        Assert.Equal("# Sprints\r\n\r\n- S1 | One\r\n- S3 | Three\r\n", result);
    }

    /// <summary>Removing the only line of a file gives an empty text rather than failing.</summary>
    [Fact]
    public void RemoveLine_OnlyLineWithoutLineEnding_GivesEmptyText()
    {
        string result = SprintFileFormat.RemoveLine("- S1 | One", new SprintId(1));

        Assert.Equal("", result);
    }

    /// <summary>An id that is not in the file leaves the text exactly as it was.</summary>
    [Fact]
    public void RemoveLine_UnknownId_TextUnchanged()
    {
        string result = SprintFileFormat.RemoveLine(ExampleText, new SprintId(9));

        Assert.Equal(ExampleText, result);
    }

    /// <summary>Only the line that is the Sprint goes: an invalid-name line and a later repeat of the id stay.</summary>
    [Fact]
    public void RemoveLine_RepeatedId_RemovesOnlyTheLineThatIsTheSprint()
    {
        string text = "- S1 | current\n- S1 | Real\n- S1 | Again\n";

        string result = SprintFileFormat.RemoveLine(text, new SprintId(1));

        Assert.Equal("- S1 | current\n- S1 | Again\n", result);
    }

    /// <summary>A valid name passes and is judged after trimming.</summary>
    [Theory]
    [InlineData("Sprint 1")]
    [InlineData("  Auth hardening  ")]
    [InlineData("x")]
    [InlineData("S0")]
    [InlineData("Sprint | with bar")]
    public void ValidateName_Valid_ReturnsNull(string name)
    {
        string? problem = SprintFileFormat.ValidateName(name);

        Assert.Null(problem);
    }

    /// <summary>A name must be 1-60 characters on one line, after trimming.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("line one\nline two")]
    [InlineData("line one\rline two")]
    public void ValidateName_EmptyOrMultiLine_ReturnsLengthProblem(string name)
    {
        string? problem = SprintFileFormat.ValidateName(name);

        Assert.Equal("A sprint name must be 1-60 characters on one line.", problem);
    }

    /// <summary>Sixty characters pass, sixty-one fail, and a null name fails the same way.</summary>
    [Fact]
    public void ValidateName_LengthBoundary()
    {
        Assert.Null(SprintFileFormat.ValidateName(new string('a', 60)));
        Assert.Null(SprintFileFormat.ValidateName("  " + new string('a', 60) + "  "));
        Assert.Equal("A sprint name must be 1-60 characters on one line.", SprintFileFormat.ValidateName(new string('a', 61)));
        Assert.Equal("A sprint name must be 1-60 characters on one line.", SprintFileFormat.ValidateName(null));
    }

    /// <summary>The words an Agent passes for the two special choices are not names, in any case.</summary>
    [Theory]
    [InlineData("current")]
    [InlineData("CURRENT")]
    [InlineData("none")]
    [InlineData(" None ")]
    public void ValidateName_ReservedWord_ReturnsReservedProblem(string name)
    {
        string? problem = SprintFileFormat.ValidateName(name);

        Assert.Equal("A sprint name can't be 'current' or 'none'.", problem);
    }

    /// <summary>A name that parses as a Sprint id is refused, and the text names the id as it prints.</summary>
    [Theory]
    [InlineData("S3", "S3")]
    [InlineData("s03", "S3")]
    [InlineData(" S12 ", "S12")]
    public void ValidateName_LooksLikeAnId_ReturnsIdProblem(string name, string printed)
    {
        string? problem = SprintFileFormat.ValidateName(name);

        Assert.Equal($"A sprint name can't look like an id ({printed}).", problem);
    }

    /// <summary>The next number is one above the higher of the file's highest and the highest a Task references.</summary>
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(2, 0, 3)]
    [InlineData(0, 4, 5)]
    [InlineData(3, 7, 8)]
    [InlineData(7, 3, 8)]
    public void NextNumber_IsOneAboveTheHigherOfTheTwo(int highestInFile, int highestReferenced, int expected)
    {
        int next = SprintFileFormat.NextNumber(highestInFile, highestReferenced);

        Assert.Equal(expected, next);
    }

    /// <summary>A Task that still references a number above the file's highest keeps that number from being reused.</summary>
    [Fact]
    public void NextNumber_TaskReferenceAboveFile_IsNotReused()
    {
        SprintFile file = SprintFileFormat.ParseFile(ExampleText);
        int highestInFile = file.Sprints.Max(s => s.Id.Number);

        int next = SprintFileFormat.NextNumber(highestInFile, highestReferenced: 5);

        Assert.Equal(6, next);
    }
}
