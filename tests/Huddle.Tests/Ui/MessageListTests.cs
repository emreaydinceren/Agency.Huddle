using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="MessageList"/>'s autoscroll decision. The component scrolls when the
/// content it has rendered has changed, and the decision is a pure function of that content, so
/// these exercise it directly rather than by driving a render lifecycle.
/// </summary>
public sealed class MessageListTests
{
    /// <summary>
    /// A Draft growing changes the signature even though no Message arrived. This is the whole
    /// point of folding Draft length in: counting Messages alone left a streaming reply rendering
    /// forever without the view ever following it down.
    /// </summary>
    [Fact]
    public void RenderedSignature_ChangesWhenOnlyADraftGrew()
    {
        IReadOnlyList<ChatMessage> messages = [];

        var before = MessageList.RenderedSignature(messages, [MessageListTests.DraftWith("Some")]);
        var after = MessageList.RenderedSignature(messages, [MessageListTests.DraftWith("Some more")]);

        Assert.NotEqual(before, after);
    }

    /// <summary>A new Message changes the signature, which is the behaviour that already existed.</summary>
    [Fact]
    public void RenderedSignature_ChangesWhenAMessageArrives()
    {
        var message = new ChatMessage("m-1", DateTimeOffset.UnixEpoch, "u-1", "echo", "hi");

        var before = MessageList.RenderedSignature([], []);
        var after = MessageList.RenderedSignature([message], []);

        Assert.NotEqual(before, after);
    }

    /// <summary>Nothing rendered and nothing arriving must not ask the view to scroll.</summary>
    [Fact]
    public void RenderedSignature_IsUnchangedWhenNothingArrived()
    {
        var message = new ChatMessage("m-1", DateTimeOffset.UnixEpoch, "u-1", "echo", "hi");
        IReadOnlyList<ChatMessage> messages = [message];
        IReadOnlyList<Draft> drafts = [MessageListTests.DraftWith("partial")];

        var before = MessageList.RenderedSignature(messages, drafts);
        var after = MessageList.RenderedSignature(messages, drafts);

        Assert.Equal(before, after);
    }

    /// <summary>A Draft becoming a Message changes the signature, so the settled text is scrolled to.</summary>
    [Fact]
    public void RenderedSignature_ChangesWhenADraftBecomesAMessage()
    {
        var message = new ChatMessage("m-1", DateTimeOffset.UnixEpoch, "u-1", "echo", "done");

        var streaming = MessageList.RenderedSignature([], [MessageListTests.DraftWith("done")]);
        var settled = MessageList.RenderedSignature([message], []);

        Assert.NotEqual(streaming, settled);
    }

    /// <summary>Builds a Draft carrying <paramref name="text"/> and nothing else of interest.</summary>
    /// <param name="text">The text the Draft has accumulated so far.</param>
    /// <returns>A Draft for use in a signature calculation.</returns>
    private static Draft DraftWith(string text) => new("msg-1", "room-1", "agent-1", "echo", text, null, null);
}
