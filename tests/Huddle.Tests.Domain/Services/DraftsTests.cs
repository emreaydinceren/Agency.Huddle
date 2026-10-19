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

    /// <summary>A second tool call is kept beside the first, so the Room view can show both.</summary>
    [Fact]
    public void Activity_SecondToolCall_KeepsBoth()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading file.cs", ToolActivityStatus.Completed);

        drafts.Activity("m1", "tc-2", "room1", "agentA", "Ada", "Writing file.cs", ToolActivityStatus.Pending);

        var draft = Assert.Single(drafts.ForRoom("room1"));
        Assert.Equal(["tc-1", "tc-2"], draft.ToolCalls.Select(call => call.ToolCallId));
    }

    /// <summary>The title and status a Draft reports are those of its newest tool call.</summary>
    [Fact]
    public void ToolTitleAndStatus_ReflectTheNewestCall()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading file.cs", ToolActivityStatus.InProgress);

        drafts.Activity("m1", "tc-2", "room1", "agentA", "Ada", "Writing file.cs", ToolActivityStatus.Pending);

        var draft = Assert.Single(drafts.ForRoom("room1"));
        Assert.Equal("Writing file.cs", draft.ToolTitle);
        Assert.Equal(ToolActivityStatus.Pending, draft.ToolStatus);
    }

    /// <summary>A Draft with no tool call reports no title and no status.</summary>
    [Fact]
    public void ToolTitleAndStatus_WithoutAnyCall_AreNull()
    {
        var drafts = new Drafts();
        drafts.Append("m1", "room1", "agentA", "Ada", "hi");

        var draft = Assert.Single(drafts.ForRoom("room1"));

        Assert.Null(draft.ToolTitle);
        Assert.Null(draft.ToolStatus);
        Assert.Empty(draft.ToolCalls);
    }

    /// <summary>A seventh call pushes the oldest out, so a Draft never holds more than six rows.</summary>
    [Fact]
    public void Activity_SeventhCall_EvictsTheFirst()
    {
        var drafts = new Drafts();
        for (var i = 1; i <= 7; i++)
        {
            drafts.Activity("m1", $"tc-{i}", "room1", "agentA", "Ada", $"call {i}", ToolActivityStatus.Completed);
        }

        var draft = Assert.Single(drafts.ForRoom("room1"));

        Assert.Equal(["tc-2", "tc-3", "tc-4", "tc-5", "tc-6", "tc-7"], draft.ToolCalls.Select(call => call.ToolCallId));
    }

    /// <summary>An update that omits the title keeps the title the call already had.</summary>
    [Fact]
    public void Activity_UpdateWithNullTitle_KeepsTheTitle()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Edit notes.md", ToolActivityStatus.InProgress);

        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        var call = Assert.Single(Assert.Single(drafts.ForRoom("room1")).ToolCalls);
        Assert.Equal("Edit notes.md", call.Title);
        Assert.Equal(ToolActivityStatus.Completed, call.Status);
    }

    /// <summary>An update that omits the Edit keeps the preview the call already had, because ACP reads an omitted field as unchanged.</summary>
    [Fact]
    public void Activity_UpdateWithNullEdit_KeepsTheEdit()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Edit a", ToolActivityStatus.InProgress, "a.txt", 4, new EditChange("1", "2"));

        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", null, ToolActivityStatus.Completed);

        var call = Assert.Single(Assert.Single(drafts.ForRoom("room1")).ToolCalls);
        Assert.Equal(new EditChange("1", "2"), call.Edit);
        Assert.Equal("a.txt", call.Path);
        Assert.Equal(4, call.Line);
    }

    /// <summary>A new Edit replaces the old one, so the row shows the latest change.</summary>
    [Fact]
    public void Activity_NewEdit_ReplacesTheEdit()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Edit a", ToolActivityStatus.InProgress, "a.txt", null, new EditChange("1", "2"));

        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", null, ToolActivityStatus.InProgress, null, null, new EditChange("1", "3"));

        var call = Assert.Single(Assert.Single(drafts.ForRoom("room1")).ToolCalls);
        Assert.Equal(new EditChange("1", "3"), call.Edit);
    }

    /// <summary>A snapshot's rows are a copy: a later activity never changes a list already handed out.</summary>
    [Fact]
    public void ForRoom_ToolCalls_AreACopy()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading", ToolActivityStatus.InProgress);
        var before = Assert.Single(drafts.ForRoom("room1"));

        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Read", ToolActivityStatus.Completed);
        drafts.Activity("m1", "tc-2", "room1", "agentA", "Ada", "Next", ToolActivityStatus.Pending);

        var call = Assert.Single(before.ToolCalls);
        Assert.Equal("Reading", call.Title);
        Assert.Equal(ToolActivityStatus.InProgress, call.Status);
    }

    /// <summary>Completing a Turn removes its Draft and with it every row.</summary>
    [Fact]
    public void Complete_RemovesTheRows()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading", ToolActivityStatus.InProgress);

        drafts.Complete("m1");

        Assert.Empty(drafts.ForRoom("room1"));
    }

    /// <summary>Clearing an Agent's Drafts removes its rows too.</summary>
    [Fact]
    public void ClearForAgent_RemovesTheRows()
    {
        var drafts = new Drafts();
        drafts.Activity("m1", "tc-1", "room1", "agentA", "Ada", "Reading", ToolActivityStatus.InProgress);

        drafts.ClearForAgent("agentA");

        Assert.Empty(drafts.ForRoom("room1"));
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
