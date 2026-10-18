using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// <see cref="ChatService.PostHumanAnswerAsync"/> (elicitation bridge, decision E-4): the Human's
/// answer to a form the asker opened mid-Turn. It is an ordinary Human Message in every way that
/// matters to the Room - Transcript, Budget, waiting Questions, the per-Room lock - except that it
/// mentions nobody and is withheld from the asker, whose Turn is still open and who receives the
/// answer as the tool's own result rather than as a second wake.
/// </summary>
public sealed class ChatServiceHumanAnswerTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>The answer is appended to the Room's Transcript as a Message from the Human, text untouched.</summary>
    [Fact]
    public async Task Answer_AppendsToTranscript_AsTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);

        ChatMessage? answer = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.NotNull(answer);
        Assert.Equal(KnownIds.Human, answer.SenderId);
        Assert.Equal("You", answer.SenderName);
        Assert.Equal("Strength", answer.Text);
        IReadOnlyList<ChatMessage> stored = await fixture.Transcript.ReadAllAsync(room.Id, ct);
        ChatMessage only = Assert.Single(stored);
        Assert.Equal(answer.Id, only.Id);
        Assert.Equal("Strength", only.Text);
        Assert.Equal(KnownIds.Human, only.SenderId);
    }

    /// <summary>The answer takes its place between the Messages around it: the Transcript reads before, answer, after.</summary>
    [Fact]
    public async Task Answer_LandsInTranscript_InPostingOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "before", ct: ct);
        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "answer", coach.Id, ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "after", ct: ct);

        IReadOnlyList<ChatMessage> stored = await fixture.Transcript.ReadAllAsync(room.Id, ct);
        Assert.Equal(["before", "answer", "after"], stored.Select(message => message.Text).ToArray());
    }

    /// <summary>
    /// The answer reaches the Room's subscribers exactly once, and the event names the asker as the
    /// Agent it is withheld from, carries no Mentions, and carries the very Message that was stored.
    /// </summary>
    [Fact]
    public async Task Answer_PublishesMessagePostedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        ChatMessage? answer = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.NotNull(answer);
        MessagePostedEvent only = Assert.Single(published);
        Assert.Equal(answer.Id, only.Message.Id);
        Assert.Equal(room.Id, only.Room.Id);
        Assert.Equal(coach.Id, only.WithheldFromAgentId);
        Assert.Empty(only.Mentions);
        Assert.Equal(2, only.Members.Count);
        Assert.Equal(new RoomBudget(0, 40), only.Budget);
    }

    /// <summary>
    /// An ordinary Human post names nobody as withheld, so existing constructions of the event are
    /// unchanged: <see cref="MessagePostedEvent.WithheldFromAgentId"/> defaults to null.
    /// </summary>
    [Fact]
    public async Task Post_OrdinaryHumanMessage_WithholdsNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "hi @Coach", ct: ct);

        MessagePostedEvent only = Assert.Single(published);
        Assert.Null(only.WithheldFromAgentId);
        Assert.Single(only.Mentions);
    }

    /// <summary>The answer is a Human Message, so it resets the Room's count of agent Messages to zero.</summary>
    [Fact]
    public async Task Answer_ResetsBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(3, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "one", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "two", ct: ct);
        Assert.Equal(new RoomBudget(2, 3), fixture.Service.GetBudget(room.Id));

        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.Equal(new RoomBudget(0, 3), fixture.Service.GetBudget(room.Id));
    }

    /// <summary>
    /// A Budget the Human extended expires with the run it was granted for, exactly as a typed Human
    /// Message makes it: the answer returns the grant to one Budget, not just the count to zero.
    /// </summary>
    [Fact]
    public async Task Answer_AfterAnExtension_ResetsTheGrantToOneBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(1, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "one", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "carry on", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "two", ct: ct);
        BudgetExtension extension = await fixture.Service.ExtendBudgetAsync(room.Id, ct);
        Assert.Equal(new RoomBudget(1, 2), extension.Budget);

        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.Equal(new RoomBudget(0, 1), fixture.Service.GetBudget(room.Id));
    }

    /// <summary>The answer is what the waiting card was asking for, so it drops the card, as a typed Human Message does.</summary>
    [Fact]
    public async Task Answer_DropsWaitingQuestions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Questions.TryPut(MakePending(room.Id, coach.Id));

        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Dropping is per Room: an answer in one Room leaves another Room's waiting card alone.</summary>
    [Fact]
    public async Task Answer_KeepsOtherRoomsQuestions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room first = await fixture.CreateRoomAsync([coach], ct);
        Room second = await fixture.CreateRoomAsync([nova], ct);
        PendingQuestions pending = MakePending(second.Id, nova.Id);
        _ = fixture.Questions.TryPut(pending);

        _ = await fixture.Service.PostHumanAnswerAsync(first.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.Equal(pending, fixture.Questions.Get(second.Id));
    }

    /// <summary>
    /// A blank answer is nothing to record: no throw, no Message, no event, and nothing else the
    /// answer would have done - the Budget count and the waiting card are left as they were.
    /// </summary>
    /// <param name="text">A blank or whitespace-only answer.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    public async Task Answer_BlankText_PostsNothing(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "framing", ct: ct);
        PendingQuestions pending = MakePending(room.Id, coach.Id);
        _ = fixture.Questions.TryPut(pending);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        ChatMessage? answer = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, text, coach.Id, ct);

        Assert.Null(answer);
        Assert.Empty(published);
        IReadOnlyList<ChatMessage> stored = await fixture.Transcript.ReadAllAsync(room.Id, ct);
        ChatMessage only = Assert.Single(stored);
        Assert.Equal("framing", only.Text);
        Assert.Equal(new RoomBudget(1, 40), fixture.Service.GetBudget(room.Id));
        Assert.Equal(pending, fixture.Questions.Get(room.Id));
    }

    /// <summary>A Room that does not exist is refused with the same code <see cref="ChatService.PostAsync"/> uses.</summary>
    [Fact]
    public async Task Answer_UnknownRoom_ThrowsUnknownRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);

        ChatException exception = await Assert.ThrowsAsync<ChatException>(
            () => fixture.Service.PostHumanAnswerAsync("no-such-room", KnownIds.Human, "Strength", "agent-coach", ct));

        Assert.Equal(ErrorCodes.UnknownRoom, exception.Code);
    }

    /// <summary>A sender who is not a Member is refused with the same code <see cref="ChatService.PostAsync"/> uses, and nothing is appended.</summary>
    [Fact]
    public async Task Answer_ByNonMember_ThrowsNotMember_AndAppendsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);

        ChatException exception = await Assert.ThrowsAsync<ChatException>(
            () => fixture.Service.PostHumanAnswerAsync(room.Id, "not-a-member", "Strength", coach.Id, ct));

        Assert.Equal(ErrorCodes.NotMember, exception.Code);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
    }

    /// <summary>
    /// Archiving is a display filter, not a gate, so an archived Room still takes the answer - the
    /// same as <see cref="ChatService.PostAsync"/>, which never looks at <see cref="Room.Archived"/>.
    /// </summary>
    [Fact]
    public async Task Answer_InAnArchivedRoom_StillPosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.SetRoomArchivedAsync(room.Id, true, ct);

        ChatMessage? answer = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);

        Assert.NotNull(answer);
        ChatMessage only = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal("Strength", only.Text);
    }

    /// <summary>
    /// The answer mentions nobody, whatever its free text says: an <c>@Nova</c> the Human typed into a
    /// form field is text, not an instruction to wake Nova.
    /// </summary>
    [Fact]
    public async Task Answer_TextNamingAnAgent_MentionsNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(40, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        ChatMessage? answer = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "ask @Nova instead", coach.Id, ct);

        Assert.NotNull(answer);
        Assert.Equal("ask @Nova instead", answer.Text);
        MessagePostedEvent only = Assert.Single(published);
        Assert.Empty(only.Mentions);
        Assert.Equal(coach.Id, only.WithheldFromAgentId);
    }

    /// <summary>
    /// Pins the current behaviour of the accepted leak in E-4: <see cref="ChatService.ExtendBudgetAsync"/>
    /// redelivers the Room's last Message without a withhold, so it would wake the asker - but only
    /// for a paused Room, and the answer, as a Human Message, has just unpaused it. Even a Room the
    /// agents had exhausted is <see cref="ExtendResult.NotPaused"/> straight after an answer, and
    /// nothing is redelivered.
    /// </summary>
    [Fact]
    public async Task Answer_ExtendBudget_IsNotPaused_AndRedeliversNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(2, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "one", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "two", ct: ct);
        Assert.True(fixture.Service.GetBudget(room.Id).Exhausted);
        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);
        List<MessagePostedEvent> redelivered = [];
        fixture.Events.MessageRedelivered += redelivered.Add;

        BudgetExtension extension = await fixture.Service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.NotPaused, extension.Result);
        Assert.Null(extension.Redelivered);
        Assert.Equal(new RoomBudget(0, 2), extension.Budget);
        Assert.Empty(redelivered);
    }

    /// <summary>
    /// With the cap disabled (<c>AgentMessageBudget</c> of zero) a Room is never paused, so an
    /// extension after an answer grants nothing and redelivers nothing. The only door by which the
    /// answer could reach the asker again stays shut at this edge as well.
    /// </summary>
    [Fact]
    public async Task Answer_ExtendBudget_WithTheCapDisabled_RedeliversNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(0, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "Strength", coach.Id, ct);
        List<MessagePostedEvent> redelivered = [];
        fixture.Events.MessageRedelivered += redelivered.Add;

        BudgetExtension extension = await fixture.Service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.NotPaused, extension.Result);
        Assert.Null(extension.Redelivered);
        Assert.Empty(redelivered);
    }

    /// <summary>
    /// A redelivery of an ordinary Message withholds nobody: <see cref="MessagePostedEvent.WithheldFromAgentId"/>
    /// stays null on the <see cref="RoomEvents.MessageRedelivered"/> path, which E-4 leaves unchanged.
    /// </summary>
    [Fact]
    public async Task Extend_RedeliveryOfAnOrdinaryMessage_WithholdsNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(1, null, ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = await fixture.Service.PostAsync(room.Id, coach.Id, "one", ct: ct);
        List<MessagePostedEvent> redelivered = [];
        fixture.Events.MessageRedelivered += redelivered.Add;

        BudgetExtension extension = await fixture.Service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extension.Result);
        MessagePostedEvent only = Assert.Single(redelivered);
        Assert.Null(only.WithheldFromAgentId);
    }

    /// <summary>
    /// The answer and an ordinary post take the same per-Room lock, in either order: while one is
    /// parked inside the Transcript append, the other cannot reach it, and once released the
    /// Transcript and the event stream agree on the order. A core that took no lock - or a second
    /// semaphore of its own - lets the second append start while the first is still in flight.
    /// </summary>
    /// <param name="answerFirst">Whether the answer is the one holding the lock while the typed post waits.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Answer_AndPostAsync_ShareThePerRoomLock(bool answerFirst)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedStore? gated = null;
        using Fixture fixture = await Fixture.CreateAsync(40, inner => gated = new GatedStore(inner), ct);
        Assert.NotNull(gated);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        ConcurrentQueue<string> eventOrder = new();
        fixture.Events.MessagePosted += e => eventOrder.Enqueue(e.Message.Text);
        Func<Task> typed = () => fixture.Service.PostAsync(room.Id, KnownIds.Human, "typed", ct: ct);
        Func<Task> answer = () => fixture.Service.PostHumanAnswerAsync(room.Id, KnownIds.Human, "answer", coach.Id, ct);

        Task holding = (answerFirst ? answer : typed)();
        await gated.FirstEntered.WaitAsync(BoundedWait, ct);
        Task waiting = (answerFirst ? typed : answer)();
        _ = await Task.WhenAny(gated.SecondEntered, Task.Delay(TimeSpan.FromMilliseconds(750), ct));

        Assert.False(gated.SecondEntered.IsCompleted, "the second post reached the Transcript while the first still held the Room's lock");
        gated.ReleaseFirst();
        await Task.WhenAll(holding, waiting).WaitAsync(BoundedWait, ct);
        string[] expected = answerFirst ? ["answer", "typed"] : ["typed", "answer"];
        IReadOnlyList<ChatMessage> stored = await fixture.Transcript.ReadAllAsync(room.Id, ct);
        Assert.Equal(expected, stored.Select(message => message.Text).ToArray());
        Assert.Equal(expected, eventOrder.ToArray());
    }

    /// <summary>Builds a minimal, valid card of <see cref="PendingQuestions"/> waiting in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="askerAgentId">The asking Agent's id.</param>
    private static PendingQuestions MakePending(string roomId, string askerAgentId)
    {
        Question question = new("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect);
        return new PendingQuestions("card-1", roomId, askerAgentId, "Coach", [question], DateTimeOffset.UnixEpoch);
    }

    /// <summary>
    /// A Transcript store whose first append parks until released and whose later appends pass
    /// straight through, recording that they got there - the deterministic way to ask "did a second
    /// writer get in while the first one held the lock".
    /// </summary>
    /// <param name="inner">The real store every call is forwarded to.</param>
    private sealed class GatedStore(IChatStore inner) : IChatStore
    {
        private readonly TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int appends;

        /// <summary>Completes when the first append has started and is parked.</summary>
        public Task FirstEntered => this.firstEntered.Task;

        /// <summary>Completes when a second append has started, which the lock must prevent while the first is parked.</summary>
        public Task SecondEntered => this.secondEntered.Task;

        /// <summary>Lets the parked first append go on.</summary>
        public void ReleaseFirst()
        {
            _ = this.releaseFirst.TrySetResult();
        }

        /// <inheritdoc/>
        public async Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref this.appends) == 1)
            {
                _ = this.firstEntered.TrySetResult();
                await this.releaseFirst.Task.WaitAsync(BoundedWait, ct);
            }
            else
            {
                _ = this.secondEntered.TrySetResult();
            }

            await inner.AppendAsync(roomId, message, ct);
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

    /// <summary>A real <see cref="ChatService"/> over a temporary data directory, with the stores a test reads back.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;

        private Fixture(TempDataDir dir, SqliteTeamDirectory team, ChatService service, RoomEvents events, QuestionStore questions, FileChatStore transcript)
        {
            this.dir = dir;
            this.Team = team;
            this.Service = service;
            this.Events = events;
            this.Questions = questions;
            this.Transcript = transcript;
        }

        /// <summary>The Team Directory the service reads Rooms and Members from.</summary>
        public SqliteTeamDirectory Team { get; }

        /// <summary>The service under test.</summary>
        public ChatService Service { get; }

        /// <summary>The hub the service publishes on.</summary>
        public RoomEvents Events { get; }

        /// <summary>The waiting cards the service drops.</summary>
        public QuestionStore Questions { get; }

        /// <summary>The on-disk Transcript, read directly rather than through the service.</summary>
        public FileChatStore Transcript { get; }

        /// <summary>Builds the fixture.</summary>
        /// <param name="agentMessageBudget">The per-Room Budget the service is configured with.</param>
        /// <param name="wrapStore">Wraps the real Transcript store the service appends to, or <see langword="null"/> to use it as is.</param>
        /// <param name="ct">Cancels the Team Directory's initialisation.</param>
        public static async Task<Fixture> CreateAsync(int agentMessageBudget, Func<IChatStore, IChatStore>? wrapStore, CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = new(dir.Options());
            await team.InitializeAsync("You", ct);
            FileChatStore transcript = new(dir.Options(), NullLogger<FileChatStore>.Instance);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            QuestionStore questions = new(events);
            IChatStore appendTarget = wrapStore is null ? transcript : wrapStore(transcript);
            ChatService service = new(
                team,
                appendTarget,
                events,
                new FakeMentionAliasSource { Aliases = [] },
                Options.Create(new TeamOptions { AgentMessageBudget = agentMessageBudget }),
                new ProposalStore(events),
                questions,
                NullLogger<ChatService>.Instance);
            return new Fixture(dir, team, service, events, questions, transcript);
        }

        /// <summary>Registers an Agent User named <paramref name="name"/>.</summary>
        /// <param name="name">The Agent's Name.</param>
        /// <param name="ct">Cancels the write.</param>
        public async Task<User> AddAgentAsync(string name, CancellationToken ct)
        {
            User? agent = await this.Team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }

        /// <summary>Creates a Room holding the Human and <paramref name="agents"/>.</summary>
        /// <param name="agents">The Agents in the Room.</param>
        /// <param name="ct">Cancels the write.</param>
        public async Task<Room> CreateRoomAsync(IReadOnlyList<User> agents, CancellationToken ct)
        {
            List<string> memberIds = [KnownIds.Human, .. agents.Select(agent => agent.Id)];
            return await this.Team.CreateRoomAsync(string.Join(", ", agents.Select(agent => agent.Name)), memberIds, ct);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.dir.Dispose();
        }
    }
}
