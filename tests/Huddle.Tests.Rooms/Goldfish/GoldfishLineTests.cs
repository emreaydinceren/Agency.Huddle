using Agency.Huddle.Contracts;
using GoldfishLine = Agency.Huddle.App.Goldfish.GoldfishLine;

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
}
