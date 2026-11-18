using Agency.Huddle.Contracts;
using GoldfishLine = Agency.Huddle.App.Goldfish.GoldfishLine;
using GoldfishStamp = Agency.Huddle.App.Goldfish.GoldfishStamp;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins the goldfish wording (spec 6.5, 6.6, 6.7): the five return lines, the result Message text and the not-in-mode reply.</summary>
public sealed class GoldfishLineTests
{
    /// <summary>The five return lines read exactly as the spec table, with the Agent Mentioned.</summary>
    [Theory]
    [InlineData(GoldfishExit.Finished, "@Ruth is back. Goldfish finished: 3 findings, result above.")]
    [InlineData(GoldfishExit.Stuck, "@Ruth is back. Gave up: could not reach the server.")]
    [InlineData(GoldfishExit.CalledBack, "@Ruth is back. Called back by Quinn after 4 minutes. Partial result above.")]
    [InlineData(GoldfishExit.Failed, "@Ruth is back. Didn't finish: the Adapter stopped.")]
    [InlineData(GoldfishExit.Restarted, "@Ruth is back. Interrupted by a restart. Partial result above.")]
    public void ReturnLine_EachExit_ReadsAsTheSpecTable(GoldfishExit exit, string expected)
    {
        string line = GoldfishLine.ReturnLine("Ruth", exit, "Quinn", TimeSpan.FromSeconds(270), isMember: true, findings: 3, firstLine: "could not reach the server");

        Assert.Equal(expected, line);
    }

    /// <summary>An Agent who is no longer a Member gets the line without the @ (E-4).</summary>
    [Fact]
    public void ReturnLine_NotAMember_HasNoAt()
    {
        string line = GoldfishLine.ReturnLine("a-17", GoldfishExit.Failed, null, TimeSpan.Zero, isMember: false);

        Assert.Equal("a-17 is back. Didn't finish: the Adapter stopped.", line);
    }

    /// <summary>Minutes round down, one is singular and zero reads "less than a minute".</summary>
    [Theory]
    [InlineData(0, "less than a minute")]
    [InlineData(59, "less than a minute")]
    [InlineData(60, "1 minute")]
    [InlineData(119, "1 minute")]
    [InlineData(240, "4 minutes")]
    public void ReturnLine_CalledBackElapsed_RoundsDown(int seconds, string expectedSpan)
    {
        string line = GoldfishLine.ReturnLine("Ruth", GoldfishExit.CalledBack, "Quinn", TimeSpan.FromSeconds(seconds), isMember: true);

        Assert.Equal($"@Ruth is back. Called back by Quinn after {expectedSpan}. Partial result above.", line);
    }

    /// <summary>Without a caller name the line drops "by".</summary>
    [Fact]
    public void ReturnLine_CalledBackWithoutCaller_DropsBy()
    {
        string line = GoldfishLine.ReturnLine("Ruth", GoldfishExit.CalledBack, null, TimeSpan.FromMinutes(2), isMember: true);

        Assert.Equal("@Ruth is back. Called back after 2 minutes. Partial result above.", line);
    }

    /// <summary>One finding is singular.</summary>
    [Fact]
    public void ReturnLine_OneFinding_IsSingular()
    {
        string line = GoldfishLine.ReturnLine("Ruth", GoldfishExit.Finished, null, TimeSpan.Zero, isMember: true, findings: 1);

        Assert.Equal("@Ruth is back. Goldfish finished: 1 finding, result above.", line);
    }

    /// <summary>The result Message quotes the brief, then the result, then the findings.</summary>
    [Fact]
    public void ResultText_DoneWithFindings_IsBriefResultFindings()
    {
        string text = GoldfishLine.ResultText("Check the build\nand @Quinn", "All green.", "- one\n- two", partial: false);

        Assert.Equal("Brief: Check the build\nand @Quinn\n\nAll green.\n\n- one\n- two", text);
    }

    /// <summary>Without findings the text ends with the result.</summary>
    [Fact]
    public void ResultText_NoFindings_EndsWithResult()
    {
        string text = GoldfishLine.ResultText("Do it", "Done.", null, partial: false);

        Assert.Equal("Brief: Do it\n\nDone.", text);
    }

    /// <summary>A partial result says so and keeps the last reply.</summary>
    [Fact]
    public void ResultText_PartialWithReply_SaysPartialResult()
    {
        string text = GoldfishLine.ResultText("Do it", "Half way.", null, partial: true);

        Assert.Equal("Brief: Do it\n\nPartial result:\nHalf way.", text);
    }

    /// <summary>With nothing posted the result is "No result was posted.", partial or not.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultText_NoResult_SaysNoResultWasPosted(bool partial)
    {
        string text = GoldfishLine.ResultText("Do it", null, null, partial);

        Assert.Equal("Brief: Do it\n\nNo result was posted.", text);
    }

    /// <summary>The reply for an Agent not in goldfish mode.</summary>
    [Fact]
    public void NotInMode_NamesTheAgent()
    {
        Assert.Equal("Ruth isn't in goldfish mode.", GoldfishLine.NotInMode("Ruth"));
    }

    /// <summary>The start row parses back to its summary line and the brief, which keeps its blank lines, Mentions and Markdown.</summary>
    [Fact]
    public void TryParseStartRow_StartRow_ReturnsSummaryAndBriefVerbatim()
    {
        string brief = "Ask @Kai about **price**\n\nThen list the files.";

        bool parsed = GoldfishLine.TryParseStartRow(GoldfishLine.StartRow("Ruth", brief), out string summary, out string parsedBrief);

        Assert.Equal((true, "Ruth went into goldfish mode. They don't remember this Room.", brief), (parsed, summary, parsedBrief));
    }

    /// <summary>Text that is not a start row (the result row, a return row, ordinary talk) is not parsed as one.</summary>
    [Theory]
    [InlineData("Brief: Look\n\nNo result was posted.")]
    [InlineData("@Ruth is back. Gave up.")]
    [InlineData("Ruth went into goldfish mode.")]
    [InlineData("Ruth went into goldfish mode. They don't remember this Room.")]
    public void TryParseStartRow_OtherText_IsRejected(string text)
    {
        bool parsed = GoldfishLine.TryParseStartRow(text, out string summary, out string brief);

        Assert.Equal((false, string.Empty, string.Empty), (parsed, summary, brief));
    }

    /// <summary>Every return line, with the Agent Mentioned or bare, parses to its text without the leading @.</summary>
    [Theory]
    [InlineData(GoldfishExit.Finished, true)]
    [InlineData(GoldfishExit.Finished, false)]
    [InlineData(GoldfishExit.Stuck, true)]
    [InlineData(GoldfishExit.Stuck, false)]
    [InlineData(GoldfishExit.CalledBack, true)]
    [InlineData(GoldfishExit.CalledBack, false)]
    [InlineData(GoldfishExit.Failed, true)]
    [InlineData(GoldfishExit.Failed, false)]
    [InlineData(GoldfishExit.Restarted, true)]
    [InlineData(GoldfishExit.Restarted, false)]
    public void TryParseReturnRow_EveryReturnLine_ParsesWithoutTheAt(GoldfishExit exit, bool isMember)
    {
        string line = GoldfishLine.ReturnLine("Ruth", exit, "Quinn", TimeSpan.FromMinutes(4), isMember, findings: 2, firstLine: "no luck");

        bool parsed = GoldfishLine.TryParseReturnRow(line, out string summary);

        Assert.Equal((true, line.TrimStart('@')), (parsed, summary));
    }

    /// <summary>The result row, the restart line, a Human sentence with " is back. " and a multi-line text are not return rows.</summary>
    [Theory]
    [InlineData("Brief: Look into it\n\nNo result was posted.")]
    [InlineData("Picked up after a restart.\n@Ruth is back. Called back")]
    [InlineData("@Ruth is back. Hope the trip was fun.")]
    [InlineData("@Ruth is back. Gave up.\nAnd more.")]
    public void TryParseReturnRow_OtherText_IsRejected(string text)
    {
        bool parsed = GoldfishLine.TryParseReturnRow(text, out string summary);

        Assert.Equal((false, string.Empty), (parsed, summary));
    }

    /// <summary>The return detail shows start, end, Turns and, when known, the spend with three decimals and its currency.</summary>
    [Theory]
    [InlineData(3, 0.12, "USD", "Started 08:00, ended 08:12, 3 Turns, spent 0.120 USD")]
    [InlineData(1, 0, "USD", "Started 08:00, ended 08:12, 1 Turn")]
    [InlineData(2, 1.5, null, "Started 08:00, ended 08:12, 2 Turns")]
    public void ReturnDetail_WithAndWithoutSpend_ReadsAsTheTable(int turns, double spend, string? currency, string expected)
    {
        DateTimeOffset started = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

        string detail = GoldfishLine.ReturnDetail(started, started.AddMinutes(12), turns, (decimal)spend, currency, TimeZoneInfo.Utc);

        Assert.Equal(expected, detail);
    }

    /// <summary>The stamp is recognised on a stamped author and on no other, and the tooltip is the settled sentence.</summary>
    [Fact]
    public void GoldfishStamp_IsStampedAndTooltip_ReadAsSettled()
    {
        Assert.Equal(
            (true, false, "Fresh-eyes mode: no memory of this Room, sees only their brief and the files it names."),
            (GoldfishStamp.IsStamped("Ruth · goldfish"), GoldfishStamp.IsStamped("Ruth"), GoldfishStamp.Tooltip));
    }
}
