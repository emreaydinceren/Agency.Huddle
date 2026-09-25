using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// What saving a Task change would do to its assignee (Spec §10.1): the Name it would notify, that
/// Name's presence if the wake would actually happen, and which guard - if any - stops it. Every UI
/// label about who will be woken (a Save button, a drag hint, a banner) reads this record rather than
/// re-deriving the guards in Spec §10.2 itself, so the interface can never disagree with
/// <see cref="TaskTriggerService.Preview"/>.
/// </summary>
/// <param name="AssigneeName">The Task's assignee Name (<c>After.Assignee</c>), or <see langword="null"/> when the Task has none.</param>
/// <param name="Presence">The assignee's presence badge, resolved only when <paramref name="Block"/> is <see cref="WakeBlock.None"/>; otherwise <see langword="null"/>.</param>
/// <param name="Block">Which guard (Spec §10.2) stops the wake, or <see cref="WakeBlock.None"/> when none does.</param>
public sealed record WakePreview(string? AssigneeName, PresenceState? Presence, WakeBlock Block);

/// <summary>Which Spec §10.2 guard, if any, stops a Task change from waking its assignee.</summary>
public enum WakeBlock
{
    /// <summary>No guard applies: the change would wake the assignee.</summary>
    None,

    /// <summary>The Task has no assignee, or its assignee isn't a known Persona (guards 2 and 5).</summary>
    NoAssignee,

    /// <summary>The assignee is the Human (guard 3).</summary>
    AssigneeIsHuman,

    /// <summary>The assignee made the change themselves (guard 4, the self-edit guard).</summary>
    AssigneeIsActor,

    /// <summary>The change was made by an Agent, and the Task's wake budget (Spec §10.6) is spent (guard 6).</summary>
    BudgetPaused,

    /// <summary>Tasks, or wake-ups specifically, are turned off (guard 1).</summary>
    Disabled,
}

/// <summary>
/// Wakes a Task's assignee when the Task changes (Spec §10, ADR-0026): subscribes to
/// <see cref="TaskEvents.TaskChanged"/> in <see cref="StartAsync"/>, coalesces changes by the same
/// actor to the same Task for <c>Tasks.WakeCoalesceSeconds</c> (Spec §10.3), and when a batch fires
/// posts one Message through <see cref="ChatService"/> that Mentions the latest assignee and lists
/// every batched Change log summary (Spec §10.5). The Message is posted as the actor (D-11): the
/// Human for a Human or outside-Huddle change, the Agent itself for an Agent's change.
/// <para>
/// Fires are started by the coalescing timer (or directly, when the window is zero or less), run as
/// tracked tasks that catch and log everything, and are serialised behind one gate - two batches for
/// the same Task must not interleave their budget and Room decisions (corrections-B3 D9 items 7-9).
/// </para>
/// </summary>
internal sealed partial class TaskTriggerService : IHostedService, IDisposable
{
    /// <summary>The Prompt the wake-up Message is rendered from (Spec §10.5).</summary>
    private const string WakeMessageKey = "task.wake.message";

    /// <summary>More outside-edit changes than this within one coalesce window are logged, not woken (corrections-B3 D9 item 18).</summary>
    private const int MaxOutsideEditsPerWindow = 10;

    /// <summary>An <c>@</c> followed by U+2060 (word joiner): reads as <c>@</c> but no longer starts a Mention (corrections-B3 D9 item 16).</summary>
    private static readonly string NeutralisedAt = "@" + (char)0x2060;

    private readonly TaskEvents events;
    private readonly TaskStore store;
    private readonly TaskActivity activity;
    private readonly TurnActivity turns;
    private readonly ChatService chat;
    private readonly ITeamDirectory directory;
    private readonly PersonaStore personas;
    private readonly IAgentGateway gateway;
    private readonly PersonaHealth health;
    private readonly IPromptSource prompts;
    private readonly TeamOptions teamOptions;
    private readonly TimeProvider clock;
    private readonly ILogger<TaskTriggerService> logger;
    private readonly OwnPosts ownPosts;

    /// <summary>Guards <see cref="batches"/>, <see cref="inFlight"/>, <see cref="outsideWindow"/>, <see cref="subscribed"/> and <see cref="stopped"/>. Never held across an await.</summary>
    private readonly Lock gate = new();

    /// <summary>Serialises every fire (corrections-B3 D9 item 9).</summary>
    private readonly SemaphoreSlim fireGate = new(1, 1);

    /// <summary>Cancelled by <see cref="StopAsync"/>; every fire's awaited calls take its token.</summary>
    private readonly CancellationTokenSource lifetime = new();

    private readonly Dictionary<BatchKey, Batch> batches = [];
    private readonly HashSet<Task> inFlight = [];
    private OutsideWindow? outsideWindow;
    private bool subscribed;
    private bool stopped;
    private bool disposed;

    /// <summary>Creates the service. Nothing is subscribed until <see cref="StartAsync"/>.</summary>
    /// <param name="events">Raises <see cref="TaskEvents.TaskChanged"/>, subscribed to in <see cref="StartAsync"/>.</param>
    /// <param name="store">Reads a Task's latest state when a coalesced batch fires.</param>
    /// <param name="activity">The per-Task wake budget (Spec §10.6): guard 6, and the count a fired Agent-made wake consumes.</param>
    /// <param name="turns">Reports whether the assignee has a Turn running, for <see cref="Preview"/>'s presence.</param>
    /// <param name="chat">Posts the wake-up Message.</param>
    /// <param name="directory">Resolves Users and Rooms: presence, the assignee's user, the sender and the Room.</param>
    /// <param name="personas">Resolves whether the assignee is still a known Persona (guard 5).</param>
    /// <param name="gateway">Reports whether the assignee's pipe connection is live, for <see cref="Preview"/>'s presence.</param>
    /// <param name="health">Reports the assignee's latest health, for <see cref="Preview"/>'s presence.</param>
    /// <param name="prompts">Renders the <c>task.wake.message</c> Prompt.</param>
    /// <param name="options">Supplies <see cref="TasksOptions"/> (guards 1 and 6, the coalesce window) and <see cref="TeamOptions.HumanName"/> (guard 3).</param>
    /// <param name="clock">Creates the coalescing timers.</param>
    /// <param name="logger">Records skipped and failed wakes.</param>
    /// <param name="ownPosts">Records a wake posted as an Agent as that Agent's own post, so its session in the Room gets the catch-up line (corrections-B3 D9 item 17).</param>
    public TaskTriggerService(
        TaskEvents events,
        TaskStore store,
        TaskActivity activity,
        TurnActivity turns,
        ChatService chat,
        ITeamDirectory directory,
        PersonaStore personas,
        IAgentGateway gateway,
        PersonaHealth health,
        IPromptSource prompts,
        IOptions<TeamOptions> options,
        TimeProvider clock,
        ILogger<TaskTriggerService> logger,
        OwnPosts ownPosts)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(ownPosts);

        this.events = events;
        this.store = store;
        this.activity = activity;
        this.turns = turns;
        this.chat = chat;
        this.directory = directory;
        this.personas = personas;
        this.gateway = gateway;
        this.health = health;
        this.prompts = prompts;
        this.teamOptions = options.Value;
        this.clock = clock;
        this.logger = logger;
        this.ownPosts = ownPosts;
    }

    /// <summary>
    /// What saving <paramref name="after"/> would do to its assignee (Spec §10.1-§10.2), applying
    /// every guard without posting anything or touching the wake budget. Pure over its inputs and
    /// this service's injected collaborators.
    /// </summary>
    /// <param name="before">The Task before the change, or <see langword="null"/> for a Create. Unused by the guards themselves - Spec §10.2 considers only <paramref name="after"/>'s assignee, so a reassignment never tells the previous assignee.</param>
    /// <param name="after">The Task as it would be saved.</param>
    /// <param name="actor">Who made the change.</param>
    /// <returns>The resolved <see cref="WakePreview"/>.</returns>
    public WakePreview Preview(TaskItem? before, TaskItem after, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(actor);
        _ = before;

        string? assignee = after.Assignee;

        if (!this.teamOptions.Tasks.Enabled || !this.teamOptions.Tasks.WakeEnabled)
        {
            return new WakePreview(assignee, null, WakeBlock.Disabled);
        }

        if (string.IsNullOrEmpty(assignee))
        {
            return new WakePreview(assignee, null, WakeBlock.NoAssignee);
        }

        if (string.Equals(assignee, this.teamOptions.HumanName, StringComparison.OrdinalIgnoreCase))
        {
            return new WakePreview(assignee, null, WakeBlock.AssigneeIsHuman);
        }

        if (string.Equals(assignee, actor.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new WakePreview(assignee, null, WakeBlock.AssigneeIsActor);
        }

        if (this.personas.Get(assignee) is null)
        {
            return new WakePreview(assignee, null, WakeBlock.NoAssignee);
        }

        if (actor.Kind == TaskActorKind.Agent && this.activity.Budget(after.Id).Exhausted)
        {
            return new WakePreview(assignee, null, WakeBlock.BudgetPaused);
        }

        PresenceState? presence = TaskPresence.For(assignee, this.directory, this.gateway, this.health, this.turns);
        return new WakePreview(assignee, presence, WakeBlock.None);
    }

    /// <summary>Subscribes to <see cref="TaskEvents.TaskChanged"/> (Spec §10.1; corrections-B3 D9 item 7: here, never in the constructor).</summary>
    /// <param name="cancellationToken">Unused: subscribing does not wait.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (!this.subscribed)
            {
                this.events.TaskChanged += this.OnTaskChanged;
                this.subscribed = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribes, drops every pending batch and its timer (so nothing still inside its window is
    /// posted), cancels the service lifetime, and awaits every fire already in flight.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting for in-flight fires when the host gives up.</param>
    /// <returns>A task that completes once no fire is in flight.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.Halt();
        await this.WhenIdleAsync().WaitAsync(cancellationToken);
    }

    /// <summary>Unsubscribes, drops every pending batch and its timer, and releases the gate and lifetime. A fire still in flight ends on its cancelled token.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.Halt();
        this.lifetime.Dispose();
        this.fireGate.Dispose();
    }

    /// <summary>Awaits every fire in flight, including any started while waiting (corrections-B3 D9 item 8). Fires never throw, so neither does this.</summary>
    /// <returns>A task that completes once no fire is in flight.</returns>
    internal async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (this.gate)
            {
                this.inFlight.RemoveWhere(static fire => fire.IsCompleted);
                pending = [.. this.inFlight];
            }

            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending);
        }
    }

    /// <summary>Replaces every <c>@</c> with <c>@</c> + U+2060 (word joiner), so Task text can't Mention a Teammate (corrections-B3 D9 item 16).</summary>
    /// <param name="text">Task-authored text: a title or a Change log summary.</param>
    /// <returns>The text with every Mention neutralised.</returns>
    private static string NeutraliseMentions(string text) =>
        text.Replace("@", NeutralisedAt, StringComparison.Ordinal);

    /// <summary>Unsubscribes, marks the service stopped, disposes every pending timer and cancels the lifetime. Idempotent.</summary>
    private void Halt()
    {
        List<ITimer> timers = [];
        lock (this.gate)
        {
            if (this.subscribed)
            {
                this.events.TaskChanged -= this.OnTaskChanged;
                this.subscribed = false;
            }

            if (this.stopped)
            {
                return;
            }

            this.stopped = true;
            foreach (Batch batch in this.batches.Values)
            {
                if (batch.Timer is { } timer)
                {
                    timers.Add(timer);
                }
            }

            this.batches.Clear();
        }

        foreach (ITimer timer in timers)
        {
            timer.Dispose();
        }

        this.lifetime.Cancel();
    }

    /// <summary>
    /// Adds <paramref name="change"/> to its <c>(TaskId, actor Name)</c> batch (Spec §10.3), starting
    /// the batch's timer on its first change only - later changes never restart it - or firing at once
    /// when <c>WakeCoalesceSeconds &lt;= 0</c>. Also counts outside-edit changes per window (item 18).
    /// </summary>
    /// <param name="change">The change <see cref="TaskEvents.TaskChanged"/> published.</param>
    private void OnTaskChanged(TaskChange change)
    {
        if (change.Actor.Kind == TaskActorKind.Human)
        {
            // Corrections-B3 D9 item 15: on receipt, before any guard - a Human change resets the Task's
            // wake budget even when it wakes nobody. An OutsideHuddle actor never resets it (item 18).
            this.activity.ResetForHuman(change.After.Id);
        }

        BatchKey key = new(change.After.Id, change.Actor.Name);
        int seconds = this.teamOptions.Tasks.WakeCoalesceSeconds;
        bool fireNow = seconds <= 0;
        bool flooded = false;

        lock (this.gate)
        {
            if (this.stopped)
            {
                return;
            }

            if (!this.batches.TryGetValue(key, out Batch? batch))
            {
                batch = new Batch(key);
                if (!fireNow)
                {
                    batch.Timer = this.clock.CreateTimer(this.OnTimerFired, key, TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
                }

                this.batches[key] = batch;
            }

            batch.Actor = change.Actor;
            batch.Entries.Add(change.Entry);

            if (change.Actor.Kind == TaskActorKind.OutsideHuddle)
            {
                DateTimeOffset now = this.clock.GetUtcNow();
                if (this.outsideWindow is null || now - this.outsideWindow.Start >= TimeSpan.FromSeconds(Math.Max(seconds, 0)))
                {
                    this.outsideWindow = new OutsideWindow(now);
                }

                this.outsideWindow.Count++;
                flooded = this.outsideWindow.Count == MaxOutsideEditsPerWindow + 1;
                batch.OutsideWindow = this.outsideWindow;
            }
        }

        if (flooded)
        {
            LogOutsideEditFlood(this.logger, MaxOutsideEditsPerWindow);
        }

        if (fireNow)
        {
            this.StartFire(key);
        }
    }

    /// <summary>A batch's coalescing timer elapsed.</summary>
    /// <param name="state">The batch's <see cref="BatchKey"/>.</param>
    private void OnTimerFired(object? state)
    {
        if (state is BatchKey key)
        {
            this.StartFire(key);
        }
    }

    /// <summary>Starts a fire for <paramref name="key"/> and tracks it for <see cref="WhenIdleAsync"/>.</summary>
    /// <param name="key">The batch to fire.</param>
    private void StartFire(BatchKey key)
    {
        Task fire = this.FireAsync(key);
        lock (this.gate)
        {
            this.inFlight.RemoveWhere(static done => done.IsCompleted);
            this.inFlight.Add(fire);
        }
    }

    /// <summary>
    /// Fires one batch: removes it at once (a change arriving from here on opens a new batch), then,
    /// behind the fire gate, sends its wake. Catches and logs everything - a fire runs detached from
    /// any caller, so an escaping exception would go unobserved.
    /// </summary>
    /// <param name="key">The batch to fire.</param>
    /// <returns>A task that completes when the fire is done; it never faults.</returns>
    private async Task FireAsync(BatchKey key)
    {
        try
        {
            Batch? batch;
            bool flooded;
            CancellationToken ct;
            lock (this.gate)
            {
                if (this.stopped || !this.batches.Remove(key, out batch))
                {
                    return;
                }

                flooded = batch.OutsideWindow is { Count: > MaxOutsideEditsPerWindow };
                ct = this.lifetime.Token;
            }

            batch.Timer?.Dispose();
            if (flooded)
            {
                LogOutsideEditSkipped(this.logger, key.TaskId);
                return;
            }

            await this.fireGate.WaitAsync(ct);
            try
            {
                await this.WakeAsync(batch, ct);
            }
            finally
            {
                this.fireGate.Release();
            }
        }
        catch (OperationCanceledException ex) when (this.stopped)
        {
            LogFireCancelled(this.logger, key.TaskId, ex);
        }
        catch (Exception ex)
        {
            // Nothing awaits a fire but WhenIdleAsync, so an exception here would go unobserved: log it
            // and let the next change wake the assignee as usual (corrections-B3 D9 item 7).
            LogFireFailed(this.logger, key.TaskId, ex);
        }
    }

    /// <summary>
    /// Sends one batch's wake: reads the latest Task, applies the guards, resolves the assignee, the
    /// sender and the Room, consumes the Agent wake budget, posts, and refunds the budget unless the
    /// Message was posted (the outcome is Woken or Offline, the only two that count - item 9). Every
    /// outcome of Spec §10.5 is recorded in <see cref="TaskActivity"/>: Woken or Offline by the
    /// assignee's presence after posting, BudgetSpent for a Room Budget refusal, WakePaused for a spent
    /// Task wake budget, and Failed for any other <see cref="ChatException"/> or an Agent actor with no
    /// user - the last two with no Room.
    /// </summary>
    /// <param name="batch">The batch, already removed from <see cref="batches"/>.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when the wake was posted or skipped.</returns>
    private async Task WakeAsync(Batch batch, CancellationToken ct)
    {
        TaskId id = batch.Key.TaskId;
        TaskItem? latest = this.store.Get(id);
        if (latest is null)
        {
            LogTaskGone(this.logger, id);
            return;
        }

        TaskActor actor = batch.Actor;
        WakePreview preview = this.Preview(null, latest, actor);
        if (preview.Block == WakeBlock.BudgetPaused && preview.AssigneeName is { } pausedAssignee)
        {
            LogBlocked(this.logger, id, preview.Block);
            this.RecordOutcome(id, pausedAssignee, null, WakeOutcome.WakePaused);
            return;
        }

        if (preview.Block != WakeBlock.None || preview.AssigneeName is not { } assigneeName)
        {
            LogBlocked(this.logger, id, preview.Block);
            return;
        }

        User? assignee = await this.directory.FindUserByNameAsync(assigneeName, ct);
        if (assignee is null)
        {
            LogAssigneeNeverRegistered(this.logger, id, assigneeName);
            return;
        }

        string? senderId = await this.ResolveSenderIdAsync(actor, ct);
        if (senderId is null)
        {
            LogNoSender(this.logger, id, actor.Name);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.Failed);
            return;
        }

        Room room;
        try
        {
            room = await this.ChooseRoomAsync(latest, senderId, assignee, ct);
        }
        catch (ChatException ex)
        {
            LogRoomRefused(this.logger, id, ex.Code, ex);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.Failed);
            return;
        }

        bool isAgent = actor.Kind == TaskActorKind.Agent;
        if (isAgent && !this.activity.TryConsumeAgentWake(id))
        {
            LogBlocked(this.logger, id, WakeBlock.BudgetPaused);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.WakePaused);
            return;
        }

        WakeOutcome outcome;
        bool counted = false;
        try
        {
            string text = this.Render(latest, actor, assigneeName, batch.Entries);
            await this.chat.PostAsync(room.Id, senderId, text, ct: ct);
            if (isAgent)
            {
                this.ownPosts.Record(senderId, room.Id, text);
            }

            PresenceState? presence = TaskPresence.For(assigneeName, this.directory, this.gateway, this.health, this.turns);
            outcome = presence is PresenceState.Awake or PresenceState.Asleep ? WakeOutcome.Woken : WakeOutcome.Offline;
            counted = true;
        }
        catch (ChatException ex) when (string.Equals(ex.Code, ErrorCodes.BudgetExhausted, StringComparison.Ordinal))
        {
            LogPostRefused(this.logger, id, room.Id, ex.Code, ex);
            outcome = WakeOutcome.BudgetSpent;
        }
        catch (ChatException ex)
        {
            LogPostRefused(this.logger, id, room.Id, ex.Code, ex);
            outcome = WakeOutcome.Failed;
        }
        finally
        {
            if (isAgent && !counted)
            {
                this.activity.RefundAgentWake(id);
            }
        }

        this.RecordOutcome(id, assigneeName, room, outcome);
    }

    /// <summary>Records one wake outcome in <see cref="TaskActivity"/> (Spec §10.5, §10.7), stamped with the clock at fire time.</summary>
    /// <param name="id">The Task.</param>
    /// <param name="assigneeName">The assignee the wake targeted.</param>
    /// <param name="room">The Room posted in, or <see langword="null"/> when none was chosen.</param>
    /// <param name="outcome">What the wake did.</param>
    private void RecordOutcome(TaskId id, string assigneeName, Room? room, WakeOutcome outcome) =>
        this.activity.Record(new WakeRecord(id, assigneeName, room?.Id, room?.Name, outcome, this.clock.GetUtcNow()));

    /// <summary>The actor as a Room Member (Spec §10.4): the Human for a Human or outside-Huddle change; the Agent's own user otherwise.</summary>
    /// <param name="actor">Who made the change.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>The sender's user id, or <see langword="null"/> when an Agent actor has no user row.</returns>
    private async Task<string?> ResolveSenderIdAsync(TaskActor actor, CancellationToken ct)
    {
        if (actor.Kind != TaskActorKind.Agent)
        {
            return KnownIds.Human;
        }

        if (actor.UserId is { } userId)
        {
            return userId;
        }

        User? user = await this.directory.FindUserByNameAsync(actor.Name, ct);
        return user?.Id;
    }

    /// <summary>
    /// Chooses the Room the wake is posted in, by Spec §10.4 steps 1-4 in order:
    /// <list type="number">
    /// <item><description>The Task's origin Room, when it exists (Archived is allowed, D-12) and both the sender and the assignee are Members.</description></item>
    /// <item><description>The creator Room: S = {Human, creator, assignee}, or {Human, assignee} when the creator is the Human or has no user row (corrections-B3 D9 item 13). Used only when the sender is in S, via <see cref="ITeamDirectory.FindRoomWithExactMemberSetAsync"/> (which skips Archived Rooms).</description></item>
    /// <item><description>Otherwise - a third Agent made the change (D-13) - the actor Room: S = {Human, sender, assignee}, looked up the same way.</description></item>
    /// <item><description>No Room found: <see cref="ChatService.CreateRoomForAsync"/> creates one for the last step's S without the Human (it adds the Human itself, and one Agent gives the direct Room).</description></item>
    /// </list>
    /// Every set tried contains the sender, so <see cref="ChatService.PostAsync"/>'s membership check
    /// passes. Runs inside the fire gate, which is what keeps two racing fires from creating two Rooms.
    /// </summary>
    /// <param name="task">The latest Task.</param>
    /// <param name="senderId">The sender's user id.</param>
    /// <param name="assignee">The assignee's user.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>The Room to post in.</returns>
    private async Task<Room> ChooseRoomAsync(TaskItem task, string senderId, User assignee, CancellationToken ct)
    {
        if (await this.FindUsableOriginAsync(task, senderId, assignee, ct) is { } origin)
        {
            return origin;
        }

        List<string> memberSet = [KnownIds.Human];
        User? creator = await this.directory.FindUserByNameAsync(task.Creator, ct);
        if (creator is { Kind: UserKind.Agent })
        {
            memberSet.Add(creator.Id);
        }

        AddOnce(memberSet, assignee.Id);
        if (!memberSet.Contains(senderId, StringComparer.Ordinal))
        {
            memberSet = [KnownIds.Human, senderId];
            AddOnce(memberSet, assignee.Id);
        }

        Room? existing = await this.directory.FindRoomWithExactMemberSetAsync(memberSet, ct);
        if (existing is not null)
        {
            return existing;
        }

        List<string> agentIds = [.. memberSet.Where(static id => !string.Equals(id, KnownIds.Human, StringComparison.Ordinal))];
        return await this.chat.CreateRoomForAsync(agentIds, ct);
    }

    /// <summary>Adds <paramref name="id"/> to <paramref name="ids"/> unless it is already there (ordinal).</summary>
    /// <param name="ids">The member set being built.</param>
    /// <param name="id">The id to add.</param>
    private static void AddOnce(List<string> ids, string id)
    {
        if (!ids.Contains(id, StringComparer.Ordinal))
        {
            ids.Add(id);
        }
    }

    /// <summary>Spec §10.4 step 1: the Task's origin Room, if it exists (Archived is allowed) and both the sender and the assignee are Members.</summary>
    /// <param name="task">The latest Task.</param>
    /// <param name="senderId">The sender's user id.</param>
    /// <param name="assignee">The assignee's user.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>The origin Room, or <see langword="null"/> when step 1 doesn't apply.</returns>
    private async Task<Room?> FindUsableOriginAsync(TaskItem task, string senderId, User assignee, CancellationToken ct)
    {
        if (task.OriginRoomId is not { } originId)
        {
            return null;
        }

        Room? origin = await this.directory.GetRoomAsync(originId, ct);
        if (origin is null)
        {
            return null;
        }

        IReadOnlyList<User> members = await this.directory.GetRoomMembersAsync(originId, ct);
        bool senderIsMember = members.Any(m => string.Equals(m.Id, senderId, StringComparison.Ordinal));
        bool assigneeIsMember = members.Any(m => string.Equals(m.Id, assignee.Id, StringComparison.Ordinal));
        return senderIsMember && assigneeIsMember ? origin : null;
    }

    /// <summary>Renders <c>task.wake.message</c> (Spec §10.5) for the latest Task and the batch's summaries, neutralising Mentions in the title and the changes.</summary>
    /// <param name="task">The latest Task.</param>
    /// <param name="actor">Who made the changes.</param>
    /// <param name="assigneeName">The latest assignee's Name.</param>
    /// <param name="entries">The batch's Change log entries, in arrival order.</param>
    /// <returns>The Message text.</returns>
    private string Render(TaskItem task, TaskActor actor, string assigneeName, IReadOnlyList<ChangeLogEntry> entries)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["{{assignee}}"] = assigneeName,
            ["{{taskId}}"] = task.Id.ToString(),
            ["{{title}}"] = NeutraliseMentions(task.Title),
            ["{{status}}"] = task.Status.ToWire(),
            ["{{team}}"] = task.Location.Team,
            ["{{actor}}"] = actor.Name,
            ["{{changes}}"] = string.Join('\n', entries.Select(static e => "- " + NeutraliseMentions(e.Summary))),
        };

        return this.prompts.Render(WakeMessageKey, values);
    }

    /// <summary>Logs that more outside-edit changes than the limit arrived within one coalesce window.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="limit">The per-window limit.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "More than {Limit} Task changes made outside Huddle arrived within one coalesce window; they are logged and nobody is woken.")]
    private static partial void LogOutsideEditFlood(ILogger logger, int limit);

    /// <summary>Logs that one Task's outside-edit wake was skipped as part of a flood.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Not waking the assignee of Task {TaskId}: it changed outside Huddle as part of a bulk edit.")]
    private static partial void LogOutsideEditSkipped(ILogger logger, TaskId taskId);

    /// <summary>Logs that a Task was gone by the time its batch fired.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Not waking anyone for Task {TaskId}: it no longer exists.")]
    private static partial void LogTaskGone(ILogger logger, TaskId taskId);

    /// <summary>Logs that a guard stopped a wake.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="block">The guard.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Not waking the assignee of Task {TaskId}: {Block}.")]
    private static partial void LogBlocked(ILogger logger, TaskId taskId, WakeBlock block);

    /// <summary>Logs that the assignee has never registered as a user.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="assignee">The assignee's Name.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Not waking '{Assignee}' for Task {TaskId}: that Persona has never registered.")]
    private static partial void LogAssigneeNeverRegistered(ILogger logger, TaskId taskId, string assignee);

    /// <summary>Logs that the Agent actor has no user to post as.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="actor">The actor's Name.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Not waking the assignee of Task {TaskId}: the actor '{Actor}' has no user to post as.")]
    private static partial void LogNoSender(ILogger logger, TaskId taskId, string actor);

    /// <summary>Logs that the Room refused the wake-up Message.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="code">The refusal's error code.</param>
    /// <param name="exception">The refusal.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not wake the assignee of Task {TaskId} in Room {RoomId}: {Code}.")]
    private static partial void LogPostRefused(ILogger logger, TaskId taskId, string roomId, string code, Exception exception);

    /// <summary>Logs that choosing or creating the Room was refused.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="code">The refusal's error code.</param>
    /// <param name="exception">The refusal.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not wake the assignee of Task {TaskId}: no Room could be chosen ({Code}).")]
    private static partial void LogRoomRefused(ILogger logger, TaskId taskId, string code, Exception exception);

    /// <summary>Logs that a fire was cancelled by shutdown.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="exception">The cancellation.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Wake-up for Task {TaskId} cancelled by shutdown.")]
    private static partial void LogFireCancelled(ILogger logger, TaskId taskId, Exception exception);

    /// <summary>Logs that a fire failed unexpectedly.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Task.</param>
    /// <param name="exception">The failure.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wake-up for Task {TaskId} failed.")]
    private static partial void LogFireFailed(ILogger logger, TaskId taskId, Exception exception);

    /// <summary>Identifies a batch: changes by one actor (by Name) to one Task (Spec §10.3).</summary>
    /// <param name="TaskId">The Task.</param>
    /// <param name="ActorName">The actor's Name.</param>
    private readonly record struct BatchKey(TaskId TaskId, string ActorName);

    /// <summary>One coalescing batch. Mutated only under <see cref="gate"/> until its fire removes it.</summary>
    /// <param name="key">The batch's key.</param>
    private sealed class Batch(BatchKey key)
    {
        /// <summary>The batch's key.</summary>
        public BatchKey Key { get; } = key;

        /// <summary>The actor of the latest change: the Message is posted as them.</summary>
        public TaskActor Actor { get; set; } = new(TaskActorKind.Human, key.ActorName, null);

        /// <summary>Every batched change's Change log entry, in arrival order.</summary>
        public List<ChangeLogEntry> Entries { get; } = [];

        /// <summary>The coalescing timer, or <see langword="null"/> when the batch fires directly.</summary>
        public ITimer? Timer { get; set; }

        /// <summary>The outside-edit window of this batch's latest outside-Huddle change, if any.</summary>
        public OutsideWindow? OutsideWindow { get; set; }
    }

    /// <summary>A coalesce window of outside-edit changes, counted to spot a bulk edit (corrections-B3 D9 item 18).</summary>
    /// <param name="start">When the window's first outside-edit change arrived.</param>
    private sealed class OutsideWindow(DateTimeOffset start)
    {
        /// <summary>When the window's first outside-edit change arrived.</summary>
        public DateTimeOffset Start { get; } = start;

        /// <summary>How many outside-edit changes arrived in this window.</summary>
        public int Count { get; set; }
    }
}
