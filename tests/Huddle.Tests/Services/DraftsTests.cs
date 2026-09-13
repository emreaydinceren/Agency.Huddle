using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for <see cref="Drafts"/>, the in-memory store of a Turn's text while it streams in.</summary>
public sealed class DraftsTests
{
    /// <summary>
    /// A Group Room where two Agents were both Mentioned can have two Turns streaming into it at
    /// once. Because <see cref="Drafts"/> is keyed by Message id rather than Room id, both Drafts
    /// must survive side by side.
    /// </summary>
    [Fact]
    public void TwoAgentsStreamingIntoOneRoom_KeepBothDrafts()
    {
        var drafts = new Drafts();

        drafts.Append("m1", "room1", "agentA", "Ada", "Hello");
        drafts.Append("m2", "room1", "agentB", "Bea", "World");

        var forRoom = drafts.ForRoom("room1");

        Assert.Equal(2, forRoom.Count);
        Assert.Contains(forRoom, d => d.MessageId == "m1" && d.Text == "Hello");
        Assert.Contains(forRoom, d => d.MessageId == "m2" && d.Text == "World");
    }

    /// <summary>Clearing one Agent's Drafts must not disturb a different Agent's Draft in the same Room.</summary>
    [Fact]
    public void ClearForAgent_LeavesTheOtherAgentsDraftAlone()
    {
        var drafts = new Drafts();
        drafts.Append("m1", "room1", "agentA", "Ada", "Hello");
        drafts.Append("m2", "room1", "agentB", "Bea", "World");

        drafts.ClearForAgent("agentA");

        var forRoom = drafts.ForRoom("room1");
        Assert.Single(forRoom);
        Assert.Equal("m2", forRoom[0].MessageId);
    }

    /// <summary>A completed Turn's Draft is removed and no longer appears in its Room.</summary>
    [Fact]
    public void Complete_RemovesTheDraft()
    {
        var drafts = new Drafts();
        drafts.Append("m1", "room1", "agentA", "Ada", "Hello");

        drafts.Complete("m1");

        Assert.Empty(drafts.ForRoom("room1"));
    }

    /// <summary>
    /// Both a Turn's own terminator and a successful post call <see cref="Drafts.Complete"/>, so it
    /// must tolerate a Message id that never had a Draft.
    /// </summary>
    [Fact]
    public void Complete_ForAnUnknownMessageId_IsHarmless()
    {
        var drafts = new Drafts();

        var exception = Record.Exception(() => drafts.Complete("no-such-message"));

        Assert.Null(exception);
    }

    /// <summary>
    /// <see cref="Drafts.ForRoom"/> returns an immutable snapshot: appending more text after taking it
    /// must not retroactively change the list already handed out.
    /// </summary>
    [Fact]
    public void ForRoom_ReturnsASnapshot_NotALiveReference()
    {
        var drafts = new Drafts();
        drafts.Append("m1", "room1", "agentA", "Ada", "Hello");

        var snapshot = drafts.ForRoom("room1");
        drafts.Append("m1", "room1", "agentA", "Ada", " World");

        Assert.Equal("Hello", snapshot[0].Text);
    }

    /// <summary>
    /// A Draft's text stops growing once it reaches <see cref="Drafts.MaxDraftTextLength"/>, so a
    /// looping model or an Agent that never sends a terminator cannot grow this Singleton's memory
    /// without bound.
    /// </summary>
    [Fact]
    public void ADraftStopsGrowingAtTheCap()
    {
        var drafts = new Drafts();
        var chunk = new string('a', 1024);
        var chunksNeededToExceedCap = (Drafts.MaxDraftTextLength / chunk.Length) + 2;

        for (var i = 0; i < chunksNeededToExceedCap; i++)
        {
            drafts.Append("m1", "room1", "agentA", "Ada", chunk);
        }

        var draft = Assert.Single(drafts.ForRoom("room1"));
        Assert.Equal(Drafts.MaxDraftTextLength, draft.Text.Length);
    }

    /// <summary>A new tool activity replaces the previous one rather than accumulating alongside it.</summary>
    [Fact]
    public void Activity_ReplacesThePreviousActivity()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading file.cs", ToolActivityStatus.InProgress);

        drafts.Activity("m1", "tc-2", "room1", "agentA", "Ada", "Writing file.cs", ToolActivityStatus.Pending);

        var draft = Assert.Single(drafts.ForRoom("room1"));
        Assert.Equal("Writing file.cs", draft.ToolTitle);
        Assert.Equal(ToolActivityStatus.Pending, draft.ToolStatus);
    }

    /// <summary>A Draft may receive its first tool activity before any text has arrived.</summary>
    [Fact]
    public void Activity_BeforeAnyAppend_CreatesTheDraft()
    {
        var drafts = new Drafts();

        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading file.cs", ToolActivityStatus.InProgress);

        var draft = Assert.Single(drafts.ForRoom("room1"));
        Assert.Equal(string.Empty, draft.Text);
        Assert.Equal("Reading file.cs", draft.ToolTitle);
    }
}
