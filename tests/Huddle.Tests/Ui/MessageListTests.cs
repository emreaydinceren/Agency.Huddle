using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="MessageList"/>: its autoscroll decision, and (Task 6.1) the avatar each row
/// resolves beside it. The autoscroll decision is a pure function of rendered content, so those
/// cases exercise it directly rather than by driving a render lifecycle; the avatar cases render the
/// real component through <see cref="MudBunitContext"/> against a real <see cref="AvatarStore"/> -
/// the same "render for real, register the real store" pattern <c>AppearanceAvatarTests</c> and
/// <c>TeammateAvatarTests</c> both use - since <see cref="AvatarStore.Get(string)"/> is what a row
/// actually resolves against, not a lookup this suite could fake without also proving the wiring.
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

    /// <summary>An avatar renders beside a settled Message row - the gutter Part A's two-column layout adds.</summary>
    [Fact]
    public async Task MessageList_Message_RendersAnAvatar()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = MessageListTests.NewContext(factory);
        var message = MessageListTests.MessageFrom("echo");

        var cut = ctx.Render<MessageList>(parameters => parameters.Add(p => p.Messages, [message]));

        Assert.NotEmpty(cut.FindAll(".mud-avatar"));
    }

    /// <summary>
    /// A Draft gets an avatar too - it is the same Teammate mid-sentence, and a gutter that only
    /// appeared once the Turn landed would make every streaming reply jump sideways as it completed.
    /// </summary>
    [Fact]
    public async Task MessageList_Draft_RendersAnAvatar()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = MessageListTests.NewContext(factory);
        var draft = MessageListTests.DraftFrom("echo", "Still thinking this through");

        var cut = ctx.Render<MessageList>(parameters => parameters.Add(p => p.Drafts, [draft]));

        Assert.NotEmpty(cut.FindAll(".mud-avatar"));
    }

    /// <summary>A Message from a Teammate with a stored label shows that label rather than the monogram.</summary>
    [Fact]
    public async Task MessageList_Message_FromTeammateWithStoredLabel_ShowsTheLabel()
    {
        await using var factory = new TeamWebApplicationFactory();
        var avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("echo", new Avatar(Label: "EC", Image: null, Background: null));
        await using var ctx = MessageListTests.NewContext(factory);
        var message = MessageListTests.MessageFrom("echo");

        var cut = ctx.Render<MessageList>(parameters => parameters.Add(p => p.Messages, [message]));

        Assert.Equal("EC", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>A Message from a sender with no stored avatar shows the monogram of the sender name.</summary>
    [Fact]
    public async Task MessageList_Message_FromSenderWithNoStoredAvatar_ShowsTheMonogram()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = MessageListTests.NewContext(factory);
        var message = MessageListTests.MessageFrom("Jordan Lee");

        var cut = ctx.Render<MessageList>(parameters => parameters.Add(p => p.Messages, [message]));

        Assert.Equal("JL", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>
    /// The Human's own Messages resolve through the same <see cref="AvatarStore.Get(string)"/> path a
    /// Teammate's do: a Message whose <see cref="ChatMessage.SenderName"/> is the Human's Name picks
    /// up the Human's own stored avatar, with no separate lookup path for it.
    /// </summary>
    [Fact]
    public async Task MessageList_Message_FromTheHuman_ResolvesTheHumansStoredAvatar()
    {
        await using var factory = new TeamWebApplicationFactory();
        var avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("You", new Avatar(Label: "HU", Image: null, Background: null));
        await using var ctx = MessageListTests.NewContext(factory);
        var message = new ChatMessage("m-1", DateTimeOffset.UnixEpoch, KnownIds.Human, "You", "hello team");

        var cut = ctx.Render<MessageList>(parameters => parameters.Add(p => p.Messages, [message]));

        Assert.Equal("HU", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>Builds a settled Message from <paramref name="senderName"/>, with an id and timestamp that carry no meaning of their own.</summary>
    /// <param name="senderName">The Message's <see cref="ChatMessage.SenderName"/>, and the key <see cref="AvatarStore.Get(string)"/> resolves it by.</param>
    private static ChatMessage MessageFrom(string senderName) =>
        new("m-1", DateTimeOffset.UnixEpoch, "u-1", senderName, "hi");

    /// <summary>Builds a Draft from <paramref name="senderName"/> carrying <paramref name="text"/>, with ids that carry no meaning of their own.</summary>
    /// <param name="senderName">The Draft's <see cref="Draft.SenderName"/>, and the key <see cref="AvatarStore.Get(string)"/> resolves it by.</param>
    /// <param name="text">The text the Draft has accumulated so far.</param>
    private static Draft DraftFrom(string senderName, string text) =>
        new("msg-1", "room-1", "agent-1", senderName, text, null, null);

    /// <summary>Registers this factory's real <see cref="AvatarStore"/> into a fresh <see cref="MudBunitContext"/> - the same pattern <c>AppearanceAvatarTests.NewContext</c> and <c>TeammateCardTests.NewContext</c> use.</summary>
    /// <param name="factory">The factory whose composed <see cref="AvatarStore"/> singleton to reuse.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        return ctx;
    }
}
