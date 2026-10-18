using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// The real collaborators the Elicitation service, bridge and the <see cref="ChatService"/> drops run
/// against, in one <see cref="TempDataDir"/>: a Team Directory (Human Name <c>"You"</c>), a file
/// Transcript store, the hub, both card stores and a <see cref="ChatService"/> wired to them.
/// </summary>
internal sealed class ElicitationFixture : IDisposable
{
    private readonly TempDataDir dataDir;

    private ElicitationFixture(
        TempDataDir dataDir,
        SqliteTeamDirectory directory,
        FileChatStore transcript,
        RoomEvents events,
        QuestionStore questions,
        ElicitationStore elicitations,
        ChatService chat)
    {
        this.dataDir = dataDir;
        this.Directory = directory;
        this.Transcript = transcript;
        this.Events = events;
        this.Questions = questions;
        this.Elicitations = elicitations;
        this.Chat = chat;
        this.ServiceLog = new RecordingLogger<ElicitationService>();
        this.BridgeLog = new RecordingLogger<RoomElicitationBridge>();
        this.Service = new ElicitationService(elicitations, chat, directory, this.ServiceLog);
        this.Bridge = new RoomElicitationBridge(elicitations, directory, this.BridgeLog);
    }

    /// <summary>The Team Directory a test arranges Rooms and Members through.</summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary>The on-disk Transcript, read directly rather than through the service.</summary>
    public FileChatStore Transcript { get; }

    /// <summary>The hub the service and the stores publish on.</summary>
    public RoomEvents Events { get; }

    /// <summary>The Questions cards <see cref="ChatService"/> drops on a Human Message.</summary>
    public QuestionStore Questions { get; }

    /// <summary>The store the cards wait in.</summary>
    public ElicitationStore Elicitations { get; }

    /// <summary>The <see cref="ChatService"/>, constructed with <see cref="Elicitations"/>.</summary>
    public ChatService Chat { get; }

    /// <summary>The service that answers and skips cards.</summary>
    public ElicitationService Service { get; }

    /// <summary>What <see cref="Service"/> logged.</summary>
    public RecordingLogger<ElicitationService> ServiceLog { get; }

    /// <summary>The bridge a Room Session hands requests to.</summary>
    public RoomElicitationBridge Bridge { get; }

    /// <summary>What <see cref="Bridge"/> logged.</summary>
    public RecordingLogger<RoomElicitationBridge> BridgeLog { get; }

    /// <summary>Builds a fixture with an initialised Team Directory.</summary>
    /// <param name="ct">Cancels initialisation.</param>
    /// <param name="wrapStore">Wraps the real Transcript store <see cref="Chat"/> appends to, or <see langword="null"/> to use it as is.</param>
    public static async Task<ElicitationFixture> CreateAsync(CancellationToken ct, Func<IChatStore, IChatStore>? wrapStore = null)
    {
        TempDataDir dataDir = new();
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dataDir.Path });
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);
        FileChatStore transcript = new(options, NullLogger<FileChatStore>.Instance);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore questions = new(events);
        ElicitationStore elicitations = new(events);
        IChatStore appendTarget = wrapStore is null ? transcript : wrapStore(transcript);
        ChatService chat = new(
            directory,
            appendTarget,
            events,
            new FakeMentionAliasSource(),
            options,
            new ProposalStore(events),
            questions,
            NullLogger<ChatService>.Instance,
            elicitations: elicitations);

        return new ElicitationFixture(dataDir, directory, transcript, events, questions, elicitations, chat);
    }

    /// <summary>Registers an Agent User under <paramref name="name"/>.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="ct">Cancels the write.</param>
    public async Task<User> AddAgentAsync(string name, CancellationToken ct)
    {
        User? agent = await this.Directory.UpsertAgentUserAsync(name, null, ct);
        Assert.NotNull(agent);
        return agent;
    }

    /// <summary>Creates a Room holding the Human and <paramref name="agents"/>.</summary>
    /// <param name="agents">The Agents in the Room.</param>
    /// <param name="ct">Cancels the write.</param>
    public async Task<Room> CreateRoomAsync(IReadOnlyList<User> agents, CancellationToken ct)
    {
        List<string> memberIds = [KnownIds.Human, .. agents.Select(agent => agent.Id)];
        return await this.Directory.CreateRoomAsync(string.Join(", ", agents.Select(agent => agent.Name)), memberIds, ct);
    }

    /// <summary>Adds a card for the one-question form, asked by <paramref name="asker"/>, waiting in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="asker">The asking Agent.</param>
    public PendingElicitation AddCard(string roomId, User asker)
    {
        return this.Elicitations.Add(
            roomId,
            asker.Id,
            asker.Name,
            ElicitationTestSupport.FormOf(ElicitationTestSupport.SingleQuestionSchema, ElicitationTestSupport.SingleQuestionMessage));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.dataDir.Dispose();
    }
}
