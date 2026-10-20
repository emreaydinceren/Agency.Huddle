using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Services;

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
    private readonly QuestionStore questions;
    private readonly ElicitationStore? elicitations;
    private readonly IReferenceSnapshotter? references;
    private readonly ILogger<ChatService> logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> postLocks = new(StringComparer.Ordinal);

    // Read and written only inside that Room's postLocks semaphore. The dictionary is concurrent
    // because Rooms are independent of one another, NOT because the counter is safe to race: deciding
    // whether a post fits the Budget is check-then-act, and no atomic dictionary operation makes that
    // atomic. See PostAsync.
    private readonly ConcurrentDictionary<string, RoomBudget> budgets = new(StringComparer.Ordinal);

    private readonly int agentMessageBudget;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// The live Team list, used to refuse a Room scoped to a Team that does not exist. A settable property
    /// rather than a constructor parameter because <see cref="ITeamCatalog"/> is internal and this
    /// constructor is public; <c>ServiceCollectionExtensions</c> sets it right after
    /// <c>ActivatorUtilities</c> builds the service. <see langword="null"/> means no Team is known, so
    /// every Team-scoped call is refused.
    /// </summary>
    internal ITeamCatalog? TeamCatalog { get; set; }

    /// <summary>
    /// Where a Room's chat uploads live, so <see cref="DeleteRoomAsync"/> can recycle them and the delete
    /// confirmation can count them. An optional settable property for the same reason as
    /// <see cref="TeamCatalog"/>; <see langword="null"/> means uploads are not tracked and nothing is recycled.
    /// </summary>
    internal IRoomUploadFolders? UploadFolders { get; set; }

    /// <summary>
    /// Told when a Room is created, joined or archived (Feed spec §6.5.3). An optional settable property for the
    /// same reason as <see cref="TeamCatalog"/>: <see cref="IFeedPublisher"/> is internal and the constructor is public.
    /// <see langword="null"/> posts nothing.
    /// </summary>
    internal IFeedPublisher? FeedPublisher { get; set; }

    /// <summary>The number of uploaded files a delete of <paramref name="room"/> would send to the Recycle Bin.</summary>
    /// <param name="room">The Room about to be deleted.</param>
    /// <returns>The file count of both upload folders; 0 when none exist.</returns>
    public int CountRoomUploads(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return this.UploadFolders?.CountFiles(room) ?? 0;
    }

    /// <param name="teamDirectory">The Team's Users and Rooms.</param>
    /// <param name="store">Where a posted Message's transcript is appended.</param>
    /// <param name="events">The hub a posted Message or a Room change is published on.</param>
    /// <param name="aliasSource">
    /// Every Persona's Alias, library-wide - folded into <see cref="MentionParser"/>'s candidate list
    /// at <see cref="PostAsync"/>, and consulted by <see cref="InviteAsync(string, string, CancellationToken)"/> when a typed handle does
    /// not match any Name directly.
    /// </param>
    /// <param name="options">Supplies the per-Room Budget, <see cref="TeamOptions.AgentMessageBudget"/>.</param>
    /// <param name="proposals">
    /// Holds each Room's pending Proposal (Spec §6.9). Dropped from <see cref="SetRoomArchivedAsync(string, bool, CancellationToken)"/>
    /// (archiving only, never unarchiving) and <see cref="DeleteRoomAsync"/>, after either succeeds -
    /// Spec §12 F-16: a Room the sidebar no longer offers must not go on showing a Proposal card.
    /// </param>
    /// <param name="questions">
    /// Holds each Room's waiting Questions card (Questions spec §6.5). Dropped when the Human posts a
    /// Message in the Room, because that Message is the answer the asker will act on, and when the
    /// Room is archived or deleted.
    /// </param>
    /// <param name="logger">Used to log Room creation and Invitation events.</param>
    /// <param name="elicitations">
    /// Holds each Room's waiting forms (elicitation bridge, decision E-5). Every card of a Room is dropped -
    /// its request resolved as cancelled - when the Human types a Message there, and when the Room is
    /// archived or deleted. <see cref="PostHumanAnswerAsync"/> and an Agent's Message leave them alone:
    /// answering one form must not cancel another. Optional and last so every earlier caller compiles
    /// unchanged; <see langword="null"/> drops nothing.
    /// </param>
    /// <param name="references">
    /// Builds the reference snapshot every posted Message carries (spec §8.2). Optional so a caller that
    /// does not care compiles unchanged; <see langword="null"/> records no references.
    /// </param>
    /// <param name="timeProvider">
    /// The clock stamped onto each posted Message. Optional and last so every earlier caller compiles
    /// unchanged; <see langword="null"/> uses <see cref="TimeProvider.System"/>.
    /// </param>
    public ChatService(
        ITeamDirectory teamDirectory,
        IChatStore store,
        RoomEvents events,
        IMentionAliasSource aliasSource,
        IOptions<TeamOptions> options,
        ProposalStore proposals,
        QuestionStore questions,
        ILogger<ChatService> logger,
        ElicitationStore? elicitations = null,
        IReferenceSnapshotter? references = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(aliasSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(logger);

        this.teamDirectory = teamDirectory;
        this.store = store;
        this.events = events;
        this.aliasSource = aliasSource;
        this.agentMessageBudget = options.Value.AgentMessageBudget;
        this.proposals = proposals;
        this.questions = questions;
        this.elicitations = elicitations;
        this.references = references;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ChatMessage> PostAsync(
        string roomId, string senderId, string text, string? messageId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.TrimEnd().Length == 0)
        {
            throw new ChatException(ErrorCodes.BadMessage, "empty message");
        }

        return await this.PostCoreAsync(roomId, senderId, text, messageId, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts the Human's answer to a form the asker opened mid-Turn (elicitation bridge, decision E-4).
    /// It is an ordinary Human Message in every way that matters to the Room - it is appended to the
    /// Transcript, resets the Budget and drops the Room's waiting Questions, all under the same
    /// per-Room lock as <see cref="PostAsync"/> - with two differences. It mentions nobody, whatever
    /// its free text says, and it is withheld from the asker (<see cref="MessagePostedEvent.WithheldFromAgentId"/>).
    /// The asker's Turn is still open and receives the answer as the tool's own result; a delivery
    /// would queue a second Turn, and the Reply Gate cannot refuse it in a one-to-one Room, which it
    /// answers whether or not anyone was mentioned.
    /// </summary>
    /// <remarks>
    /// Accepted leaks, documented in <c>known-limits.md</c>: another Agent that follows the Room still
    /// wakes; the asker's own token Budget is not reset; <see cref="ExtendBudgetAsync"/> redelivers the
    /// Room's last Message without the withhold, which cannot reach the asker because the answer has
    /// just unpaused the Room; and on resume the asker's catch-up may include the answer as context.
    /// </remarks>
    /// <param name="roomId">The Room the asker is waiting in.</param>
    /// <param name="humanId">The Human, as a Member of that Room.</param>
    /// <param name="text">The answer. Blank or whitespace-only is nothing to record.</param>
    /// <param name="askerAgentId">The Agent that asked, and the only one this Message is withheld from.</param>
    /// <param name="ct">Cancels the lookups, the lock wait and the append.</param>
    /// <returns>The Message that was appended, or <see langword="null"/> when <paramref name="text"/> was blank and nothing was posted.</returns>
    /// <exception cref="ChatException">The Room does not exist, or <paramref name="humanId"/> is not a Member of it.</exception>
    public async Task<ChatMessage?> PostHumanAnswerAsync(
        string roomId, string humanId, string text, string askerAgentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return await this.PostCoreAsync(roomId, humanId, text, null, askerAgentId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The body <see cref="PostAsync"/> and <see cref="PostHumanAnswerAsync"/> share, so both take the
    /// same per-Room lock and neither can drift from the other's Budget, Question-drop or publish rules.
    /// </summary>
    /// <param name="roomId">The Room to post into.</param>
    /// <param name="senderId">The sender, as a Member of that Room.</param>
    /// <param name="text">The non-blank Message text.</param>
    /// <param name="messageId">The Message's id, or <see langword="null"/> to generate one.</param>
    /// <param name="withheldFromAgentId">
    /// The Agent the Message is withheld from, or <see langword="null"/> for an ordinary post. Non-null
    /// marks a Human answer, which also mentions nobody.
    /// </param>
    /// <param name="ct">Cancels the lookups, the lock wait and the append.</param>
    private async Task<ChatMessage> PostCoreAsync(
        string roomId, string senderId, string text, string? messageId, string? withheldFromAgentId, CancellationToken ct)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        var sender = members.FirstOrDefault(m => m.Id == senderId)
            ?? throw new ChatException(ErrorCodes.NotMember, $"'{senderId}' is not a member of room '{roomId}'.");

        // The reference lookups need no ordering against other posts, so they run before the Room's post lock
        // is taken (spec §8.2): holding it across them would slow every other writer to the Room. A snapshotter
        // that fails returns null, so the post goes ahead without references.
        IReadOnlyList<MessageReference>? snapshot = this.references is null
            ? null
            : await this.references.SnapshotAsync(text, ct).ConfigureAwait(false);

        var semaphore = this.postLocks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
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
            IReadOnlyList<User> mentions = withheldFromAgentId is null
                ? MentionParser.Parse(text, members, this.aliasSource.Aliases)
                : [];
            var message = new ChatMessage(id, this.timeProvider.GetUtcNow(), sender.Id, sender.Name, text) { References = snapshot };
            await this.store.AppendAsync(roomId, message, ct).ConfigureAwait(false);

            budget = this.SpendBudget(roomId, budget, isAgent);
            this.DropWaitingCards(roomId, isAgent, withheldFromAgentId);

            this.events.PublishMessagePosted(new MessagePostedEvent(room, message, members, mentions, budget, withheldFromAgentId));
            return message;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Records a Message that reached the Transcript against the Room's Budget and stores the result.
    /// </summary>
    /// <param name="roomId">The Room the Message was posted into.</param>
    /// <param name="budget">The Room's Budget before this Message.</param>
    /// <param name="isAgent">Whether the sender is an Agent.</param>
    /// <returns>The Room's Budget after this Message.</returns>
    private RoomBudget SpendBudget(string roomId, RoomBudget budget, bool isAgent)
    {
        // Only a Message that reached the Transcript spends Budget, so a failed append above
        // leaves the count where it was. A Human Message resets both halves: any extension the
        // Human granted expires with the run it was granted for.
        budget = isAgent
            ? budget with { Used = budget.Used + 1 }
            : new RoomBudget(0, this.agentMessageBudget);
        this.budgets[roomId] = budget;
        return budget;
    }

    /// <summary>
    /// Drops the Questions and forms still waiting in a Room when a typed Human Message arrives.
    /// </summary>
    /// <param name="roomId">The Room the Message was posted into.</param>
    /// <param name="isAgent">Whether the sender is an Agent.</param>
    /// <param name="withheldFromAgentId">The Agent the Message is withheld from, or <see langword="null"/> for an ordinary post.</param>
    private void DropWaitingCards(string roomId, bool isAgent, string? withheldFromAgentId)
    {
        // A typed Human Message is the answer the asker will act on, so a card still waiting would
        // invite a second, contradicting one (Questions spec §6.5, D-4). An Agent's Message must not
        // drop it: the asker's own framing Message lands after the card appears.
        if (!isAgent)
        {
            _ = this.questions.Drop(roomId);

            // A typed Message cancels every form still waiting in the Room. The Human's own answer to
            // a form (withheldFromAgentId set) is not that: it must leave the Room's other forms waiting.
            if (withheldFromAgentId is null)
            {
                _ = this.elicitations?.Drop(roomId);
            }
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
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        var history = await this.store.ReadAllAsync(roomId, ct).ConfigureAwait(false);
        if (history.Count == 0)
        {
            // Outside the per-Room semaphore, and deliberately so - there is no Budget decision to make
            // atomic here, only a display value to report. This matches how GetBudget already reads for
            // display; do not "fix" this by moving it inside the lock below.
            return new BudgetExtension(ExtendResult.NothingToRedeliver, this.CurrentBudget(roomId), null);
        }

        var last = history[^1];

        var semaphore = this.postLocks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
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
            var room = await this.InviteAsync(roomId, agentName, ct).ConfigureAwait(false);
            return new ComposerResult(null, $"Invited {agentName}. Room is now \"{room.Name}\".");
        }

        var message = await this.PostAsync(roomId, humanId, t, ct: ct).ConfigureAwait(false);
        return new ComposerResult(message, null);
    }

    /// <summary>
    /// Returns the Room whose Members are exactly the Human and this Agent, creating it if there is
    /// none. Called on every registration, so an Agent that reconnects re-attaches to the Room it
    /// already had rather than gaining a second one.
    /// </summary>
    public Task<Room> EnsureRoomForAsync(User agent, CancellationToken ct = default) =>
        this.EnsureRoomCoreAsync(agent, null, ct);

    /// <summary>As <see cref="EnsureRoomForAsync(User, CancellationToken)"/>, naming who asked for the Feed item.</summary>
    /// <param name="agent">The Agent.</param>
    /// <param name="actor">Who asked, as the Feed names them.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    internal Task<Room> EnsureRoomForAsync(User agent, FeedActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this.EnsureRoomCoreAsync(agent, actor, ct);
    }

    /// <summary>The body of <see cref="EnsureRoomForAsync(User, CancellationToken)"/>.</summary>
    /// <param name="agent">The Agent.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    private async Task<Room> EnsureRoomCoreAsync(User agent, FeedActor? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var existing = await this.teamDirectory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var room = await this.teamDirectory.CreateRoomAsync(RoomNaming.Derive([agent]), [KnownIds.Human, agent.Id], ct).ConfigureAwait(false);
        ChatService.LogCreatedDirectRoom(this.logger, room.Id, agent.Name);
        this.events.PublishRoomsChanged();
        await this.PostRoomCreatedAsync(room, [agent.Name], actor, ct).ConfigureAwait(false);
        return room;
    }

    /// <summary>
    /// Creates a root Room holding the Human and the named Agents. A single Agent reuses that Agent's
    /// existing Room, so this is the one entry point for starting any conversation.
    /// </summary>
    public Task<Room> CreateRoomForAsync(IReadOnlyList<string> agentIds, CancellationToken ct = default) =>
        this.CreateRoomForAsync(agentIds, null, ct);

    /// <summary>
    /// Creates a Room holding the Human and the named Agents, scoped to <paramref name="team"/> when one
    /// is given (ADR-0052). A single Agent reuses that Agent's existing one-to-one in the same Scope.
    /// </summary>
    /// <param name="agentIds">The Agents to invite.</param>
    /// <param name="team">The Team to scope the Room to, or <see langword="null"/> for the root.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    /// <returns>The Room.</returns>
    /// <exception cref="ChatException">The Team is unknown, no Agent is named, or an id is not an Agent.</exception>
    public Task<Room> CreateRoomForAsync(IReadOnlyList<string> agentIds, string? team, CancellationToken ct = default) =>
        this.CreateRoomCoreAsync(agentIds, team, null, ct);

    /// <summary>As <see cref="CreateRoomForAsync(IReadOnlyList{string}, string?, CancellationToken)"/>, naming who asked for the Feed item.</summary>
    /// <param name="agentIds">The Agents to invite.</param>
    /// <param name="team">The Team to scope the Room to, or <see langword="null"/> for the root.</param>
    /// <param name="actor">Who asked, as the Feed names them.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    internal Task<Room> CreateRoomForAsync(IReadOnlyList<string> agentIds, string? team, FeedActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this.CreateRoomCoreAsync(agentIds, team, actor, ct);
    }

    /// <summary>The body of <see cref="CreateRoomForAsync(IReadOnlyList{string}, string?, CancellationToken)"/>.</summary>
    /// <param name="agentIds">The Agents to invite.</param>
    /// <param name="team">The Team to scope the Room to, or <see langword="null"/> for the root.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    private async Task<Room> CreateRoomCoreAsync(IReadOnlyList<string> agentIds, string? team, FeedActor? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        string? scope = this.ResolveTeam(team);

        if (agentIds.Count == 0)
        {
            throw new ChatException(ErrorCodes.BadMessage, "select at least one agent");
        }

        var agents = new List<User>(agentIds.Count);
        foreach (var agentId in agentIds)
        {
            var user = await this.teamDirectory.GetUserAsync(agentId, ct).ConfigureAwait(false);
            if (user is null || user.Kind != UserKind.Agent)
            {
                throw new ChatException(ErrorCodes.BadMessage, $"Unknown agent '{agentId}'.");
            }

            agents.Add(user);
        }

        if (agents.Count == 1)
        {
            return scope is null
                ? await this.EnsureRoomCoreAsync(agents[0], actor, ct).ConfigureAwait(false)
                : await this.EnsureTeamOneToOneCoreAsync(scope, agents[0], actor, ct).ConfigureAwait(false);
        }

        var name = RoomNaming.Derive(agents);
        var memberIds = new List<string>(agents.Count + 1) { KnownIds.Human };
        memberIds.AddRange(agents.Select(a => a.Id));
        Room room = await this.teamDirectory.CreateRoomAsync(name, memberIds, scope, false, ct).ConfigureAwait(false);
        ChatService.LogCreatedRoom(this.logger, room.Id, room.Name, memberIds.Count);
        this.events.PublishRoomsChanged();
        await this.PostRoomCreatedAsync(room, [.. agents.Select(a => a.Name)], actor, ct).ConfigureAwait(false);
        return room;
    }

    /// <summary>
    /// Returns the Human's one-to-one with <paramref name="agent"/> inside <paramref name="team"/>,
    /// creating it, scoped to the Team, if there is none. The root one-to-one is a different Room and is
    /// left alone.
    /// </summary>
    /// <param name="team">The Team, matched ignoring case against the Team catalog.</param>
    /// <param name="agent">The Agent.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    /// <returns>The Team-scoped one-to-one.</returns>
    /// <exception cref="ChatException">The Team is unknown.</exception>
    public Task<Room> EnsureTeamOneToOneAsync(string team, User agent, CancellationToken ct = default) =>
        this.EnsureTeamOneToOneCoreAsync(team, agent, null, ct);

    /// <summary>As <see cref="EnsureTeamOneToOneAsync(string, User, CancellationToken)"/>, naming who asked for the Feed item.</summary>
    /// <param name="team">The Team, matched ignoring case against the Team catalog.</param>
    /// <param name="agent">The Agent.</param>
    /// <param name="actor">Who asked, as the Feed names them.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    internal Task<Room> EnsureTeamOneToOneAsync(string team, User agent, FeedActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this.EnsureTeamOneToOneCoreAsync(team, agent, actor, ct);
    }

    /// <summary>The body of <see cref="EnsureTeamOneToOneAsync(string, User, CancellationToken)"/>.</summary>
    /// <param name="team">The Team, matched ignoring case against the Team catalog.</param>
    /// <param name="agent">The Agent.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    private async Task<Room> EnsureTeamOneToOneCoreAsync(string team, User agent, FeedActor? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(agent);

        string scope = this.ResolveTeam(team) ?? throw new ChatException(ErrorCodes.BadMessage, $"Unknown team '{team}'.");

        Room? existing = await this.teamDirectory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, scope, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        Room room = await this.teamDirectory.CreateRoomAsync(RoomNaming.Derive([agent]), [KnownIds.Human, agent.Id], scope, false, ct).ConfigureAwait(false);
        ChatService.LogCreatedDirectRoom(this.logger, room.Id, agent.Name);
        this.events.PublishRoomsChanged();
        await this.PostRoomCreatedAsync(room, [agent.Name], actor, ct).ConfigureAwait(false);
        return room;
    }

    /// <summary>
    /// Resolves a Team name to the catalog's spelling. <see langword="null"/> in, <see langword="null"/>
    /// out (the root); a name the catalog does not know, or no catalog at all, is refused.
    /// </summary>
    private string? ResolveTeam(string? team)
    {
        if (team is null)
        {
            return null;
        }

        return this.TeamCatalog?.Find(team)?.Name
            ?? throw new ChatException(ErrorCodes.BadMessage, $"Unknown team '{team}'.");
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
    public Task<Room> InviteAsync(string roomId, string agentName, CancellationToken ct = default) =>
        this.InviteCoreAsync(roomId, agentName, null, ct);

    /// <summary>As <see cref="InviteAsync(string, string, CancellationToken)"/>, naming who asked for the Feed item.</summary>
    /// <param name="roomId">The Room to invite the Agent into.</param>
    /// <param name="agentName">The Agent's Name or Alias.</param>
    /// <param name="actor">Who asked, as the Feed names them.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    internal Task<Room> InviteAsync(string roomId, string agentName, FeedActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this.InviteCoreAsync(roomId, agentName, actor, ct);
    }

    /// <summary>The body of <see cref="InviteAsync(string, string, CancellationToken)"/>.</summary>
    /// <param name="roomId">The Room to invite the Agent into.</param>
    /// <param name="agentName">The Agent's Name or Alias.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookups and the write.</param>
    private async Task<Room> InviteCoreAsync(string roomId, string agentName, FeedActor? actor, CancellationToken ct)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        var user = await this.teamDirectory.FindUserByNameAsync(agentName, ct).ConfigureAwait(false);
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
                user = await this.teamDirectory.FindUserByNameAsync(alias.Name, ct).ConfigureAwait(false);
            }
        }

        if (user is null || user.Kind != UserKind.Agent)
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Unknown agent @{agentName}");
        }

        var before = await this.teamDirectory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        var wasAutoNamed = !room.IsTeamRoom && string.Equals(room.Name, RoomNaming.Derive(before), StringComparison.Ordinal);

        await this.teamDirectory.AddMemberAsync(roomId, user.Id, ct).ConfigureAwait(false);

        if (wasAutoNamed)
        {
            var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
            await this.teamDirectory.RenameRoomAsync(roomId, RoomNaming.Derive(members), ct).ConfigureAwait(false);
        }

        ChatService.LogInvitedAgent(this.logger, user.Name, user.Id, roomId);
        this.events.PublishRoomsChanged();

        Room after = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
        await this.PostFeedAsync(
            FeedSourceType.RoomJoined,
            after,
            actor,
            new RoomJoinedPayload(after.Id, after.Name, after.Team, user.Name),
            ct).ConfigureAwait(false);
        return after;
    }

    /// <summary>
    /// Gives a Room a Human-chosen name, which stays in place across later Invitations - see the
    /// remarks on <see cref="InviteAsync(string, string, CancellationToken)"/> for why that matters. A Room name is free display text: it
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

        Room existing = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        if (existing.IsTeamRoom)
        {
            throw new ChatException(ErrorCodes.BadMessage, "A Team Room is named after its Team and can't be renamed.");
        }

        await this.teamDirectory.RenameRoomAsync(roomId, trimmed, ct).ConfigureAwait(false);
        this.events.PublishRoomsChanged();

        return await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
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
    public Task<Room> SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default) =>
        this.SetRoomArchivedCoreAsync(roomId, archived, null, ct);

    /// <summary>As <see cref="SetRoomArchivedAsync(string, bool, CancellationToken)"/>, naming who asked for the Feed item.</summary>
    /// <param name="roomId">The Room to archive or unarchive.</param>
    /// <param name="archived"><see langword="true"/> to archive the Room; <see langword="false"/> to unarchive it.</param>
    /// <param name="actor">Who asked, as the Feed names them.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    internal Task<Room> SetRoomArchivedAsync(string roomId, bool archived, FeedActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this.SetRoomArchivedCoreAsync(roomId, archived, actor, ct);
    }

    /// <summary>The body of <see cref="SetRoomArchivedAsync(string, bool, CancellationToken)"/>.</summary>
    /// <param name="roomId">The Room to archive or unarchive.</param>
    /// <param name="archived"><see langword="true"/> to archive the Room; <see langword="false"/> to unarchive it.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the lookup and the write.</param>
    private async Task<Room> SetRoomArchivedCoreAsync(string roomId, bool archived, FeedActor? actor, CancellationToken ct)
    {
        Room before = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        await this.teamDirectory.SetRoomArchivedAsync(roomId, archived, ct).ConfigureAwait(false);

        // Never for unarchiving: an unarchived Room is exactly as live as it was, so a Proposal still
        // pending in it is still answerable. Spec §12 F-16 only drops on archiving true and on delete.
        if (archived)
        {
            this.proposals.Drop(roomId);
            _ = this.questions.Drop(roomId);
            _ = this.elicitations?.Drop(roomId);
        }

        ChatService.LogRoomArchived(this.logger, roomId, archived);
        this.events.PublishRoomsChanged();

        if (archived)
        {
            await this.PostFeedAsync(FeedSourceType.RoomArchived, before, actor, new RoomArchivedPayload(before.Id, before.Name, before.Team), ct).ConfigureAwait(false);
        }

        return await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");
    }

    /// <summary>Tells the Feed a Room was created, with the Human and <paramref name="agentNames"/> as its Members.</summary>
    /// <param name="room">The Room that was just created.</param>
    /// <param name="agentNames">The Names of the Agents in it.</param>
    /// <param name="actor">Who asked, or <see langword="null"/> for the Human.</param>
    /// <param name="ct">Cancels the Human lookup.</param>
    private async Task PostRoomCreatedAsync(Room room, IReadOnlyList<string> agentNames, FeedActor? actor, CancellationToken ct)
    {
        if (this.FeedPublisher is null)
        {
            return;
        }

        try
        {
            string humanName = await this.HumanNameAsync(ct).ConfigureAwait(false);
            await this.PostFeedAsync(
                FeedSourceType.RoomCreated,
                room,
                actor,
                new RoomCreatedPayload(room.Id, room.Name, room.Team, [humanName, .. agentNames]),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same rule as PostFeedAsync: the Room is already created, and the Feed can never fail the work that caused it.
            ChatService.LogFeedFailed(this.logger, ex, room.Id);
        }
    }

    /// <summary>Hands one Room item to the Feed. The Feed can never hurt the work, so a failure is logged and dropped.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="room">The Room the item is about.</param>
    /// <param name="actor">Who did it, or <see langword="null"/> for the Human.</param>
    /// <param name="payload">The payload, whose type matches <paramref name="type"/>.</param>
    /// <param name="ct">Cancels the Human lookup.</param>
    private async Task PostFeedAsync(FeedSourceType type, Room room, FeedActor? actor, FeedPayload payload, CancellationToken ct)
    {
        if (this.FeedPublisher is null)
        {
            return;
        }

        try
        {
            FeedActor who = actor ?? new FeedActor(FeedActorKind.Human, await this.HumanNameAsync(ct).ConfigureAwait(false));
            this.FeedPublisher.Post(new FeedMessage(type, room.Id, who, false, payload));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed records and never decides (Feed spec policy 4): a failing lookup must not fail the Room change that caused it.
            ChatService.LogFeedFailed(this.logger, ex, room.Id);
        }
    }

    /// <summary>The Human's display Name, or "You" when the directory has none.</summary>
    /// <param name="ct">Cancels the lookup.</param>
    private async Task<string> HumanNameAsync(CancellationToken ct)
    {
        User? human = await this.teamDirectory.GetUserAsync(KnownIds.Human, ct).ConfigureAwait(false);
        return human?.Name ?? "You";
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
        Room room = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false)
            ?? throw new ChatException(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.");

        await this.teamDirectory.DeleteRoomAsync(roomId, ct).ConfigureAwait(false);
        await this.store.DeleteAsync(roomId, ct).ConfigureAwait(false);

        // ChatUploads spec 6.9: the Room's uploaded files go to the Recycle Bin once the delete has
        // succeeded. Best effort - a refusal is logged and never fails the delete.
        if (this.UploadFolders is not null)
        {
            IReadOnlyList<string> failures = await this.UploadFolders.RecycleAsync(room, ct).ConfigureAwait(false);
            foreach (string failure in failures)
            {
                this.logger.LogWarning("Room '{RoomId}' was deleted but its uploads were not recycled: {Reason}", roomId, failure);
            }
        }

        // Spec §12 F-16: a deleted Room can never be Approved or Declined into, so any Proposal still
        // pending in it is dropped along with everything else below.
        this.proposals.Drop(roomId);
        _ = this.questions.Drop(roomId);
        _ = this.elicitations?.Drop(roomId);

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

    /// <summary>Logs that the Feed could not record a Room change.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="roomId">The id of the Room.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The Feed could not record the change to Room '{RoomId}'.")]
    private static partial void LogFeedFailed(ILogger logger, Exception exception, string roomId);

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