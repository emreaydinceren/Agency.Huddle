using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teammates;
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

/// <summary>
/// What <see cref="ChatService.ExtendBudgetAsync"/> actually did. A caller that only reads the old
/// <see langword="bool"/> return cannot tell "nothing to redeliver" from "not paused" from "granted" -
/// all three rendered as the same silence in the Room view, which is issue #40. Naming the three
/// outcomes lets a later phase explain the one the Human is looking at instead of guessing.
/// </summary>
public enum ExtendResult
{
    /// <summary>The Room was paused, is now extended, and its last Message went out again.</summary>
    Granted,

    /// <summary>The Room was not paused, so there was nothing for an extension to grant.</summary>
    NotPaused,

    /// <summary>The Room holds no Message at all, so there is nothing an extension could redeliver.</summary>
    NothingToRedeliver,
}

/// <summary>
/// The three facts the Reply Gate needs about a re-delivered Message, deliberately not the
/// <see cref="MessagePostedEvent"/> itself. <see cref="RoomEvents.MessageRedelivered"/>'s own doc
/// records that no Blazor component may subscribe to it - a component that handled the envelope could
/// render the Message a second time. Handing the caller the envelope would put that same mistake one
/// careless <c>messages.Add(...)</c> away; handing it only these three facts removes the temptation
/// rather than policing it with a comment and a guard test.
/// </summary>
/// <param name="Members">The Room's Members at the moment of re-delivery.</param>
/// <param name="Mentions">Who the re-delivered Message names, re-parsed fresh so it matches exactly who it woke the first time.</param>
/// <param name="SenderId">The re-delivered Message's original sender.</param>
public sealed record RedeliveryFacts(
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    string SenderId);

/// <summary>
/// The outcome of <see cref="ChatService.ExtendBudgetAsync"/>: what happened, the Room's Budget
/// afterward, and - only when something was actually redelivered - the facts of that delivery.
/// <see cref="Redelivered"/> is non-null exactly when <see cref="Result"/> is <see cref="ExtendResult.Granted"/>.
/// The caller applies the Reply Gate itself; <see cref="ChatService"/> must never decide whether anyone
/// will reply - ADR-0003, ADR-0004 and ADR-0005 each reject a server-computed <c>shouldReply</c>, because
/// the server labels, it never decides.
/// </summary>
/// <param name="Result">Which of the three outcomes occurred.</param>
/// <param name="Budget">The Room's Budget after the call, whichever outcome occurred.</param>
/// <param name="Redelivered">The facts of the re-delivered Message, or <see langword="null"/> when nothing was redelivered.</param>
public sealed record BudgetExtension(
    ExtendResult Result,
    RoomBudget Budget,
    RedeliveryFacts? Redelivered);

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
    private readonly ProposalStore proposals;
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
    /// <param name="proposals">
    /// Holds each Room's pending Proposal (Spec §6.9). Dropped from <see cref="SetRoomArchivedAsync"/>
    /// (archiving only, never unarchiving) and <see cref="DeleteRoomAsync"/>, after either succeeds -
    /// Spec §12 F-16: a Room the sidebar no longer offers must not go on showing a Proposal card.
    /// </param>
    /// <param name="logger">Used to log Room creation and Invitation events.</param>
    public ChatService(
        ITeamDirectory teamDirectory,
        IChatStore store,
        RoomEvents events,
        IMentionAliasSource aliasSource,
        IOptions<TeamOptions> options,
        ProposalStore proposals,
        ILogger<ChatService> logger)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(aliasSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(logger);

        this.teamDirectory = teamDirectory;
        this.store = store;
        this.events = events;
        this.aliasSource = aliasSource;
        this.agentMessageBudget = options.Value.AgentMessageBudget;
        this.proposals = proposals;
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
    /// The Room's Budget after the call, and which of three things happened:
    /// <see cref="ExtendResult.NothingToRedeliver"/> if the Room holds no Message at all;
    /// <see cref="ExtendResult.NotPaused"/> if it was not paused, so there was nothing to grant - a
    /// second click grants nothing; or <see cref="ExtendResult.Granted"/> if it was extended and woken,
    /// in which case <see cref="BudgetExtension.Redelivered"/> carries the facts of that re-delivery.
    /// </returns>
    public async Task<BudgetExtension> ExtendBudgetAsync(string roomId, CancellationToken ct = default)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var history = await this.store.ReadAllAsync(roomId, ct);
        if (history.Count == 0)
        {
            // Outside the per-Room semaphore, and deliberately so - there is no Budget decision to make
            // atomic here, only a display value to report. This matches how GetBudget already reads for
            // display; do not "fix" this by moving it inside the lock below.
            return new BudgetExtension(ExtendResult.NothingToRedeliver, this.CurrentBudget(roomId), null);
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
                return new BudgetExtension(ExtendResult.NotPaused, budget, null);
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
        var redelivered = new RedeliveryFacts(members, mentions, last.SenderId);
        return new BudgetExtension(ExtendResult.Granted, extended, redelivered);
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

        var room = await this.teamDirectory.CreateRoomAsync(RoomNaming.Derive([agent]), [KnownIds.Human, agent.Id], ct);
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

        var name = RoomNaming.Derive(agents);
        var memberIds = new List<string>(agents.Count + 1) { KnownIds.Human };
        memberIds.AddRange(agents.Select(a => a.Id));
        var room = await this.teamDirectory.CreateRoomAsync(name, memberIds, ct);
        ChatService.LogCreatedRoom(this.logger, room.Id, room.Name, memberIds.Count);
        this.events.PublishRoomsChanged();
        return room;
    }

    /// <summary>
    /// Adds an Agent to a Room that already exists. All three doors into an Invitation - <c>/invite</c>
    /// in the composer, the <b>Add teammate</b> control on the Room header, and an Agent's own
    /// <c>mcp__team__invite_agent</c> call - end here, so all three inherit this method's behaviour,
    /// including the naming rule below.
    /// </summary>
    /// <remarks>
    /// A Room the Human renamed through <see cref="RenameRoomAsync"/> is the fix for the duplicate-name
    /// problem recorded in <c>docs/engineering/product-observations.md</c> §6 - a manual test run found
    /// 14 Rooms in the sidebar, five of them showing the indistinguishable auto-derived name
    /// "Nova, Jarvis". Re-deriving the name on every Invitation regardless would throw that chosen name
    /// away on the very next <c>mcp__team__invite_agent</c> call, so this method compares the Room's
    /// name, before adding the new Member, against what <see cref="RoomNaming.Derive"/> would have
    /// produced for its Members at that point. Only when the two agree - meaning nobody has renamed the
    /// Room away from its auto-name - does the Invitation re-derive and apply a new one.
    /// </remarks>
    /// <param name="roomId">The Room to invite the Agent into.</param>
    /// <param name="agentName">The Agent's Name or Alias.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    /// <returns>The Room after the Invitation.</returns>
    public async Task<Room> InviteAsync(string roomId, string agentName, CancellationToken ct = default)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var user = await this.teamDirectory.FindUserByNameAsync(agentName, ct);
        if (user is null || user.Kind != UserKind.Agent)
        {
            // agentName may be a Persona's Alias rather than its Name - docs/engineering/traps.md is
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

        var before = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var wasAutoNamed = string.Equals(room.Name, RoomNaming.Derive(before), StringComparison.Ordinal);

        await this.teamDirectory.AddMemberAsync(roomId, user.Id, ct);

        if (wasAutoNamed)
        {
            var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
            await this.teamDirectory.RenameRoomAsync(roomId, RoomNaming.Derive(members), ct);
        }

        ChatService.LogInvitedAgent(this.logger, user.Name, user.Id, roomId);
        this.events.PublishRoomsChanged();

        return await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
    }

    /// <summary>
    /// Gives a Room a Human-chosen name, which stays in place across later Invitations - see the
    /// remarks on <see cref="InviteAsync"/> for why that matters. A Room name is free display text: it
    /// is never resolved against, so unlike a Teammate Name it is not run through <see cref="NameRules"/>.
    /// </summary>
    /// <param name="roomId">The Room to rename.</param>
    /// <param name="name">
    /// The new name. Blank or whitespace-only is rejected, and surrounding whitespace is trimmed
    /// before it is stored: a Room named <c>" Pricing "</c> is indistinguishable in the sidebar from
    /// one named <c>"Pricing"</c>, which is the very confusion a Human-chosen name exists to remove.
    /// </param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    /// <returns>The Room after the rename.</returns>
    public async Task<Room> RenameRoomAsync(string roomId, string name, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ChatException(ErrorCodes.BadMessage, "A room name cannot be blank.");
        }

        _ = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        await this.teamDirectory.RenameRoomAsync(roomId, trimmed, ct);
        this.events.PublishRoomsChanged();

        return await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
    }

    /// <summary>
    /// Archives or unarchives a Room. Archiving is a display filter only - see
    /// <see cref="Room.Archived"/> - the Room stays live and Agents can still post into it.
    /// </summary>
    /// <param name="roomId">The Room to archive or unarchive.</param>
    /// <param name="archived">
    /// <see langword="true"/> to archive the Room; <see langword="false"/> to unarchive it.
    /// </param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    /// <returns>The Room after the change, so the caller sees the new <see cref="Room.Archived"/>.</returns>
    public async Task<Room> SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default)
    {
        _ = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        await this.teamDirectory.SetRoomArchivedAsync(roomId, archived, ct);

        // Never for unarchiving: an unarchived Room is exactly as live as it was, so a Proposal still
        // pending in it is still answerable. Spec §12 F-16 only drops on archiving true and on delete.
        if (archived)
        {
            this.proposals.Drop(roomId);
        }

        ChatService.LogRoomArchived(this.logger, roomId, archived);
        this.events.PublishRoomsChanged();

        return await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
    }

    /// <summary>
    /// Permanently deletes a Room: its Team Directory row, its Membership rows and its Transcript
    /// file. Both stores must be told - see <see cref="IChatStore.DeleteAsync"/>'s remarks - or an
    /// orphan Transcript file survives with nothing left that can ever reach it.
    /// </summary>
    /// <param name="roomId">The Room to delete.</param>
    /// <param name="ct">Cancels the lookup and the deletes.</param>
    public async Task DeleteRoomAsync(string roomId, CancellationToken ct = default)
    {
        _ = await this.teamDirectory.GetRoomAsync(roomId, ct)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        await this.teamDirectory.DeleteRoomAsync(roomId, ct);
        await this.store.DeleteAsync(roomId, ct);

        // Spec §12 F-16: a deleted Room can never be Approved or Declined into, so any Proposal still
        // pending in it is dropped along with everything else below.
        this.proposals.Drop(roomId);

        // Both budgets and postLocks are keyed by roomId and now describe a Room that no longer
        // exists, so drop them. Drafts and RoomFollows are deliberately left untouched: both are
        // in-memory, both die on restart, and both are only ever read by room id - an id that is now
        // unreachable, so their entries for it are inert. Reaching them would mean injecting
        // RoomFollows (an internal Acp type) into ChatService for no observable gain.
        this.budgets.TryRemove(roomId, out _);
        this.postLocks.TryRemove(roomId, out _);

        ChatService.LogRoomDeleted(this.logger, roomId);
        this.events.PublishRoomsChanged();
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

    /// <summary>Logs that a Room's archived state changed.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The id of the Room whose archived state changed.</param>
    /// <param name="archived">The Room's archived state after the change.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Room '{RoomId}' archived set to {Archived}.")]
    private static partial void LogRoomArchived(ILogger logger, string roomId, bool archived);

    /// <summary>Logs that a Room was permanently deleted.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roomId">The id of the deleted Room.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Room '{RoomId}' was deleted.")]
    private static partial void LogRoomDeleted(ILogger logger, string roomId);
}