using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>
/// A real <see cref="ChatService"/>, Team Directory, Transcript, Question and Form stores and
/// <see cref="NeedsYouTracker"/> wired together over one <see cref="RoomEvents"/>, so a test drives the
/// tracker through the events the app really raises rather than through hand-built ones.
/// </summary>
internal sealed class NeedsYouFixture : IDisposable
{
    private readonly TempDataDir dir;

    private NeedsYouFixture(TempDataDir dir, Agency.Huddle.App.Feed.IFeedPublisher? feed, bool persistQuestions)
    {
        this.dir = dir;
        this.Events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        this.Team = dir.CreateDirectory();
        this.Transcript = dir.CreateChatStore();
        this.Questions = persistQuestions
            ? new QuestionStore(this.Events, new Agency.Huddle.App.Data.WaitingQuestionStore(dir.Options()))
            : new QuestionStore(this.Events);
        this.Forms = new ElicitationStore(this.Events);
        this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        this.Service = new ChatService(
            this.Team,
            this.Transcript,
            this.Events,
            new FakeMentionAliasSource(),
            TestOptions.Create(opts => opts.AgentMessageBudget = 3),
            new ProposalStore(this.Events),
            this.Questions,
            NullLogger<ChatService>.Instance,
            this.Forms,
            timeProvider: this.Clock);
        this.Tracker = new NeedsYouTracker(this.Events, this.Questions, this.Forms, this.Team, NullLogger<NeedsYouTracker>.Instance, this.Clock, feed);
    }

    /// <summary>The temporary data directory, for a store a test opens beside the fixture's.</summary>
    public TempDataDir Directory => this.dir;

    /// <summary>The hub everything publishes on.</summary>
    public RoomEvents Events { get; }

    /// <summary>The real Team Directory.</summary>
    public SqliteTeamDirectory Team { get; }

    /// <summary>The on-disk Transcript.</summary>
    public FileChatStore Transcript { get; }

    /// <summary>The Question card store.</summary>
    public QuestionStore Questions { get; }

    /// <summary>The Form store.</summary>
    public ElicitationStore Forms { get; }

    /// <summary>The clock the service and tracker read.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>The service tests post through. Its per-Room Budget is 3.</summary>
    public ChatService Service { get; }

    /// <summary>The tracker under test.</summary>
    public NeedsYouTracker Tracker { get; }

    /// <summary>Builds and initialises a fixture.</summary>
    /// <param name="ct">Cancels the directory's initialisation.</param>
    /// <param name="feed">Receives the tracker's Feed items; <see langword="null"/> posts nothing.</param>
    /// <param name="persistQuestions"><see langword="true"/> to keep Question cards in <c>team.db</c> too (Turn Queue spec §5a).</param>
    /// <returns>A fixture the caller disposes.</returns>
    public static async Task<NeedsYouFixture> CreateAsync(CancellationToken ct, Agency.Huddle.App.Feed.IFeedPublisher? feed = null, bool persistQuestions = false)
    {
        NeedsYouFixture fixture = new(new TempDataDir(), feed, persistQuestions);
        await fixture.Team.InitializeAsync("You", ct);
        return fixture;
    }

    /// <summary>Registers an Agent named <paramref name="name"/>.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The Agent.</returns>
    public async Task<User> AddAgentAsync(string name, CancellationToken ct)
    {
        User? agent = await this.Team.UpsertAgentUserAsync(name, null, ct);
        Assert.NotNull(agent);
        return agent;
    }

    /// <summary>Creates a Room holding the Human and <paramref name="agents"/>.</summary>
    /// <param name="name">The Room's Name.</param>
    /// <param name="agents">The Agents in it.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The Room.</returns>
    public async Task<Room> CreateRoomAsync(string name, IReadOnlyList<User> agents, CancellationToken ct)
    {
        List<string> memberIds = [KnownIds.Human, .. agents.Select(agent => agent.Id)];
        return await this.Team.CreateRoomAsync(name, memberIds, ct);
    }

    /// <summary>Builds a Question card for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room it waits in.</param>
    /// <param name="agent">The asker.</param>
    /// <returns>The card.</returns>
    public static PendingQuestions Card(string roomId, User agent)
    {
        Question question = new("Which?", ["A", "B"], QuestionKind.SingleSelect);
        return new PendingQuestions(Guid.NewGuid().ToString("N"), roomId, agent.Id, agent.Name, [question], DateTimeOffset.UnixEpoch.AddDays(1));
    }

    /// <summary>Adds a Form that <paramref name="agent"/> is waiting on in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room it waits in.</param>
    /// <param name="agent">The asker.</param>
    /// <returns>The stored card.</returns>
    public PendingElicitation AddForm(string roomId, User agent)
    {
        return this.Forms.Add(roomId, agent.Id, agent.Name, new ElicitationForm("Pick", []));
    }

    /// <summary>Deletes the temporary data directory.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }
}
