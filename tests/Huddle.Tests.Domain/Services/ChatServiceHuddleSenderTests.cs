using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// <see cref="ChatService.PostAsHuddleAsync"/> and the sender <see cref="KnownIds.Huddle"/> (ADR-0065): a Message Huddle wrote itself.
/// It needs no Membership, and it is not the Human and not an Agent, so it answers no Mention, clears nothing from Needs You,
/// resets and spends no Budget, and leaves a Room's waiting cards alone.
/// </summary>
public sealed class ChatServiceHuddleSenderTests
{
    /// <summary>Huddle posts into a Room it is not a Member of; the Message is stored under the id and Name of Huddle.</summary>
    [Fact]
    public async Task PostAsHuddle_NonMember_AppendsUnderHuddle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        ChatMessage posted = await fixture.Service.PostAsHuddleAsync(room.Id, "@Nova GRO-1 changed", ct);

        Assert.Equal(("huddle", "Huddle", "@Nova GRO-1 changed"), (posted.SenderId, posted.SenderName, posted.Text));
        ChatMessage stored = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal(posted.Id, stored.Id);
        Assert.Equal(KnownIds.Huddle, stored.SenderId);
    }

    /// <summary>The event names the assignee as mentioned, lists only the Room's real Members, and is the one the Agents are delivered.</summary>
    [Fact]
    public async Task PostAsHuddle_PublishesAMentionOfTheAssigneeAndNoHuddleMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "@Nova look", ct);

        MessagePostedEvent only = Assert.Single(published);
        Assert.Equal(nova.Id, Assert.Single(only.Mentions).Id);
        Assert.DoesNotContain(only.Members, member => string.Equals(member.Id, KnownIds.Huddle, StringComparison.Ordinal));
        Assert.True(MessagePostedEvent.IsRecipient(nova, only.Message.SenderId));
    }

    /// <summary>Nobody else gets the exemption: posting as Huddle through the ordinary door is refused, and so is any other non-Member.</summary>
    [Fact]
    public async Task Post_HuddleOrAnyOtherNonMember_StaysRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        User kai = await fixture.AddAgentAsync("Kai", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        ChatException asHuddle = await Assert.ThrowsAsync<ChatException>(() => fixture.Service.PostAsync(room.Id, KnownIds.Huddle, "hi", ct: ct));
        ChatException asStranger = await Assert.ThrowsAsync<ChatException>(() => fixture.Service.PostAsync(room.Id, kai.Id, "hi", ct: ct));

        Assert.Equal(ErrorCodes.NotMember, asHuddle.Code);
        Assert.Equal(ErrorCodes.NotMember, asStranger.Code);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
    }

    /// <summary>An unknown Room and a blank text are refused as they are for any post.</summary>
    [Fact]
    public async Task PostAsHuddle_UnknownRoomOrBlankText_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        ChatException unknown = await Assert.ThrowsAsync<ChatException>(() => fixture.Service.PostAsHuddleAsync("no-such-room", "hi", ct));
        ChatException blank = await Assert.ThrowsAsync<ChatException>(() => fixture.Service.PostAsHuddleAsync(room.Id, "  ", ct));

        Assert.Equal(ErrorCodes.UnknownRoom, unknown.Code);
        Assert.Equal(ErrorCodes.BadMessage, blank.Code);
    }

    /// <summary>A Huddle post neither spends the Room's Budget nor resets what the Agents have spent; the Human's post still resets it.</summary>
    [Fact]
    public async Task PostAsHuddle_LeavesTheRoomBudgetAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "one", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "two", ct: ct);

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "wake", ct);

        Assert.Equal(2, fixture.Service.GetBudget(room.Id).Used);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "hello", ct: ct);

        Assert.Equal(0, fixture.Service.GetBudget(room.Id).Used);
    }

    /// <summary>A Huddle post posts into an exhausted Room too, and does not clear the Budget reason from Needs You.</summary>
    [Fact]
    public async Task PostAsHuddle_InAnExhaustedRoom_StillPostsAndKeepsTheBudgetReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        foreach (string text in new[] { "one", "two", "three" })
        {
            _ = await fixture.Service.PostAsync(room.Id, nova.Id, text, ct: ct);
        }

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "wake", ct);

        Assert.True(fixture.Service.GetBudget(room.Id).Exhausted);
        Assert.Equal(NeedsYouReason.Budget, Assert.Single(fixture.Tracker.Snapshot()).Reasons);
    }

    /// <summary>A Huddle post leaves a Room's waiting Question and Form where they are; a typed Human Message still drops them.</summary>
    [Fact]
    public async Task PostAsHuddle_LeavesWaitingQuestionsAndFormsAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = fixture.Questions.TryPut(NeedsYouFixture.Card(room.Id, nova));
        PendingElicitation form = fixture.AddForm(room.Id, nova);

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "wake", ct);

        Assert.NotNull(fixture.Questions.Get(room.Id));
        Assert.NotNull(fixture.Forms.TryTake(room.Id, form.Id));
    }

    /// <summary>An unanswered Mention of the Human stays in Needs You and its Notification stays unresolved after a Huddle post.</summary>
    [Fact]
    public async Task PostAsHuddle_DoesNotAnswerAMentionOfTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You?", ct: ct);

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "@Nova GRO-1 changed", ct);

        Assert.Equal(NeedsYouReason.Mention, Assert.Single(fixture.Tracker.Snapshot()).Reasons);
        Assert.DoesNotContain(new FeedResolution(room.Id, FeedSourceType.RoomMention), feed.Resolutions);
    }

    /// <summary>A Huddle post in a Room with no waiting reason does not start one.</summary>
    [Fact]
    public async Task PostAsHuddle_StartsNoNeedsYouReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        _ = await fixture.Service.PostAsHuddleAsync(room.Id, "@Nova wake", ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }
}
