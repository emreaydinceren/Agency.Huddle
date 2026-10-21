using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Agency.Huddle.App.Tasks;

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
    private readonly IHumanIdentity human;
    private readonly ITeamCatalog? teams;

    /// <summary>Guards <see cref="batches"/>, <see cref="inFlight"/>, <see cref="outsideWindow"/>, <see cref="subscribed"/> and <see cref="stopped"/>. Never held across an await.</summary>
    private readonly Lock gate = new();

    /// <summary>Serialises every fire (corrections-B3 D9 item 9).</summary>
    private readonly SemaphoreSlim fireGate = new(1, 1);

    /// <summary>Cancelled by <see cref="StopAsync"/>; every fire's awaited calls take its token.</summary>
    private readonly CancellationTokenSource lifetime = new();

    private readonly Dictionary<BatchKey, Batch> batches = [];

    /// <summary>When a Request lifecycle wake last reached a recipient for a Task, keyed by Task and Name; a status-change wake for the same event is then skipped.</summary>
    private readonly Dictionary<string, DateTimeOffset> requestWakes = new(StringComparer.OrdinalIgnoreCase);

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
    /// <param name="options">Supplies <see cref="TasksOptions"/> (guards 1 and 6, the coalesce window) and the first-run Human Name when <paramref name="human"/> is <see langword="null"/>.</param>
    /// <param name="clock">Creates the coalescing timers.</param>
    /// <param name="logger">Records skipped and failed wakes.</param>
    /// <param name="ownPosts">Records a wake posted as an Agent as that Agent's own post, so its session in the Room gets the catch-up line (corrections-B3 D9 item 17).</param>
    /// <param name="teams">Names a Team's manager, who is woken alongside the assignee (OR-2); <see langword="null"/> wakes the assignee only.</param>
    /// <param name="human">The Human's live identity, whose Name or Alias is never woken (guard 3); <see langword="null"/> fixes it at <see cref="TeamOptions.HumanName"/>.</param>
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
        OwnPosts ownPosts,
        ITeamCatalog? teams = null,
        IHumanIdentity? human = null)
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
        this.teams = teams;
        this.human = human ?? new ConfiguredHumanIdentity(options.Value.HumanName);
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

        if (this.human.Matches(assignee))
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
        await this.WhenIdleAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
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

            await Task.WhenAll(pending).ConfigureAwait(false);
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

        if (change.After.Status == TaskState.Done && change.Before?.Status != TaskState.Done)
        {
            this.StartUnblock(change);
        }

        BatchKey key = new(change.After.Id, change.Actor.Name);
        int seconds = this.teamOptions.Tasks.WakeCoalesceSeconds;
        bool fireNow = seconds <= 0;

        if (!this.TryAddToBatch(change, key, seconds, fireNow, out bool flooded))
        {
            return;
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

    /// <summary>
    /// Under the gate, adds <paramref name="change"/> to its batch, creating the batch and its timer on the first change,
    /// and counts an outside edit against the current window.
    /// </summary>
    /// <param name="change">The change to add.</param>
    /// <param name="key">The batch's key.</param>
    /// <param name="seconds">The coalescing window in seconds.</param>
    /// <param name="fireNow">Whether the batch fires at once, so needs no timer.</param>
    /// <param name="flooded">Whether this change is the one that tipped the outside-edit window over its limit.</param>
    /// <returns><see langword="false"/> when the service has stopped and the change was dropped.</returns>
    private bool TryAddToBatch(TaskChange change, BatchKey key, int seconds, bool fireNow, out bool flooded)
    {
        flooded = false;
        lock (this.gate)
        {
            if (this.stopped)
            {
                return false;
            }

            if (!this.batches.TryGetValue(key, out Batch? batch))
            {
                batch = new Batch(key) { FirstChangeAt = this.clock.GetUtcNow() };
                if (!fireNow)
                {
#pragma warning disable HAA0601 // The framework API takes the state as object, so the box cannot be avoided.
                    batch.Timer = this.clock.CreateTimer(this.OnTimerFired, key, TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
#pragma warning restore HAA0601
                }

                this.batches[key] = batch;
            }

            batch.Actor = change.Actor;
            batch.Entries.Add(change.Entry);
            if (change.Decision is { } decision)
            {
                batch.Decision = decision;
                batch.Note = change.Note;
            }

            if (change.Changes.Any(static field => field.Field == TaskField.Status))
            {
                batch.StatusChanged = true;
            }

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

        return true;
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

    /// <summary>Starts the unblock pass for a Task that reached Done and tracks it for <see cref="WhenIdleAsync"/>.</summary>
    /// <param name="change">The change that finished the Task.</param>
    private void StartUnblock(TaskChange change)
    {
        lock (this.gate)
        {
            if (this.stopped)
            {
                return;
            }
        }

        Task pass = this.UnblockAsync(change);
        lock (this.gate)
        {
            this.inFlight.RemoveWhere(static done => done.IsCompleted);
            this.inFlight.Add(pass);
        }
    }

    /// <summary>Runs one unblock pass behind the fire gate. Catches and logs everything, like <see cref="FireAsync"/>.</summary>
    /// <param name="change">The change that finished the Task.</param>
    /// <returns>A task that completes when the pass is done; it never faults.</returns>
    private async Task UnblockAsync(TaskChange change)
    {
        TaskId id = change.After.Id;
        try
        {
            CancellationToken ct;
            lock (this.gate)
            {
                if (this.stopped)
                {
                    return;
                }

                ct = this.lifetime.Token;
            }

            await this.fireGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await this.WakeUnblockedAsync(change, ct).ConfigureAwait(false);
            }
            finally
            {
                this.fireGate.Release();
            }
        }
        catch (OperationCanceledException ex) when (this.stopped)
        {
            LogFireCancelled(this.logger, id, ex);
        }
        catch (Exception ex)
        {
            // Detached like a fire: log, so the next Done event still gets its pass (corrections-B3 D9 item 7).
            LogFireFailed(this.logger, id, ex);
        }
    }

    /// <summary>
    /// RQ-6: for every Task (in any Team) whose <c>blocked_by</c> lists the Task that just reached
    /// Done, that is neither Done nor Rejected, and whose blockers are now all Done, wakes its
    /// assignee - or the Team's manager when it has none. Each unblocking is recorded as an entry in
    /// the blocked Task's own change log, keyed by when the blocker last reached Done, so a duplicate
    /// event wakes nobody again while a reopen followed by a new Done does.
    /// </summary>
    /// <param name="change">The change that finished the Task.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when every unblocked Task was handled.</returns>
    private async Task WakeUnblockedAsync(TaskChange change, CancellationToken ct)
    {
        if (!this.teamOptions.Tasks.Enabled || !this.teamOptions.Tasks.WakeEnabled)
        {
            return;
        }

        TaskItem finished = change.After;
        IReadOnlyList<TaskItem> candidates = this.store.BlockedOn(finished.Id);
        foreach (TaskItem blocked in candidates)
        {
            bool open = blocked.Status is not (TaskState.Done or TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected);
            bool allDone = blocked.BlockedBy.All(blocker => blocker == finished.Id || this.store.Get(blocker)?.Status == TaskState.Done);
            if (!open || !allDone)
            {
                continue;
            }

            // One marker per blocked Task and blocker set, stamped with the latest Done among the blockers: two
            // blockers finishing together produce the same marker, a reopen followed by a new Done a new one.
            DateTimeOffset latestDone = blocked.BlockedBy
                .Select(blocker => DoneAt(blocker == finished.Id ? finished : this.store.Get(blocker), change.Entry.At))
                .Max();
            string blockers = string.Join(',', blocked.BlockedBy.Select(static blocker => blocker.ToString()).Order(StringComparer.Ordinal));
            string marker = string.Create(CultureInfo.InvariantCulture, $"Unblocked: {blocked.Id} by {blockers} at {latestDone:O}");
            if (!blocked.ChangeLog.Any(entry => string.Equals(entry.Summary, marker, StringComparison.Ordinal)))
            {
                await this.WakeUnblockedTaskAsync(blocked, change.Actor, marker, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>When the Task last reached Done, read from the <c>status: … → Done</c> summaries <see cref="TaskDiff"/> writes.</summary>
    /// <param name="task">The blocker, or <see langword="null"/> when it no longer exists.</param>
    /// <param name="fallback">Used when the change log holds no such entry.</param>
    /// <returns>The time of the last Done entry, or <paramref name="fallback"/>.</returns>
    private static DateTimeOffset DoneAt(TaskItem? task, DateTimeOffset fallback)
    {
        DateTimeOffset doneAt = fallback;
        foreach (ChangeLogEntry entry in task?.ChangeLog ?? [])
        {
            if (entry.Summary.StartsWith("status:", StringComparison.Ordinal) && entry.Summary.Contains("→ Done", StringComparison.Ordinal))
            {
                doneAt = entry.At;
            }
        }

        return doneAt;
    }

    /// <summary>Picks the recipient for one unblocked Task, records the unblocking in its change log and delivers the wake.</summary>
    /// <param name="blocked">The Task that is no longer blocked.</param>
    /// <param name="actor">Who finished the last blocker.</param>
    /// <param name="marker">The change-log summary that records this unblocking.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when the wake was posted or skipped.</returns>
    private async Task WakeUnblockedTaskAsync(TaskItem blocked, TaskActor actor, string marker, CancellationToken ct)
    {
        string? recipient;
        if (string.IsNullOrEmpty(blocked.Assignee))
        {
            string? manager = this.teams?.Find(blocked.Location.Team)?.Manager;
            bool usable = !string.IsNullOrEmpty(manager)
                && !string.Equals(manager, actor.Name, StringComparison.OrdinalIgnoreCase)
                && !this.human.Matches(manager)
                && this.personas.Get(manager) is not null;
            recipient = usable ? manager : null;
        }
        else
        {
            WakePreview preview = this.Preview(null, blocked, actor);
            recipient = preview.Block == WakeBlock.None ? preview.AssigneeName : null;
        }

        if (recipient is null)
        {
            LogBlocked(this.logger, blocked.Id, WakeBlock.NoAssignee);
            return;
        }

        ChangeLogEntry unblocked = new(this.clock.GetUtcNow(), actor.Name, marker);
        Batch batch = new(new BatchKey(blocked.Id, actor.Name)) { Actor = actor };
        batch.Entries.Add(unblocked);
        if (!await this.DeliverAsync(batch, blocked, recipient, ct).ConfigureAwait(false))
        {
            return;
        }

        // The marker is written only after a delivered wake, so a failed delivery can be retried by the next Done event.
        if (this.store.Get(blocked.Id) is not { } current || this.store.AppendEntry(blocked.Id, current.Version, unblocked) is null)
        {
            LogTaskGone(this.logger, blocked.Id);
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

            await this.fireGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await this.WakeAsync(batch, ct).ConfigureAwait(false);
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

        await this.WakeAssigneeAsync(batch, latest, ct).ConfigureAwait(false);
        await this.WakeManagerAsync(batch, latest, ct).ConfigureAwait(false);
        await this.WakeRequestingManagerAsync(batch, latest, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// RQ-4: wakes the manager of the Team that filed a Request on every status change of that Request,
    /// unless that manager made the change, is the assignee or the receiving Team's manager (both woken
    /// above), is the Human, or is no known Persona. A Request whose requesting Team has no manager wakes
    /// nobody and is logged. Goes through <see cref="DeliverAsync"/>, so it shares the per-Task wake budget.
    /// </summary>
    /// <param name="batch">The batch, already removed from <see cref="batches"/>.</param>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when the wake was posted or skipped.</returns>
    private async Task WakeRequestingManagerAsync(Batch batch, TaskItem latest, CancellationToken ct)
    {
        if (!batch.StatusChanged || !this.teamOptions.Tasks.Enabled || !this.teamOptions.Tasks.WakeEnabled)
        {
            return;
        }

        Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in latest.UnknownFields)
        {
            frontmatter[field.Key] = field.Value;
        }

        if (!RequestFields.TryRead(frontmatter, out RequestFields? request) || request is null)
        {
            return;
        }

        string? manager = this.teams?.Find(request.FromTeam)?.Manager;
        if (string.IsNullOrEmpty(manager))
        {
            LogNoRequestingManager(this.logger, latest.Id, request.FromTeam);
            return;
        }

        if (string.Equals(manager, batch.Actor.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, latest.Assignee, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, this.teams?.Find(latest.Location.Team)?.Manager, StringComparison.OrdinalIgnoreCase)
            || this.human.Matches(manager)
            || this.personas.Get(manager) is null)
        {
            return;
        }

        await this.DeliverAsync(batch, latest, manager, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Wakes <paramref name="recipient"/> about a Request lifecycle event (RQ-7), through the same delivery
    /// and per-Task wake budget as a Task change. Nobody is woken when wake-ups are off, the recipient made
    /// the change, is the Human, or is no known Persona.
    /// </summary>
    /// <param name="request">The Request Task the event happened to.</param>
    /// <param name="recipient">The Persona to wake.</param>
    /// <param name="actor">Who caused the event.</param>
    /// <param name="summary">The line the wake message lists as the change.</param>
    /// <param name="ct">Cancels the wake.</param>
    /// <returns><see langword="true"/> when the wake was posted; <see langword="false"/> when it was skipped or failed.</returns>
    internal async Task<bool> WakeForRequestAsync(TaskItem request, string recipient, TaskActor actor, string summary, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(summary);

        if (!this.teamOptions.Tasks.Enabled
            || !this.teamOptions.Tasks.WakeEnabled
            || string.Equals(recipient, actor.Name, StringComparison.OrdinalIgnoreCase)
            || this.human.Matches(recipient)
            || this.personas.Get(recipient) is null)
        {
            return false;
        }

        Batch batch = new(new BatchKey(request.Id, actor.Name)) { Actor = actor };
        batch.Entries.Add(new ChangeLogEntry(this.clock.GetUtcNow(), actor.Name, summary));
        await this.fireGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            bool posted = await this.DeliverAsync(batch, request, recipient, ct, viaRequest: true).ConfigureAwait(false);
            if (posted)
            {
                lock (this.gate)
                {
                    this.requestWakes[RequestWakeKey(request.Id, recipient)] = this.clock.GetUtcNow();
                }
            }

            return posted;
        }
        finally
        {
            this.fireGate.Release();
        }
    }

    /// <summary>The key of <see cref="requestWakes"/>.</summary>
    /// <param name="id">The Task.</param>
    /// <param name="name">The recipient's Name.</param>
    private static string RequestWakeKey(TaskId id, string name) => string.Concat(id.ToString(), "|", name);

    /// <summary>
    /// Whether a Request lifecycle wake already reached <paramref name="name"/> for this Task since the batch's first change, so the
    /// status-change wake for the same event is not a second one.
    /// </summary>
    /// <param name="id">The Task.</param>
    /// <param name="name">The recipient's Name.</param>
    /// <param name="batch">The batch about to wake the recipient.</param>
    private bool WokenForRequest(TaskId id, string name, Batch batch)
    {
        if (batch.Entries.Count == 0)
        {
            return false;
        }

        DateTimeOffset batchStart = batch.FirstChangeAt;
        lock (this.gate)
        {
            return this.requestWakes.TryGetValue(RequestWakeKey(id, name), out DateTimeOffset woken) && woken >= batchStart;
        }
    }

    /// <summary>Applies the Spec §10.2 guards to the Task's assignee and, when none stops it, delivers the assignee's wake.</summary>
    /// <param name="batch">The batch, already removed from <see cref="batches"/>.</param>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when the wake was posted or skipped.</returns>
    private async Task WakeAssigneeAsync(Batch batch, TaskItem latest, CancellationToken ct)
    {
        TaskId id = latest.Id;
        WakePreview preview = this.Preview(null, latest, batch.Actor);
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

        await this.DeliverAsync(batch, latest, assigneeName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// OR-2: wakes the Team's manager (from <c>_team.md</c>) about the same change, unless wake-ups
    /// are off, the manager made the change, already is the assignee (so was woken above), is the
    /// Human, or is no known Persona. The wake goes through <see cref="DeliverAsync"/>, so it consumes
    /// the same per-Task Teammate-made wake budget as the assignee's. A Team with no manager, or no
    /// catalog, behaves as before.
    /// </summary>
    /// <param name="batch">The batch, already removed from <see cref="batches"/>.</param>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>A task that completes when the wake was posted or skipped.</returns>
    private async Task WakeManagerAsync(Batch batch, TaskItem latest, CancellationToken ct)
    {
        if (this.teams?.Find(latest.Location.Team)?.Manager is not { Length: > 0 } manager
            || !this.teamOptions.Tasks.Enabled
            || !this.teamOptions.Tasks.WakeEnabled)
        {
            return;
        }

        if (string.Equals(manager, batch.Actor.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manager, latest.Assignee, StringComparison.OrdinalIgnoreCase)
            || this.human.Matches(manager)
            || this.personas.Get(manager) is null)
        {
            return;
        }

        await this.DeliverAsync(batch, latest, manager, ct).ConfigureAwait(false);
    }

    /// <summary>Resolves the recipient, the sender and the Room, consumes the Agent wake budget, posts the wake and records its outcome.</summary>
    /// <param name="batch">The batch, already removed from <see cref="batches"/>.</param>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="assigneeName">The Name to wake: the assignee, or the Team's manager.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <param name="viaRequest">Whether this is a Request lifecycle wake, which is never skipped for an earlier one.</param>
    /// <returns><see langword="true"/> when the wake was posted; <see langword="false"/> when it was skipped or failed.</returns>
    private async Task<bool> DeliverAsync(Batch batch, TaskItem latest, string assigneeName, CancellationToken ct, bool viaRequest = false)
    {
        TaskId id = latest.Id;
        if (!viaRequest && this.WokenForRequest(id, assigneeName, batch))
        {
            return false;
        }

        TaskActor actor = batch.Actor;
        User? assignee = await this.directory.FindUserByNameAsync(assigneeName, ct).ConfigureAwait(false);
        if (assignee is null)
        {
            LogAssigneeNeverRegistered(this.logger, id, assigneeName);
            return false;
        }

        string? senderId = await this.ResolveSenderIdAsync(actor, ct).ConfigureAwait(false);
        if (senderId is null)
        {
            LogNoSender(this.logger, id, actor.Name);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.Failed);
            return false;
        }

        Room room;
        try
        {
            room = await this.ChooseRoomAsync(latest, senderId, assignee, ct).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            LogRoomRefused(this.logger, id, ex.Code, ex);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.Failed);
            return false;
        }

        bool isAgent = actor.Kind == TaskActorKind.Agent;
        if (isAgent && !this.activity.TryConsumeAgentWake(id))
        {
            LogBlocked(this.logger, id, WakeBlock.BudgetPaused);
            this.RecordOutcome(id, assigneeName, null, WakeOutcome.WakePaused);
            return false;
        }

        WakeOutcome outcome = await this.PostWakeAsync(batch, latest, assigneeName, senderId, room, ct).ConfigureAwait(false);
        this.RecordOutcome(id, assigneeName, room, outcome);
        return outcome is WakeOutcome.Woken or WakeOutcome.Offline;
    }

    /// <summary>Posts the wake text in <paramref name="room"/>; an Agent's wake budget is refunded unless the post went through.</summary>
    /// <param name="batch">The batch being delivered.</param>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="assigneeName">The Name being woken.</param>
    /// <param name="senderId">The sender's user id.</param>
    /// <param name="room">The Room to post in.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>What the wake did.</returns>
    private async Task<WakeOutcome> PostWakeAsync(Batch batch, TaskItem latest, string assigneeName, string senderId, Room room, CancellationToken ct)
    {
        TaskId id = latest.Id;
        TaskActor actor = batch.Actor;
        bool isAgent = actor.Kind == TaskActorKind.Agent;
        WakeOutcome outcome;
        bool counted = false;
        try
        {
            string text = this.Render(latest, actor, assigneeName, batch.Entries);
            if (DecisionSentence(latest, batch.Decision, batch.Note) is { } sentence)
            {
                text = text + "\n" + sentence;
            }

            if (string.Equals(senderId, KnownIds.Huddle, StringComparison.Ordinal))
            {
                // A Huddle post is exempt from the Room's Budget, but the assignee's reply is not: into a spent Room the wake would read
                // Woken while the reply is refused, so it is recorded as an Agent's refused wake is and not posted.
                if (this.chat.GetBudget(room.Id).Exhausted)
                {
                    LogBlocked(this.logger, id, WakeBlock.BudgetPaused);
                    return WakeOutcome.BudgetSpent;
                }

                _ = await this.chat.PostAsHuddleAsync(room.Id, text, ct).ConfigureAwait(false);
            }
            else
            {
                await this.chat.PostAsync(room.Id, senderId, text, ct: ct).ConfigureAwait(false);
            }

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

        return outcome;
    }

    /// <summary>Records one wake outcome in <see cref="TaskActivity"/> (Spec §10.5, §10.7), stamped with the clock at fire time.</summary>
    /// <param name="id">The Task.</param>
    /// <param name="assigneeName">The assignee the wake targeted.</param>
    /// <param name="room">The Room posted in, or <see langword="null"/> when none was chosen.</param>
    /// <param name="outcome">What the wake did.</param>
    private void RecordOutcome(TaskId id, string assigneeName, Room? room, WakeOutcome outcome) =>
        this.activity.Record(new WakeRecord(id, assigneeName, room?.Id, room?.Name, outcome, this.clock.GetUtcNow()));

    /// <summary>
    /// The sender of the wake (Spec §10.4): Huddle for a Helper's change (ADR-0065), since a Helper is not the Human and a Message from
    /// the Human would answer the Human's own Mentions; the Human for a Human or outside-Huddle change; the Agent's own user otherwise.
    /// </summary>
    /// <param name="actor">Who made the change.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>The sender's user id, or <see langword="null"/> when an Agent actor has no user row.</returns>
    private async Task<string?> ResolveSenderIdAsync(TaskActor actor, CancellationToken ct)
    {
        if (actor.Kind == TaskActorKind.Helper)
        {
            return KnownIds.Huddle;
        }

        if (actor.Kind != TaskActorKind.Agent)
        {
            return KnownIds.Human;
        }

        if (actor.UserId is { } userId)
        {
            return userId;
        }

        User? user = await this.directory.FindUserByNameAsync(actor.Name, ct).ConfigureAwait(false);
        return user?.Id;
    }

    /// <summary>
    /// Chooses the Room the wake is posted in, by Spec §10.4 steps 1-4 in order:
    /// <list type="number">
    /// <item><description>The Task's origin Room, when it exists (Archived is allowed, D-12) and both the sender and the assignee are Members.</description></item>
    /// <item><description>The creator Room: S = {Human, creator, assignee}, or {Human, assignee} when the creator is the Human or has no user row (corrections-B3 D9 item 13). Used only when the sender is in S, via <see cref="ITeamDirectory.FindRoomWithExactMemberSetAsync(IReadOnlyCollection{string}, CancellationToken)"/> (which skips Archived Rooms).</description></item>
    /// <item><description>Otherwise - a third Agent made the change (D-13) - the actor Room: S = {Human, sender, assignee}, looked up the same way.</description></item>
    /// <item><description>No Room found: <see cref="ChatService.CreateRoomForAsync(IReadOnlyList{string}, CancellationToken)"/> creates one for the last step's S without the Human (it adds the Human itself, and one Agent gives the direct Room).</description></item>
    /// </list>
    /// Steps 2-4 run in the Task's Team Scope (found and created scoped to <see cref="TaskLocation.Team"/>); a Team the
    /// catalog does not know stays at the root. Every set tried contains the sender (Huddle, which takes no seat, aside), so <see cref="ChatService.PostAsync"/>'s membership check
    /// passes. Runs inside the fire gate, which is what keeps two racing fires from creating two Rooms.
    /// </summary>
    /// <param name="task">The latest Task.</param>
    /// <param name="senderId">The sender's user id.</param>
    /// <param name="assignee">The assignee's user.</param>
    /// <param name="ct">The service lifetime.</param>
    /// <returns>The Room to post in.</returns>
    private async Task<Room> ChooseRoomAsync(TaskItem task, string senderId, User assignee, CancellationToken ct)
    {
        if (await this.FindUsableOriginAsync(task, senderId, assignee, ct).ConfigureAwait(false) is { } origin)
        {
            return origin;
        }

        List<string> memberSet = [KnownIds.Human];
        User? creator = await this.directory.FindUserByNameAsync(task.Creator, ct).ConfigureAwait(false);
        if (creator is { Kind: UserKind.Agent })
        {
            memberSet.Add(creator.Id);
        }

        AddOnce(memberSet, assignee.Id);

        // Huddle takes no seat in any Room (ADR-0065), so there is no set it must belong to; every other sender must.
        if (!string.Equals(senderId, KnownIds.Huddle, StringComparison.Ordinal) && !memberSet.Contains(senderId, StringComparer.Ordinal))
        {
            memberSet = [KnownIds.Human, senderId];
            AddOnce(memberSet, assignee.Id);
        }

        // The fallback Room lives in the Task's Team Scope (Team chats D-15); a Team the catalog does not know stays at the root.
        string? scope = this.teams?.Find(task.Location.Team)?.Name;
        Room? existing = await this.directory.FindRoomWithExactMemberSetAsync(memberSet, scope, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        List<string> agentIds = [.. memberSet.Where(static id => !string.Equals(id, KnownIds.Human, StringComparison.Ordinal))];
        return await this.chat.CreateRoomForAsync(agentIds, scope, FeedActor.Huddle, ct).ConfigureAwait(false);
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

        Room? origin = await this.directory.GetRoomAsync(originId, ct).ConfigureAwait(false);
        if (origin is null)
        {
            return null;
        }

        IReadOnlyList<User> members = await this.directory.GetRoomMembersAsync(originId, ct).ConfigureAwait(false);
        bool senderIsMember = string.Equals(senderId, KnownIds.Huddle, StringComparison.Ordinal)
            || members.Any(m => string.Equals(m.Id, senderId, StringComparison.Ordinal));
        bool assigneeIsMember = members.Any(m => string.Equals(m.Id, assignee.Id, StringComparison.Ordinal));
        return senderIsMember && assigneeIsMember ? origin : null;
    }

    /// <summary>The sentence that quotes the Human's decision (Human gate Spec §6.6), appended as the Message's last line.</summary>
    /// <param name="task">The latest Task, whose Action an Accept names.</param>
    /// <param name="decision">The batch's decision, if any.</param>
    /// <param name="note">The note given with it.</param>
    /// <returns>The sentence, or <see langword="null"/> when there is nothing to quote.</returns>
    private static string? DecisionSentence(TaskItem task, Agency.Huddle.App.Tasks.HumanGate.TaskDecision? decision, string? note) => decision switch
    {
        Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Accept when task.Action is { } action =>
            $"Accepted: go ahead and {NeutraliseMentions(action)}, then move {task.Id} to Done.",
        Agency.Huddle.App.Tasks.HumanGate.TaskDecision.RequestChanges when note is not null =>
            $"Changes requested: \"{NeutraliseMentions(note)}\"",
        Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Decline when note is not null =>
            $"Declined: \"{NeutraliseMentions(note)}\"",
        _ => null,
    };

    /// <summary>Posts the Human's <paramref name="text"/> to <paramref name="owner"/> of a Proof item, in the Room <see cref="ChooseRoomAsync"/> picks with the owner as a Member.</summary>
    /// <param name="task">The Task the item belongs to.</param>
    /// <param name="owner">The item's owner, by Name.</param>
    /// <param name="text">The text to post, exactly as given.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns>The Room's display name, or a failure when the owner is unknown or the post is refused.</returns>
    internal async Task<AskOutcome> AskAsync(TaskItem task, string owner, string text, CancellationToken ct)
    {
        User? ownerUser = await this.directory.FindUserByNameAsync(owner, ct).ConfigureAwait(false);
        if (ownerUser is null)
        {
            return AskOutcome.Failure;
        }

        try
        {
            Room room = await this.ChooseRoomAsync(task, KnownIds.Human, ownerUser, ct).ConfigureAwait(false);
            await this.chat.PostAsync(room.Id, KnownIds.Human, text, ct: ct).ConfigureAwait(false);
            return new AskOutcome(true, room.Name);
        }
        catch (ChatException ex)
        {
            LogRoomRefused(this.logger, task.Id, ex.Code, ex);
            return AskOutcome.Failure;
        }
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

    /// <summary>Logs that a Request's requesting Team has no manager to wake.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="taskId">The Request.</param>
    /// <param name="team">The requesting Team.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Not waking the requesting manager of Request {TaskId}: team '{Team}' has no manager.")]
    private static partial void LogNoRequestingManager(ILogger logger, TaskId taskId, string team);

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

        /// <summary>
        /// When the batch's first change arrived, at full clock precision. Change log entries are cut to whole seconds, so a
        /// Request wake posted in the same second as a later, unrelated change would otherwise look as if it covered that change.
        /// </summary>
        public DateTimeOffset FirstChangeAt { get; init; }

        /// <summary>Every batched change's Change log entry, in arrival order.</summary>
        public List<ChangeLogEntry> Entries { get; } = [];

        /// <summary>The Human's latest decision in this batch, or <see langword="null"/> when none was made.</summary>
        public Agency.Huddle.App.Tasks.HumanGate.TaskDecision? Decision { get; set; }

        /// <summary>The note given with <see cref="Decision"/>.</summary>
        public string? Note { get; set; }

        /// <summary>The coalescing timer, or <see langword="null"/> when the batch fires directly.</summary>
        public ITimer? Timer { get; set; }

        /// <summary>Whether any batched change moved the Task's status (RQ-4 wakes the requesting manager on those).</summary>
        public bool StatusChanged { get; set; }

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
