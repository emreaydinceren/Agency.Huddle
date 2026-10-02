using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Questions;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins when <c>ChatService</c> drops a Room's waiting Elicitation cards (elicitation bridge, decision
/// E-5): a typed Human Message, an archive and a delete drop every card of the Room and resolve each
/// waiting request as cancelled; an Agent's Message, an unarchive and the Human's own answer to a form
/// do not - answering card A must not cancel card B.
/// </summary>
public sealed class ChatServiceElicitationDropTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>A Message the Human types cancels every waiting card of the Room and empties it.</summary>
    [Fact]
    public async Task Post_TypedHumanMessage_CancelsEveryCardOfTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation first = fixture.AddCard(room.Id, coach);
        PendingElicitation second = fixture.AddCard(room.Id, nova);

        _ = await fixture.Chat.PostAsync(room.Id, KnownIds.Human, "never mind, do it your way", ct: ct);

        Assert.IsType<ElicitationCancelled>(await first.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.IsType<ElicitationCancelled>(await second.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>The composer's door is the same door: a Message sent through it drops the cards too.</summary>
    [Fact]
    public async Task Submit_FromTheComposer_CancelsTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        _ = await fixture.Chat.SubmitFromComposerAsync(room.Id, KnownIds.Human, "skip it", ct);

        Assert.IsType<ElicitationCancelled>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
    }

    /// <summary>A typed Message in one Room leaves every other Room's cards, and the requests waiting on them, alone.</summary>
    [Fact]
    public async Task Post_TypedHumanMessage_LeavesOtherRoomsAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room first = await fixture.CreateRoomAsync([coach], ct);
        Room second = await fixture.CreateRoomAsync([nova], ct);
        PendingElicitation other = fixture.AddCard(second.Id, nova);

        _ = await fixture.Chat.PostAsync(first.Id, KnownIds.Human, "hello", ct: ct);

        Assert.Same(other, Assert.Single(fixture.Elicitations.Get(second.Id)));
        Assert.False(other.Completion.Task.IsCompleted);
    }

    /// <summary>An Agent's Message does not drop the cards: the asker's own framing Message lands after the card appears.</summary>
    [Fact]
    public async Task Post_AgentMessage_KeepsTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        _ = await fixture.Chat.PostAsync(room.Id, coach.Id, "one moment", ct: ct);

        Assert.Same(card, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Archiving a Room cancels its cards: an archived Room hides them, so the request would otherwise wait for ever.</summary>
    [Fact]
    public async Task SetRoomArchived_True_CancelsTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        _ = await fixture.Chat.SetRoomArchivedAsync(room.Id, true, ct);

        Assert.IsType<ElicitationCancelled>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>Unarchiving drops nothing: the Room is exactly as live as it was.</summary>
    [Fact]
    public async Task SetRoomArchived_False_KeepsTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        _ = await fixture.Chat.SetRoomArchivedAsync(room.Id, false, ct);

        Assert.Same(card, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Deleting a Room cancels its cards: the Room a card belonged to no longer exists.</summary>
    [Fact]
    public async Task DeleteRoom_CancelsTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        await fixture.Chat.DeleteRoomAsync(room.Id, ct);

        Assert.IsType<ElicitationCancelled>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>
    /// The Human's answer to card A is a Human Message, but it must not cancel card B: it drops the
    /// Room's waiting Questions, as any Human Message does, and leaves the Elicitation cards alone.
    /// </summary>
    [Fact]
    public async Task PostHumanAnswer_KeepsTheElicitationCards_ButDropsWaitingQuestions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation answered = fixture.AddCard(room.Id, coach);
        PendingElicitation other = fixture.AddCard(room.Id, nova);
        _ = fixture.Questions.TryPut(new PendingQuestions(
            "card-1",
            room.Id,
            nova.Id,
            "Nova",
            [new Question("Which?", ["A", "B"], QuestionKind.SingleSelect)],
            DateTimeOffset.UnixEpoch));

        _ = await fixture.Chat.PostHumanAnswerAsync(room.Id, KnownIds.Human, "an answer", coach.Id, ct);

        Assert.Equal([answered.Id, other.Id], fixture.Elicitations.Get(room.Id).Select(card => card.Id).ToArray());
        Assert.False(answered.Completion.Task.IsCompleted);
        Assert.False(other.Completion.Task.IsCompleted);
        Assert.Null(fixture.Questions.Get(room.Id));
    }
}
