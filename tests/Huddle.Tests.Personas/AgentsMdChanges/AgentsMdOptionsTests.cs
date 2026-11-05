using Agency.Huddle.App;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins the <c>Team:AgentsMd</c> defaults for the AGENTS.md change watcher, judgement and Helper limits (Spec §6.9).
/// </summary>
public sealed class AgentsMdOptionsTests
{
    /// <summary>A fresh options object carries the Spec §6.9 default for every change-watcher limit.</summary>
    [Fact]
    public void Defaults()
    {
        AgentsMdOptions options = new();

        Assert.Equal(30, options.NoticeWaitSeconds);
        Assert.Equal(60, options.DebounceSeconds);
        Assert.Equal(300, options.MinJudgeSeconds);
        Assert.Equal(10, options.PollSeconds);
        Assert.Equal(200, options.MaxSkipChangedChars);
        Assert.Equal(20, options.MaxRevisionsKept);
        Assert.Equal(5, options.MaxNoticesPerFile);
        Assert.Equal(8000, options.MaxNoticeChars);
        Assert.Equal(4000, options.MaxComparisonChars);
        Assert.Equal(12000, options.MaxHelperComparisonChars);
        Assert.Equal(16000, options.MaxHelperFileChars);
        Assert.Equal(256000, options.MaxLedgerChars);
    }
}
