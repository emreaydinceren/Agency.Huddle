using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

public sealed record ComposerResult(ChatMessage? Posted, string? Info);

/// <summary>
/// One Room's Budget: how many agent-authored Messages it has taken since the Human last spoke there,
/// and how many it currently allows. <paramref name="Granted"/> is not the configured default — the
/// Human can extend it a Budget at a time — which is why both halves travel together everywhere they
/// go, including onto the wire.
/// </summary>
/// <param name="Used">Agent-authored Messages taken since the Human last spoke in this Room.</param>
/// <param name="Granted">How many this Room currently allows. Zero or less means uncapped.</param>
public sealed record RoomBudget(int Used, int Granted)
{
    /// <summary>Whether the Room has spent its Budget and will take no more until a Human speaks.</summary>
    public bool Exhausted => this.Granted > 0 && this.Used >= this.Granted;
}

public sealed partial class ChatService
{
    // The Name is captured as "everything after /invite", not by shape, because a Name may hold
    // spaces and a pattern that spelled one out would silently truncate "@Emily Lee" to "Emily"
    // and then report the wrong Name as unknown. Nothing is lost by being permissive here:
    // InviteAsync resolves the Name against the Team Directory and rejects what it cannot find.
    //
    // A matchTimeout is passed so a pathological composer input can never hang the match itself
    // (S6444): the pattern is simple and bounded, so any real match completes in microseconds and
    // this timeout is never expected to trigger in normal use.
    private static readonly TimeSpan InviteCommandMatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex InviteCommandRegex =
        new(
            @"\A/invite\s+@?(\S.*?)\s*\z",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            InviteCommandMatchTimeout);

    private readonly ITeamDirectory teamDirectory;
    private readonly IChatStore store;
    private readonly RoomEvents events;
    private readonly IMentionAliasSource aliasSource;
    private readonly ILogger<ChatService> logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> postLocks = new(StringComparer.Ordinal);

    // Read and written only inside that Room's postLocks semaphore. The dictionary is concurrent
    // because Rooms are independent of one another, NOT because the counter is safe to race: deciding
    // whether a post fits the Budget is check-then-act, and no atomic dictionary operation makes that
    // atomic. See PostAsync.
    private readonly ConcurrentDictionary<string, RoomBudget> budgets = new(StringComparer.Ordinal);

    private readonly int agentMessageBudget;

    /// <param name="teamDirectory">The Team's Users and Rooms.</param>
    /// <param name="store">Where a posted Message's transcript is appended.</param>
    /// <param name="events">The hub a posted Message or a Room change is published on.</param>
    /// <param name="aliasSource">
    /// Every Persona's Alias, library-wide - folded into <see cref="MentionParser"/>'s candidate list
    /// at <see cref="PostAsync"/>, and consulted by <see cref="InviteAsync"/> when a typed handle does
    /// not match any Name directly.
    /// </param>
    /// <param name="options">Supplies the per-Room Budget, <see cref="TeamOptions.AgentMessageBudget"/>.</param>
    /// <param name="logger">Used to log Room creation and Invitation events.</param>
    public ChatService(
        ITeamDirectory teamDirectory,
        IChatStore store,
        RoomEvents events,
        IMentionAliasSource aliasSource,
        IOptions<TeamOptions> options,
        ILogger<ChatService> logger)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(aliasSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.teamDirectory = teamDirectory;
        this.store = store;
        this.events = events;
        this.aliasSource = aliasSource;
        this.agentMessageBudget = options.Value.AgentMessageBudget;
        this.logger = logger;
    }

    public async Task<ChatMessage> PostAsync(
        string roomId, string senderId, string text, string? messageId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.TrimEnd().Length == 0)
        {
            throw new ChatException(ErrorCodes.BadMessage, "empty message");
        }

        var room = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var sender = members.FirstOrDefault(m => m.Id == senderId)
            ?? throw new ChatException(ErrorCodes.NotMember, $"'{senderId}' is not a member of room '{roomId}'.");

        var semaphore = this.postLocks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            // Everything about the Budget happens in here, and that is load-bearing. Deciding whether
            // a post fits is check-then-act: two concurrent Agent posts that both read Used one below
            // the Budget would both conclude they fit, and the Room would take one more Message than
            // it granted. This semaphore is what makes read-decide-write atomic - the dictionary is
            // not. It is also what already keeps transcript order equal to event order, so counting
            // anywhere else would let the count describe no prefix of the Transcript.
            var isAgent = sender.Kind == UserKind.Agent;
            var budget = this.CurrentBudget(roomId);

            if (isAgent && budget.Exhausted)
            {
                ChatService.LogBudgetExhausted(this.logger, sender.Name, roomId, budget.Granted);
                throw new ChatException(ErrorCodes.BudgetExhausted, ChatService.BudgetRefusal(budget.Granted));
            }

            var id = messageId ?? Guid.CreateVersion7().ToString("N");
            var mentions = MentionParser.Parse(text, members, this.aliasSource.Aliases);
            var message = new ChatMessage(id, DateTimeOffset.UtcNow, sender.Id, sender.Name, text);
            await this.store.AppendAsync(roomId, message, ct);

            // Only a Message that reached the Transcript spends Budget, so a failed append above
            // leaves the count where it was. A Human Message resets both halves: any extension the
            // Human granted expires with the run it was granted for.
            budget = isAgent
                ? budget with { Used = budget.Used + 1 }
                : new RoomBudget(0, this.agentMessageBudget);
            this.budgets[roomId] = budget;

            this.events.PublishMessagePosted(new MessagePostedEvent(room, message, members, mentions, budget));
            return message;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// This Room's Budget as it stands. Safe to call from anywhere — the Room view reads it on every
    /// render, which is the path no event can serve: a Blazor prerender and a reconnected circuit both
    /// need the current figure without having witnessed the Message that produced it.
    /// </summary>
    /// <param name="roomId">The Room to report on.</param>
    /// <returns>The Room's Budget, fresh if it has taken no agent Messages yet.</returns>
    public RoomBudget GetBudget(string roomId)
    {
        return this.CurrentBudget(roomId);
    }

    /// <summary>
    /// Grants a paused Room one more Budget and wakes it, by delivering its most recent Message to the
    /// Agents a second time.
    /// <para>
    /// The re-delivery is the whole of it. Raising the allowance alone changes a number nothing will
    /// read: a Turn only ever begins with a delivered Message, and at the pause the Agent that would
    /// have replied has already declined this one and queued nothing. The Message goes out on
    /// <see cref="RoomEvents.MessageRedelivered"/> rather than <see cref="RoomEvents.MessagePosted"/>
    /// so the Room view does not show it twice; an Agent cannot tell the two apart, which is right,
    /// because it is the same Message.
    /// </para>
    /// </summary>
    /// <param name="roomId">The paused Room to extend.</param>
    /// <param name="ct">Cancels the lookup of the Room, its Members and its last Message.</param>
    /// <returns>
    /// <see langword="true"/> if the Room was extended and woken. <see langword="false"/> if it was not
    /// paused or holds no Message to re-deliver — so a second click grants nothing.
    /// </returns>
    public async Task<bool> ExtendBudgetAsync(string roomId, CancellationToken ct = default)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var history = await this.store.ReadAllAsync(roomId, ct);
        if (history.Count == 0)
        {
            return false;
        }

        var last = history[^1];

        var semaphore = this.postLocks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        RoomBudget extended;
        try
        {
            var budget = this.CurrentBudget(roomId);
            if (!budget.Exhausted)
            {
                return false;
            }

            extended = budget with { Granted = budget.Granted + this.agentMessageBudget };
            this.budgets[roomId] = extended;
        }
        finally
        {
            semaphore.Release();
        }

        ChatService.LogBudgetExtended(this.logger, roomId, extended.Granted);

        // Re-parsed rather than remembered, so the Mentions that woke an Agent the first time wake the
        // same one now. Published outside the semaphore: delivery reaches the pipe, and holding a
        // Room's post lock across that would block every other writer to the Room.
        var mentions = MentionParser.Parse(last.Text, members, this.aliasSource.Aliases);
        this.events.PublishMessageRedelivered(new MessagePostedEvent(room, last, members, mentions, extended));
        return true;
    }

    /// <summary>The wording a refused Agent reads. One copy, so both doors into a post carry it.</summary>
    /// <param name="granted">How many agent Messages the Room allowed.</param>
    /// <returns>Text a model should read as final rather than as worth retrying.</returns>
    private static string BudgetRefusal(int granted)
    {
        return $"This room has reached its budget of {granted} agent messages since a human last spoke. " +
            "Do not retry: further posts to this room will be refused until a human speaks here.";
    }

    private RoomBudget CurrentBudget(string roomId)
    {
        return this.budgets.TryGetValue(roomId, out var budget)
            ? budget
            : new RoomBudget(0, this.agentMessageBudget);
    }

    public async Task<ComposerResult> SubmitFromComposerAsync(
        string roomId, string humanId, string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var t = text.Trim();
        if (t.Length == 0)
        {
            throw new ChatException(ErrorCodes.BadMessage, "empty message");
        }

        if (t.StartsWith('/'))
        {
            var match = InviteCommandRegex.Match(t);
            if (!match.Success)
            {
                throw new ChatException(ErrorCodes.BadMessage, "Unknown command");
            }

            var agentName = match.Groups[1].Value;
            var room = await this.InviteAsync(roomId, agentName, ct);
            return new ComposerResult(null, $"Invited {agentName}. Room is now \"{room.Name}\".");
        }

        var message = await this.PostAsync(roomId, humanId, t, ct: ct);
        return new ComposerResult(message, null);
    }

    /// <summary>
    /// Returns the Room whose Members are exactly the Human and this Agent, creating it if there is
    /// none. Called on every registration, so an Agent that reconnects re-attaches to the Room it
    /// already had rather than gaining a second one.
    /// </summary>
    public async Task<Room> EnsureRoomForAsync(User agent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var existing = await this.teamDirectory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, ct);
        if (existing is not null)
        {
            return existing;
        }

        var room = await this.teamDirectory.CreateRoomAsync(agent.Name, [KnownIds.Human, agent.Id], ct);
        ChatService.LogCreatedDirectRoom(this.logger, room.Id, agent.Name);
        this.events.PublishRoomsChanged();
        return room;
    }

    /// <summary>
    /// Creates a Room holding the Human and the named Agents. A single Agent reuses that Agent's
    /// existing Room, so this is the one entry point for starting any conversation.
    /// </summary>
    public async Task<Room> CreateRoomForAsync(IReadOnlyList<string> agentIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        if (agentIds.Count == 0)
        {
            throw new ChatException(ErrorCodes.BadMessage, "select at least one agent");
        }

        var agents = new List<User>(agentIds.Count);
        foreach (var agentId in agentIds)
        {
            var user = await this.teamDirectory.GetUserAsync(agentId, ct);
            if (user is null || user.Kind != UserKind.Agent)
            {
                throw new ChatException(ErrorCodes.BadMessage, $"Unknown agent '{agentId}'.");
            }

            agents.Add(user);
        }

        if (agents.Count == 1)
        {
            return await this.EnsureRoomForAsync(agents[0], ct);
        }

        var name = string.Join(", ", agents.Select(a => a.Name));
        var memberIds = new List<string>(agents.Count + 1) { KnownIds.Human };
        memberIds.AddRange(agents.Select(a => a.Id));
        var room = await this.teamDirectory.CreateRoomAsync(name, memberIds, ct);
        ChatService.LogCreatedRoom(this.logger, room.Id, room.Name, memberIds.Count);
        this.events.PublishRoomsChanged();
        return room;
    }

    public async Task<Room> InviteAsync(string roomId, string agentName, CancellationToken ct = default)
    {
        _ = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var user = await this.teamDirectory.FindUserByNameAsync(agentName, ct);
        if (user is null || user.Kind != UserKind.Agent)
        {
            // agentName may be a Persona's Alias rather than its Name - docs/agencyteam/traps.md is
            // explicit that anything resolving a typed Name must resolve it against the Members (or
            // here, the Team) rather than reject it outright, and an Alias is just the Persona's
            // second, shorter spelling of the same identity. PersonaIndex guarantees Aliases are
            // unique library-wide and never equal a DIFFERENT Persona's Name, so at most one Alias can
            // match here; resolve it back to the owning Name and retry exactly once.
            var alias = this.aliasSource.Aliases.FirstOrDefault(a => string.Equals(a.Alias, agentName, StringComparison.OrdinalIgnoreCase));
            if (alias is not null)
            {
                user = await this.teamDirectory.FindUserByNameAsync(alias.Name, ct);
            }
        }

        if (user is null || user.Kind != UserKind.Agent)
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Unknown agent @{agentName}");
        }

        await this.teamDirectory.AddMemberAsync(roomId, user.Id, ct);

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var name = string.Join(", ", members.Where(m => m.Kind == UserKind.Agent).Select(m => m.Name));
        await this.teamDirectory.RenameRoomAsync(roomId, name, ct);
        ChatService.LogInvitedAgent(this.logger, user.Name, user.Id, roomId);
        this.events.PublishRoomsChanged();

        return await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
    }

    /// <summary>Logs that a direct Room was created for an Agent.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The id of the created Room.</param>
    /// <param name="agentName">The name of the Agent the Room was created for.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Created direct room '{RoomId}' for agent '{AgentName}'.")]
    private static partial void LogCreatedDirectRoom(ILogger logger, string roomId, string agentName);

    /// <summary>Logs that a multi-member Room was created.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The id of the created Room.</param>
    /// <param name="roomName">The name of the created Room.</param>
    /// <param name="memberCount">The number of members in the created Room.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Created room '{RoomId}' ({RoomName}) with {MemberCount} members.")]
    private static partial void LogCreatedRoom(ILogger logger, string roomId, string roomName, int memberCount);

    /// <summary>Logs that an Agent was invited into a Room.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="agentName">The name of the invited Agent.</param>
    /// <param name="agentId">The id of the invited Agent.</param>
    /// <param name="roomId">The id of the Room the Agent was invited into.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Invited agent '{AgentName}' ({AgentId}) into room '{RoomId}'.")]
    private static partial void LogInvitedAgent(ILogger logger, string agentName, string agentId, string roomId);

    /// <summary>Logs that a Room refused an agent-authored Message because its Budget was spent.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="agentName">The name of the refused Agent.</param>
    /// <param name="roomId">The id of the Room that refused it.</param>
    /// <param name="granted">How many agent Messages the Room allowed.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Room '{RoomId}' refused a message from '{AgentName}': its budget of {Granted} agent messages since a human last spoke is spent.")]
    private static partial void LogBudgetExhausted(ILogger logger, string agentName, string roomId, int granted);

    /// <summary>Logs that the Human granted a paused Room more Budget.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The id of the extended Room.</param>
    /// <param name="granted">The Room's new allowance.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Room '{RoomId}' was extended to {Granted} agent messages.")]
    private static partial void LogBudgetExtended(ILogger logger, string roomId, int granted);
}