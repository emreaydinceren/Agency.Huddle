namespace Agency.Huddle.Tests.Memory;

using System.Text;
using Agency.Huddle.App.Memory;

/// <summary>
/// Pins <see cref="ReportedFile"/>, the one implementation of the <c>reported.md</c> line rule (Memory Tidy spec §6.1, §6.3):
/// the age rule, the drop, the making of room at the size cap and the making of a line from the Agent's text.
/// </summary>
public sealed class ReportedFileTests
{
    private const string Head = "---\ndescription: What I have already told the Human, by date and Room. Read before reporting so nothing is said twice.\n---\n\n";

    private static readonly DateOnly Today = new(2026, 11, 16);

    /// <summary>A line exactly KeepDays old is kept and one a day older is dropped: the rule is "more than KeepDays".</summary>
    [Fact]
    public void PruneByAge_BoundaryDay_IsKeptAndTheDayBeforeIsDropped()
    {
        string text = Head + "- 2026-11-01 · Nova · fifteen days old\n- 2026-11-02 · Nova · fourteen days old\n";

        (string pruned, int dropped) = ReportedFile.PruneByAge(text, Today, 14);

        Assert.Equal(1, dropped);
        Assert.Equal(Head + "- 2026-11-02 · Nova · fourteen days old\n", pruned);
    }

    /// <summary>A line Huddle cannot read as a dated line is kept however old the rest of the file is.</summary>
    [Fact]
    public void PruneByAge_UnreadableLines_AreKept()
    {
        string text = Head + "Note the Human wrote.\n- yesterday · Nova · no date\n- 2026-13-40 · Nova · not a date\n- 2026-11-01 Nova · no separator\n- 2020-01-01 · Nova · ancient\n";

        (string pruned, int dropped) = ReportedFile.PruneByAge(text, Today, 14);

        Assert.Equal(1, dropped);
        Assert.Equal(Head + "Note the Human wrote.\n- yesterday · Nova · no date\n- 2026-13-40 · Nova · not a date\n- 2026-11-01 Nova · no separator\n", pruned);
    }

    /// <summary>A date after today is read as a date and is never dropped by age.</summary>
    [Fact]
    public void PruneByAge_FutureDate_IsReadAndKept()
    {
        string text = Head + "- 2027-01-01 · Nova · from the future\n";

        Assert.True(ReportedFile.TryReadDate("- 2027-01-01 · Nova · from the future", out DateOnly date));
        (string pruned, int dropped) = ReportedFile.PruneByAge(text, Today, 14);

        Assert.Equal(new DateOnly(2027, 1, 1), date);
        Assert.Equal(0, dropped);
        Assert.Equal(text, pruned);
    }

    /// <summary>Zero or a negative KeepDays turns dropping off.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PruneByAge_KeepDaysZeroOrLess_DropsNothing(int keepDays)
    {
        string text = Head + "- 2020-01-01 · Nova · ancient\n";

        (string pruned, int dropped) = ReportedFile.PruneByAge(text, Today, keepDays);

        Assert.Equal(0, dropped);
        Assert.Equal(text, pruned);
    }

    /// <summary>The frontmatter stays byte for byte and a CRLF file stays CRLF, including the lines kept.</summary>
    [Fact]
    public void PruneByAge_CrlfFile_KeepsFrontmatterAndLineEndings()
    {
        string head = Head.Replace("\n", "\r\n", StringComparison.Ordinal);
        string text = head + "- 2026-10-01 · Nova · old\r\n- 2026-11-15 · Nova · recent\r\n";

        (string pruned, int dropped) = ReportedFile.PruneByAge(text, Today, 14);

        Assert.Equal(1, dropped);
        Assert.Equal(head + "- 2026-11-15 · Nova · recent\r\n", pruned);
    }

    /// <summary>A file with no frontmatter is read from its first line.</summary>
    [Fact]
    public void PruneByAge_NoFrontmatter_ReadsEveryLine()
    {
        (string pruned, int dropped) = ReportedFile.PruneByAge("- 2020-01-01 · Nova · ancient\n- 2026-11-16 · Nova · now\n", Today, 14);

        Assert.Equal(1, dropped);
        Assert.Equal("- 2026-11-16 · Nova · now\n", pruned);
    }

    /// <summary>When the new line does not fit, the oldest dated lines go first, one at a time, until it does; an unreadable line stays.</summary>
    [Fact]
    public void Append_OverTheCap_DropsTheOldestDatedLinesFirst()
    {
        const string A = "- 2026-11-10 · Nova · a\n";
        const string B = "- 2026-11-12 · Nova · b\n";
        const string Unreadable = "Human's own note\n";
        const string C = "- 2026-11-09 · Nova · c\n";
        const string New = "- 2026-11-16 · Nova · new";
        string text = Head + A + B + Unreadable + C;

        ReportedAppend dropsOne = ReportedFile.Append(text, New, Today, 14, Encoding.UTF8.GetByteCount(Head + A + B + Unreadable + New + "\n"));
        ReportedAppend dropsTwo = ReportedFile.Append(text, New, Today, 14, Encoding.UTF8.GetByteCount(Head + B + Unreadable + New + "\n"));

        Assert.Equal(Head + A + B + Unreadable + New + "\n", dropsOne.Text);
        Assert.Equal(1, dropsOne.DroppedForRoom);
        Assert.Equal(Head + B + Unreadable + New + "\n", dropsTwo.Text);
        Assert.Equal(2, dropsTwo.DroppedForRoom);
        Assert.Equal(0, dropsTwo.DroppedByAge);
    }

    /// <summary>When only unreadable lines and the frontmatter remain and the new line still does not fit, nothing is produced.</summary>
    [Fact]
    public void Append_NeverDropsAnUnreadableLine_AndRefusesWhenItCannotFit()
    {
        string text = Head + "Human's own note, a long one\n- 2026-11-10 · Nova · a\n";
        int cap = Encoding.UTF8.GetByteCount(Head + "Human's own note, a long one\n") + 5;

        ReportedAppend result = ReportedFile.Append(text, "- 2026-11-16 · Nova · new", Today, 14, cap);

        Assert.Null(result.Text);
        Assert.True(result.Size > cap);
    }

    /// <summary>The age rule runs in the same append, is counted apart from the room made, and a cap of zero means none.</summary>
    [Fact]
    public void Append_DropsOldLinesByAge_AndCountsThemApart()
    {
        string text = Head + "- 2026-10-01 · Nova · old\n- 2026-10-02 · Nova · old\n- 2026-11-15 · Nova · kept\n";

        ReportedAppend result = ReportedFile.Append(text, "- 2026-11-16 · Nova · new", Today, 14, 0);

        Assert.Equal(Head + "- 2026-11-15 · Nova · kept\n- 2026-11-16 · Nova · new\n", result.Text);
        Assert.Equal(2, result.DroppedByAge);
        Assert.Equal(0, result.DroppedForRoom);
    }

    /// <summary>A last line with no break gets one before the new line, in the file's own style.</summary>
    [Fact]
    public void Append_LastLineWithoutBreak_GetsOneInTheFilesStyle()
    {
        string head = Head.Replace("\n", "\r\n", StringComparison.Ordinal);

        ReportedAppend result = ReportedFile.Append(head + "- 2026-11-15 · Nova · kept", "- 2026-11-16 · Nova · new", Today, 14, 0);

        Assert.Equal(head + "- 2026-11-15 · Nova · kept\r\n- 2026-11-16 · Nova · new\r\n", result.Text);
    }

    /// <summary>A leading marker and a leading date with its separator are replaced by today's date and the house separator.</summary>
    [Theory]
    [InlineData("Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("- Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("* Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026-11-01 · Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("- 2026-11-01 · Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("- 2099-01-01 · Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026/11/01 - Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026-11-01: Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026-11-01 | Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026-11-01 Nova and Sam · budget is over", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("- 2026-11-01 — Nova and Sam · budget is over\r\n", "- 2026-11-16 · Nova and Sam · budget is over")]
    [InlineData("2026-11-01T10:00 Nova", "- 2026-11-16 · 2026-11-01T10:00 Nova")]
    public void BuildLine_StripsMarkerAndDate_AndSetsToday(string text, string expected)
    {
        ReportedFile.LineProblem problem = ReportedFile.BuildLine(text, Today, out string line);

        Assert.Equal(ReportedFile.LineProblem.None, problem);
        Assert.Equal(expected, line);
    }

    /// <summary>Several lines are refused, an empty text too; a trailing break alone is not several lines.</summary>
    [Theory]
    [InlineData("one\ntwo", "SeveralLines")]
    [InlineData("one\r\ntwo\r\n", "SeveralLines")]
    [InlineData("one\rtwo", "SeveralLines")]
    [InlineData("", "Empty")]
    [InlineData("- 2026-11-01 · ", "Empty")]
    [InlineData("one\n\n", "None")]
    public void BuildLine_SeveralLinesOrNothing_IsReported(string text, string expected)
    {
        Assert.Equal(expected, ReportedFile.BuildLine(text, Today, out _).ToString());
    }

    /// <summary>The name matches ignoring case and only exactly.</summary>
    [Theory]
    [InlineData("reported.md", true)]
    [InlineData("Reported.MD", true)]
    [InlineData("reported.md.md", false)]
    [InlineData("notes.md", false)]
    public void IsName_IgnoresCase(string file, bool expected)
    {
        Assert.Equal(expected, ReportedFile.IsName(file));
    }
}
