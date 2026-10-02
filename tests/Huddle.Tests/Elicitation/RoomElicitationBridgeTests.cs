using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="RoomElicitationBridge"/> (elicitation bridge, decisions E-5 and E-6): a supported form
/// becomes a card in the Room and waits for the Human; an unsupported one is declined at once with a
/// warning and no card; cancelling the request hides the card and resolves it as cancelled without ever
/// throwing out of the cancellation callback; the asker is shown under its current Name.
/// </summary>
public sealed class RoomElicitationBridgeTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>A supported form becomes one card carrying the Room, the asking Agent, its Name and the form read from the schema.</summary>
    [Fact]
    public async Task RequestAsync_SupportedForm_AddsACardForTheRoomAskerAndForm()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);

        PendingElicitation card = Assert.Single(fixture.Elicitations.Get(room.Id));
        Assert.Equal(room.Id, card.RoomId);
        Assert.Equal(coach.Id, card.AskerAgentId);
        Assert.Equal("Coach", card.AskerName);
        Assert.Equal(SingleQuestionMessage, card.Form.Message);
        Assert.Equal(["question_0", "question_0_custom"], card.Form.Fields.Select(field => field.Key).ToArray());
        await cts.CancelAsync();
        _ = await request.WaitAsync(BoundedWait, ct);
    }

    /// <summary>When the Human answers, the request returns the accepted content the answer composed to.</summary>
    [Fact]
    public async Task RequestAsync_Answered_ReturnsTheAcceptedContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), ct);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        PendingElicitation card = Assert.Single(fixture.Elicitations.Get(room.Id));

        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "SQLite")), ct);

        ElicitationAccepted accepted = Assert.IsType<ElicitationAccepted>(await request.WaitAsync(BoundedWait, ct));
        Assert.Equal(["question_0"], accepted.Content.Keys.ToArray());
        Assert.Equal("SQLite", Assert.IsType<string>(accepted.Content["question_0"]));
    }

    /// <summary>When the Human skips, the request returns declined.</summary>
    [Fact]
    public async Task RequestAsync_Skipped_ReturnsDeclined()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), ct);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        PendingElicitation card = Assert.Single(fixture.Elicitations.Get(room.Id));

        _ = await fixture.Service.DeclineAsync(room.Id, card.Id);

        Assert.IsType<ElicitationDeclined>(await request.WaitAsync(BoundedWait, ct));
    }

    /// <summary>When the request is cancelled (a Stop, the Turn ending) the card is dropped so the Room stops showing it, the change is announced, and the request returns cancelled.</summary>
    [Fact]
    public async Task RequestAsync_Cancelled_DropsTheCardAndReturnsCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        List<string> raised = [];
        fixture.Events.ElicitationsChanged += raised.Add;

        await cts.CancelAsync();

        Assert.IsType<ElicitationCancelled>(await request.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
        Assert.Equal([room.Id], raised);
    }

    /// <summary>A token that is already cancelled when the request arrives returns cancelled and leaves no card behind.</summary>
    [Fact]
    public async Task RequestAsync_AlreadyCancelled_ReturnsCancelledAndLeavesNoCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        ElicitationResult result = await fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token)
            .WaitAsync(BoundedWait, ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>
    /// Cancelling never throws out of the bridge's cancellation callback, even when a subscriber to the
    /// change event does: <c>CancellationTokenSource.Cancel</c> would rethrow it to the caller, and a
    /// throwing callback can end the Room Session's consumer loop.
    /// </summary>
    [Fact]
    public async Task RequestAsync_CancelWhileAChangeHandlerThrows_DoesNotThrowAndStillCancels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        fixture.Events.ElicitationsChanged += _ => throw new InvalidOperationException("a subscriber that always fails");

        Exception? thrown = Record.Exception(cts.Cancel);

        Assert.Null(thrown);
        Assert.IsType<ElicitationCancelled>(await request.WaitAsync(BoundedWait, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>A form the reader cannot show is declined at once, with a warning that names the reason, and no card is ever shown or announced.</summary>
    [Fact]
    public async Task RequestAsync_UnsupportedForm_DeclinesAtOnceWithAWarningAndNoCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        List<string> raised = [];
        fixture.Events.ElicitationsChanged += raised.Add;
        const string Nested = """{"type":"object","properties":{"a":{"type":"object","properties":{"b":{"type":"string"}}}}}""";

        ElicitationResult result = await fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(Nested), ct)
            .WaitAsync(BoundedWait, ct);

        Assert.IsType<ElicitationDeclined>(result);
        Assert.Empty(fixture.Elicitations.Get(room.Id));
        Assert.Empty(raised);
        (LogLevel Level, string Message) entry = Assert.Single(fixture.BridgeLog.Snapshot());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"Declined an unsupported form from agent {coach.Id} in room {room.Id}: Property 'a' has the unsupported type 'object'.", entry.Message);
    }

    /// <summary>An asker renamed since it was registered is shown under its current Name, read fresh for each request.</summary>
    [Fact]
    public async Task RequestAsync_AskerRenamed_ShowsTheCurrentName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        Assert.True(fixture.Directory.RenameUser(coach.Id, "Trainer"));
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);

        Assert.Equal("Trainer", Assert.Single(fixture.Elicitations.Get(room.Id)).AskerName);
        await cts.CancelAsync();
        _ = await request.WaitAsync(BoundedWait, ct);
    }

    /// <summary>An asker the Team Directory no longer knows is shown under its id rather than refused: the question still deserves an answer.</summary>
    [Fact]
    public async Task RequestAsync_AskerUnknown_FallsBackToTheAgentId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, "agent-gone"), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);

        Assert.Equal("agent-gone", Assert.Single(fixture.Elicitations.Get(room.Id)).AskerName);
        await cts.CancelAsync();
        _ = await request.WaitAsync(BoundedWait, ct);
    }

    /// <summary>Two requests in one Room are two cards in arrival order; cancelling the first leaves the second waiting, and answering it still works.</summary>
    [Fact]
    public async Task RequestAsync_TwoRequestsInOneRoom_CancellingOneKeepsTheOther()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        using CancellationTokenSource firstCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ElicitationResult> first = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), firstCts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        Task<ElicitationResult> second = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, nova.Id), Request(SingleQuestionSchema, SingleQuestionMessage), ct);
        await WaitForCardsAsync(fixture, room.Id, 2, ct);
        Assert.Equal(["Coach", "Nova"], fixture.Elicitations.Get(room.Id).Select(card => card.AskerName).ToArray());

        await firstCts.CancelAsync();

        Assert.IsType<ElicitationCancelled>(await first.WaitAsync(BoundedWait, ct));
        PendingElicitation remaining = Assert.Single(fixture.Elicitations.Get(room.Id));
        Assert.Equal("Nova", remaining.AskerName);
        Assert.False(second.IsCompleted);
        _ = await fixture.Service.AnswerAsync(room.Id, remaining.Id, Values(Pair("question_0", "Postgres")), ct);
        Assert.IsType<ElicitationAccepted>(await second.WaitAsync(BoundedWait, ct));
    }

    /// <summary>A request cancelled after the Human already answered keeps the answer: the cancellation finds nothing left to drop.</summary>
    [Fact]
    public async Task RequestAsync_CancelledAfterAnswered_KeepsTheAnswer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        PendingElicitation card = Assert.Single(fixture.Elicitations.Get(room.Id));
        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "Postgres")), ct);

        await cts.CancelAsync();

        Assert.IsType<ElicitationAccepted>(await request.WaitAsync(BoundedWait, ct));
    }

    /// <summary>
    /// A request cancelled while the Human's answer is still being posted - the card already taken, the
    /// request not yet answered - still returns cancelled: the cancellation must release a request that
    /// nobody can answer any more, not wait for an answer that may never be completed.
    /// </summary>
    [Fact]
    public async Task RequestAsync_CancelledAfterTheCardWasTaken_StillReturnsCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ElicitationResult> request = fixture.Bridge.RequestAsync(new ElicitationContext(room.Id, coach.Id), Request(SingleQuestionSchema, SingleQuestionMessage), cts.Token);
        await WaitForCardsAsync(fixture, room.Id, 1, ct);
        PendingElicitation card = Assert.Single(fixture.Elicitations.Get(room.Id));
        Assert.Same(card, fixture.Elicitations.TryTake(room.Id, card.Id));

        await cts.CancelAsync();

        Assert.IsType<ElicitationCancelled>(await request.WaitAsync(BoundedWait, ct));
    }

    /// <summary>Waits until <paramref name="roomId"/> holds at least <paramref name="count"/> cards, on the store's own change event, bounded so a regression fails rather than hangs.</summary>
    /// <param name="fixture">The fixture whose store and hub are watched.</param>
    /// <param name="roomId">The Room to watch.</param>
    /// <param name="count">How many cards to wait for.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task WaitForCardsAsync(ElicitationFixture fixture, string roomId, int count, CancellationToken ct)
    {
        TaskCompletionSource appeared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string> check = room =>
        {
            if (string.Equals(room, roomId, StringComparison.Ordinal) && fixture.Elicitations.Get(roomId).Count >= count)
            {
                _ = appeared.TrySetResult();
            }
        };
        fixture.Events.ElicitationsChanged += check;
        try
        {
            check(roomId);
            await appeared.Task.WaitAsync(BoundedWait, ct);
        }
        finally
        {
            fixture.Events.ElicitationsChanged -= check;
        }
    }
}
