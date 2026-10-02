using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="ElicitationService"/> (elicitation bridge, decisions E-4, E-5 and E-6): the Human's
/// answer to a form becomes a Transcript Message from the Human that mentions nobody and is withheld
/// from the asker, and only then is the waiting request answered; Skip declines and posts nothing; a
/// request is never left waiting, whatever goes wrong while posting. Real collaborators throughout.
/// </summary>
public sealed class ElicitationServiceTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>An answer is posted to the Room's Transcript as a Message from the Human, in the quote-blank-line-answer shape, and returned.</summary>
    [Fact]
    public async Task Answer_PostsTheTranscriptMessage_AsTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        string? text = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct);

        Assert.Equal("> Which database?\n\nPostgres", text);
        ChatMessage posted = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal(KnownIds.Human, posted.SenderId);
        Assert.Equal("> Which database?\n\nPostgres", posted.Text);
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>
    /// The answer mentions nobody, even when its free text names the asker, and is withheld from the
    /// asker: the asker's Turn is still open and takes the answer as the tool's result, so a delivery
    /// would queue a second Turn.
    /// </summary>
    [Fact]
    public async Task Answer_IsWithheldFromTheAsker_AndMentionsNobody_EvenWhenTheTextNamesTheAsker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(McpSchema));
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("name", "ask @Coach"), Pair("agree", "true")), ct);

        MessagePostedEvent only = Assert.Single(published);
        Assert.Empty(only.Mentions);
        Assert.Equal(coach.Id, only.WithheldFromAgentId);
        Assert.Equal(KnownIds.Human, only.Message.SenderId);
    }

    /// <summary>The waiting request is answered only after the Message landed, with the wire content the Human's values compose to.</summary>
    [Fact]
    public async Task Answer_CompletesTheCardAcceptedWithTheWireContent_AfterThePostLanded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);
        bool completedWhenThePostLanded = true;
        fixture.Events.MessagePosted += _ => completedWhenThePostLanded = card.Completion.Task.IsCompleted;

        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct);

        Assert.False(completedWhenThePostLanded, "the request was answered before the Message reached the Transcript");
        ElicitationAccepted accepted = Assert.IsType<ElicitationAccepted>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Equal(["question_0"], accepted.Content.Keys.ToArray());
        Assert.Equal("Postgres", Assert.IsType<string>(accepted.Content["question_0"]));
    }

    /// <summary>A second answer to the same card finds nothing: it posts nothing and returns <see langword="null"/>.</summary>
    [Fact]
    public async Task Answer_SecondAnswer_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);
        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct);

        string? second = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "SQLite")), ct);

        Assert.Null(second);
        Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
    }

    /// <summary>An answer carrying an id nothing holds posts nothing and leaves the Room's cards waiting.</summary>
    [Fact]
    public async Task Answer_UnknownCardId_PostsNothingAndLeavesTheCards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        string? text = await fixture.Service.AnswerAsync(room.Id, "not-a-card", Values(Pair("question_0", "Postgres")), ct);

        Assert.Null(text);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Same(card, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Answering one card must not touch the Room's other waiting card: its handler is still waiting and it is still shown.</summary>
    [Fact]
    public async Task Answer_ToOneCard_LeavesTheRoomsOtherCardWaiting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation first = fixture.AddCard(room.Id, coach);
        PendingElicitation second = fixture.AddCard(room.Id, nova);

        _ = await fixture.Service.AnswerAsync(room.Id, first.Id, Values(Pair("question_0", "Postgres")), ct);

        Assert.Same(second, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(second.Completion.Task.IsCompleted);
    }

    /// <summary>
    /// An answer that does not fit the form is a UI bug and throws, naming the field - and leaves the
    /// card waiting and answerable, because taking it first would leave the agent waiting for ever.
    /// </summary>
    [Fact]
    public async Task Answer_InvalidValues_ThrowNamingTheField_AndKeepTheCardAnswerable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(McpSchema));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("age", "2.5")), ct));

        Assert.Equal("Field 'age' must be a whole number.", exception.Message);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Same(card, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(card.Completion.Task.IsCompleted);
        string? retried = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("name", "Ada"), Pair("agree", "true")), ct);
        Assert.Equal("> Name\n\nAda\n\n> I agree\n\nYes", retried);
    }

    /// <summary>When the post fails (the Room was deleted mid-tap) the failure is logged and nothing is returned, but the waiting request is still answered: the agent never waits for a Message that will not come.</summary>
    [Fact]
    public async Task Answer_PostFails_StillCompletesTheCardAcceptedAndLogs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        PendingElicitation card = fixture.Elicitations.Add("no-such-room", "agent-coach", "Coach", FormOf(SingleQuestionSchema, SingleQuestionMessage));

        string? text = await fixture.Service.AnswerAsync("no-such-room", card.Id, Values(Pair("question_0", "Postgres")), ct);

        Assert.Null(text);
        Assert.IsType<ElicitationAccepted>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        (LogLevel Level, string Message) entry = Assert.Single(fixture.ServiceLog.Snapshot(), item => item.Level == LogLevel.Warning);
        Assert.Equal("Could not post the answer in room no-such-room: Unknown room 'no-such-room'.", entry.Message);
    }

    /// <summary>A Transcript that cannot be written is no different: logged, nothing returned, and the request still answered.</summary>
    [Fact]
    public async Task Answer_TranscriptAppendThrows_StillCompletesTheCardAcceptedAndLogs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct, inner => new ThrowingStore(inner, new IOException("disk full")));
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        string? text = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct);

        Assert.Null(text);
        Assert.IsType<ElicitationAccepted>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        (LogLevel Level, string Message) entry = Assert.Single(fixture.ServiceLog.Snapshot(), item => item.Level == LogLevel.Warning);
        Assert.Equal($"Could not post the answer in room {room.Id}: disk full", entry.Message);
    }

    /// <summary>A caller that gives up while the answer is being posted still gets the request answered, and its cancellation still propagates.</summary>
    [Fact]
    public async Task Answer_CancelledWhilePosting_StillCompletesTheCard_AndPropagates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct, inner => new ThrowingStore(inner, new OperationCanceledException()));
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct));

        Assert.IsType<ElicitationAccepted>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
    }

    /// <summary>Skip takes the card and resolves the request as declined, and posts nothing: there is no Message, no event, and the asker is not woken.</summary>
    [Fact]
    public async Task Skip_CompletesTheCardDeclined_AndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        bool skipped = await fixture.Service.DeclineAsync(room.Id, card.Id);

        Assert.True(skipped);
        Assert.IsType<ElicitationDeclined>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
        Assert.Empty(published);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
    }

    /// <summary>A Skip carrying an id nothing holds changes nothing: it reports that, and the card stays waiting.</summary>
    [Fact]
    public async Task Skip_UnknownCardId_ChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);

        bool skipped = await fixture.Service.DeclineAsync(room.Id, "not-a-card");

        Assert.False(skipped);
        Assert.Same(card, Assert.Single(fixture.Elicitations.Get(room.Id)));
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>A Transcript store whose append throws what it was given, and forwards everything else.</summary>
    /// <param name="inner">The real store the reads are forwarded to.</param>
    /// <param name="failure">What every append throws.</param>
    private sealed class ThrowingStore(IChatStore inner, Exception failure) : IChatStore
    {
        /// <inheritdoc/>
        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            throw failure;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
        {
            return inner.ReadAllAsync(roomId, ct);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string roomId, CancellationToken ct = default)
        {
            return inner.DeleteAsync(roomId, ct);
        }

        /// <inheritdoc/>
        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
        {
            return inner.HasMessagesAsync(roomId, ct);
        }
    }
}
