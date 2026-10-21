using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The only way to change a Task (Spec §9): validates a <see cref="TaskDraft"/> or a
/// <see cref="TaskPatch"/> (Spec §9.2), builds the Change log entry, writes or moves the file
/// through <see cref="TaskStore"/>, and raises <see cref="TaskEvents.TaskChanged"/> after the write,
/// outside every lock. <see cref="Create"/>, <see cref="Update"/>, <see cref="Close"/>,
/// <see cref="Reopen"/> and outside-edit logging (<see cref="OnOutsideEdit"/>) are implemented so
/// far (Tasks 6.1-6.5); Teammate renaming follows in Task 6.6. Subscribes to
/// <see cref="TaskStore.OutsideEditDetected"/> and <see cref="TaskStore.IndexChanged"/> in its
/// constructor, so it also implements <see cref="IDisposable"/> to unsubscribe from both
/// (Spec §9.4, traps.md L98).
/// </summary>
internal sealed class TaskService : IDisposable
{
    /// <summary>The fixed Windows-illegal characters, enforced on every OS (Settled corrections-B2 D6 item 5) - not <see cref="Path.GetInvalidFileNameChars"/>, which on Linux is only NUL and '/'.</summary>
    private static readonly char[] IllegalFolderNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>The reserved Windows device names, checked with and without an extension, case-insensitively (Settled corrections-B2 D6 item 5).</summary>
    private static readonly HashSet<string> ReservedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Serialises every mutating method against every other one (Settled corrections-B2 D6 item 2).
    // Lock order: mutateGate -> TaskStore.writeGate, so this class never calls PersonaStore or
    // raises an event while a writeGate is held. Create and Update below hold this for their whole
    // "read current, validate, write" sequence and release it before TaskChanged is raised.
    private const string ApprovedByHumanOnly = "Only the Human can set approved_by.";

    private readonly Lock mutateGate = new();

    private readonly TaskStore store;
    private readonly TaskIdAllocator ids;
    private readonly TaskEvents events;
    private readonly PersonaStore personas;
    private readonly ISprintCatalog sprints;
    private readonly TimeProvider clock;
    private readonly ILogger<TaskService> logger;
    private readonly ProcessGateService? gates;
    private readonly IHumanIdentity human;
    private readonly IProofSource? proofSource;
    private readonly IFeedPublisher? feed;
    private readonly IRequestConfirmers? confirmers;

    /// <summary>
    /// Wires this service to the Task index and to the Change log's clock. Deliberately does not
    /// take <see cref="Data.ITeamDirectory"/> (Settled corrections-B2 D6 item 9): it is unused,
    /// because the Human's Name comes from <see cref="IHumanIdentity"/>, not from a Room
    /// membership lookup.
    /// </summary>
    /// <param name="store">Owns the Task files this service validates against and writes through.</param>
    /// <param name="ids">Allocates a new Task's id.</param>
    /// <param name="events">The hub this service raises <see cref="TaskEvents.TaskChanged"/> and re-raised reloads on.</param>
    /// <param name="personas">Supplies known Team labels and resolves an assignee's Alias to its Name.</param>
    /// <param name="sprints">The read-only Sprint catalogue a Sprint is validated and labelled against (Sprints spec §9.2, §9.4).</param>
    /// <param name="options">Unused beyond the null guard; kept so every existing caller compiles.</param>
    /// <param name="clock">Supplies the Change log entry's timestamp.</param>
    /// <param name="logger">Used for diagnostics; unused so far.</param>
    /// <param name="gates">Decides whether the Team's process gates refuse a status change (PR-7); <see langword="null"/> enforces none.</param>
    /// <param name="human">The Human's live identity; <see langword="null"/> fixes it at <see cref="TeamOptions.HumanName"/>.</param>
    /// <param name="proof">Evaluates a Task's Proof for the Human gate rules; <see langword="null"/> means an empty, complete Proof.</param>
    /// <param name="feed">Receives the Feed items each change produces (Feed spec §6.5.1); <see langword="null"/> posts nothing.</param>
    /// <param name="confirmers">Tells whether a Team has a member who can confirm a Request; <see langword="null"/> means none can.</param>
    public TaskService(
        TaskStore store,
        TaskIdAllocator ids,
        TaskEvents events,
        PersonaStore personas,
        ISprintCatalog sprints,
        IOptions<TeamOptions> options,
        TimeProvider clock,
        ILogger<TaskService> logger,
        ProcessGateService? gates = null,
        IHumanIdentity? human = null,
        IProofSource? proof = null,
        IFeedPublisher? feed = null,
        IRequestConfirmers? confirmers = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(sprints);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.store = store;
        this.ids = ids;
        this.events = events;
        this.personas = personas;
        this.sprints = sprints;
        this.clock = clock;
        this.logger = logger;
        this.gates = gates;
        this.proofSource = proof;
        this.feed = feed;
        this.confirmers = confirmers;
        this.human = human ?? new ConfiguredHumanIdentity(options.Value.HumanName);

        // Settled corrections-B2 D6 item 13: TaskEvents is a plain hub; this is the one place that
        // turns TaskStore's own index-rebuild notification into the same event a Task mutation
        // raises, so a UI list needs only one subscription regardless of which one fired.
        this.store.IndexChanged += this.events.RaiseTasksReloaded;

        // Spec §9.4: turns an edit made outside Huddle into a Change log entry. Unsubscribed in
        // Dispose alongside IndexChanged above (traps.md L98).
        this.store.OutsideEditDetected += this.OnOutsideEdit;
    }

    /// <summary>Unsubscribes from every <see cref="TaskStore"/> event this service subscribed to in its constructor.</summary>
    public void Dispose()
    {
        this.store.OutsideEditDetected -= this.OnOutsideEdit;
        this.store.IndexChanged -= this.events.RaiseTasksReloaded;
    }

    /// <summary>
    /// Validates <paramref name="draft"/> (Spec §9.2), allocates its id, and writes it as a brand
    /// new file that never overwrites an existing one (Settled corrections-B2 D6 item 6).
    /// </summary>
    /// <param name="draft">The new Task.</param>
    /// <param name="actor">Who is creating it; becomes the Task's <see cref="TaskItem.Creator"/> and the Change log entry's actor.</param>
    /// <param name="humanOverridesGates">Whether a Human creation into a gated status passes a refusing process gate.</param>
    public TaskResult Create(TaskDraft draft, TaskActor actor, bool humanOverridesGates = false)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.CreateCore(draft, actor, humanOverridesGates);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        return result;
    }

    /// <summary>
    /// Applies <paramref name="patch"/> to the current Task and writes the result (Spec §9.4),
    /// merging or conflicting against a stale <paramref name="baseVersion"/> first (Spec §9.3).
    /// </summary>
    /// <param name="id">The Task to update.</param>
    /// <param name="patch">Only the fields being changed.</param>
    /// <param name="baseVersion">
    /// The <see cref="TaskItem.Version"/> the caller last saw, or <see langword="null"/> when the
    /// caller didn't track one (tools, drags - Spec §9.3). <see langword="null"/>, or a value equal
    /// to the Task's current version, applies the patch outright; a stale value is merged against
    /// what changed since, or reported as a <see cref="TaskResult.Conflict"/>.
    /// </param>
    /// <param name="actor">Who is making the change.</param>
    /// <param name="humanOverridesGates">
    /// When <see langword="true"/> the Human's status change passes a refusing process gate and the
    /// override is logged; when <see langword="false"/> (the default) the Human gets the <see cref="TaskResult.GateRefused"/> like
    /// anyone else, so the Task panel can show it and offer "Move anyway" (PR-7).
    /// </param>
    public TaskResult Update(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor, bool humanOverridesGates = false)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        RefusedMove? refusedMove;
        lock (this.mutateGate)
        {
            (result, change) = this.UpdateCore(id, patch, baseVersion, actor, humanOverridesGates, out refusedMove);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        if (refusedMove is not null)
        {
            this.PublishRefusal(refusedMove, result, actor);
        }

        return result;
    }

    /// <summary>
    /// Records the Human's decision on a Task waiting for them (Human gate Spec §6.6): Accept, Request changes or Decline.
    /// </summary>
    /// <param name="id">The Task.</param>
    /// <param name="decision">What the Human decides.</param>
    /// <param name="note">The reason or the requested changes; required for Request changes and Decline.</param>
    /// <param name="baseVersion">The version the Human was shown; a mismatch returns <see cref="TaskResult.Conflict"/>.</param>
    /// <param name="actor">Who decides; must be the Human.</param>
    public TaskResult Decide(TaskId id, TaskDecision decision, string? note, string baseVersion, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(baseVersion);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.DecideCore(id, decision, note, baseVersion, actor);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        return result;
    }

    /// <summary>The body of <see cref="Decide"/>, run under <see cref="mutateGate"/>: checks, then one write.</summary>
    /// <param name="id">The Task.</param>
    /// <param name="decision">What the Human decides.</param>
    /// <param name="note">The note.</param>
    /// <param name="baseVersion">The version the Human was shown.</param>
    /// <param name="actor">Who decides.</param>
    private (TaskResult Result, TaskChange? Change) DecideCore(TaskId id, TaskDecision decision, string? note, string baseVersion, TaskActor actor)
    {
        TaskItem? current = this.store.Get(id);
        string? text = this.store.ReadText(id);
        if (current is null || text is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        if (actor.Kind != TaskActorKind.Human)
        {
            return (new TaskResult.Refused([string.Create(CultureInfo.InvariantCulture, $"Only {this.human.Name} can decide on {id}.")]), null);
        }

        if (!string.Equals(baseVersion, current.Version, StringComparison.Ordinal))
        {
            return (new TaskResult.Conflict(current, []), null);
        }

        string? problem = this.DecisionProblem(current, decision, note);
        if (problem is not null)
        {
            return (new TaskResult.Refused([problem]), null);
        }

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        string? trimmed = note?.Trim();
        return decision switch
        {
            TaskDecision.Accept => this.AcceptCore(current, text, at, actor),
            TaskDecision.RequestChanges => this.RewriteCore(current, text, TaskState.InProgress, "changes requested: " + trimmed, at, actor, decision, trimmed),
            _ => this.DeclineCore(current, text, "declined: " + trimmed, at, actor, trimmed),
        };
    }

    /// <summary>The refusal text for a decision that does not apply, or <see langword="null"/> when it does.</summary>
    /// <param name="current">The Task now.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="note">The note.</param>
    private string? DecisionProblem(TaskItem current, TaskDecision decision, string? note)
    {
        string notWaiting = string.Create(CultureInfo.InvariantCulture, $"{current.Id} isn't waiting for your decision.");
        bool waiting = !current.Location.Closed
            && (decision == TaskDecision.Accept
                ? current.Status == TaskState.NeedsYou && Acceptance.Of(current).State != AcceptanceState.Valid && this.ProofOf(current).IsComplete
                : current.Status is TaskState.Review or TaskState.NeedsYou);
        if (!waiting)
        {
            return notWaiting;
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return decision switch
            {
                TaskDecision.RequestChanges => "Say what needs to change.",
                TaskDecision.Decline => "Say why you're declining.",
                _ => null,
            };
        }

        return null;
    }

    /// <summary>Accept: appends <c>accepted &lt;hash&gt;</c>, and for a Task with no Action also moves it to Done, in one write.</summary>
    /// <param name="current">The Task now, in Needs You.</param>
    /// <param name="text">Its file text.</param>
    /// <param name="at">When.</param>
    /// <param name="actor">The Human.</param>
    private (TaskResult Result, TaskChange? Change) AcceptCore(TaskItem current, string text, DateTimeOffset at, TaskActor actor)
    {
        ChangeLogEntry accepted = new(at, actor.Name, "accepted " + ContentFingerprint.Of(current.Title, current.Action, current.Description));
        if (!string.IsNullOrWhiteSpace(current.Action))
        {
            return this.WriteDecision(current, TaskFileFormat.AppendEntry(text, accepted), current.Location, [], accepted, actor, TaskDecision.Accept, null);
        }

        TaskItem withAcceptance = current with { ChangeLog = [.. current.ChangeLog, accepted] };
        TaskItem done = withAcceptance with { Status = TaskState.Done };
        if (HumanGateRules.Check(withAcceptance, done, actor, this.ProofOf(done), this.human.Name) is { } refusal)
        {
            return (new TaskResult.GateRefused(refusal.Gate, refusal.Message, CanOverride: false), null);
        }

        return this.RewriteCore(current, TaskFileFormat.AppendEntry(text, accepted), TaskState.Done, null, at, actor, TaskDecision.Accept, null);
    }

    /// <summary>Writes a status change with an optional leading note entry and the status entry, in one write.</summary>
    /// <param name="current">The Task now.</param>
    /// <param name="text">Its file text (possibly with entries already appended).</param>
    /// <param name="status">The new status.</param>
    /// <param name="noteEntry">The note entry summary, or <see langword="null"/> for none.</param>
    /// <param name="at">When.</param>
    /// <param name="actor">The Human.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="note">The note.</param>
    private (TaskResult Result, TaskChange? Change) RewriteCore(TaskItem current, string text, TaskState status, string? noteEntry, DateTimeOffset at, TaskActor actor, TaskDecision decision, string? note)
    {
        TaskItem candidate = current with { Status = status };
        IReadOnlyList<FieldChange> diff = TaskDiff.Compare(current, candidate);
        string withNote = noteEntry is null ? text : TaskFileFormat.AppendEntry(text, new ChangeLogEntry(at, actor.Name, noteEntry));
        ChangeLogEntry statusEntry = new(at, actor.Name, TaskDiff.Summarise(diff));
        string finalText = TaskFileFormat.AppendEntry(TaskFileFormat.ReplaceHead(withNote, candidate), statusEntry);
        return this.WriteDecision(current, finalText, current.Location, diff, statusEntry, actor, decision, note);
    }

    /// <summary>Decline: rejects the Task and moves it to the closed location in one <see cref="TaskStore.Move"/>.</summary>
    /// <param name="current">The Task now.</param>
    /// <param name="text">Its file text.</param>
    /// <param name="declined">The <c>declined: ...</c> entry summary.</param>
    /// <param name="at">When.</param>
    /// <param name="actor">The Human.</param>
    /// <param name="note">The reason.</param>
    private (TaskResult Result, TaskChange? Change) DeclineCore(TaskItem current, string text, string declined, DateTimeOffset at, TaskActor actor, string? note)
    {
        TaskItem candidate = current with { Status = TaskState.Rejected };
        IReadOnlyList<FieldChange> diff = TaskDiff.Compare(current, candidate);
        ChangeLogEntry closed = new(at, actor.Name, "closed");
        string withDeclined = TaskFileFormat.AppendEntry(TaskFileFormat.ReplaceHead(text, candidate), new ChangeLogEntry(at, actor.Name, declined));
        string finalText = TaskFileFormat.AppendEntry(withDeclined, closed);
        return this.WriteDecision(current, finalText, current.Location with { Closed = true }, diff, closed, actor, TaskDecision.Decline, note);
    }

    /// <summary>Writes the decided text (moving it when the location changed) and builds the result and the one change.</summary>
    /// <param name="current">The Task now.</param>
    /// <param name="finalText">The full new file text.</param>
    /// <param name="location">Where the Task ends up.</param>
    /// <param name="diff">What changed.</param>
    /// <param name="entry">The last entry written.</param>
    /// <param name="actor">The Human.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="note">The note.</param>
    private (TaskResult Result, TaskChange? Change) WriteDecision(TaskItem current, string finalText, TaskLocation location, IReadOnlyList<FieldChange> diff, ChangeLogEntry entry, TaskActor actor, TaskDecision decision, string? note)
    {
        TaskItem? written = location == current.Location
            ? this.store.Write(current, current.Version, finalText)
            : this.store.Move(current, current.Version, location, finalText);
        if (written is null)
        {
            return (new TaskResult.Conflict(this.store.Get(current.Id) ?? current, []), null);
        }

        TaskChange change = new(current, written, diff, actor, entry, decision, note);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>
    /// Moves <paramref name="id"/> into its Team's (or Project's) "_closed" folder (Spec §9): refused
    /// when it's already Closed. Unlike <see cref="Update"/>, there is no <c>baseVersion</c> - a
    /// concurrent change to the file since it was last read is instead caught by
    /// <see cref="TaskStore.Move"/>'s own version check and reported as a <see cref="TaskResult.Conflict"/>.
    /// </summary>
    /// <param name="id">The Task to close.</param>
    /// <param name="actor">Who is closing it; becomes the Change log entry's actor.</param>
    public TaskResult Close(TaskId id, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.CloseOrReopenCore(id, actor, closing: true);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        return result;
    }

    /// <summary>
    /// Moves <paramref name="id"/> back out of "_closed" (Spec §9): refused when it isn't Closed. See
    /// <see cref="Close"/> for the conflict note.
    /// </summary>
    /// <param name="id">The Task to reopen.</param>
    /// <param name="actor">Who is reopening it; becomes the Change log entry's actor.</param>
    public TaskResult Reopen(TaskId id, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.CloseOrReopenCore(id, actor, closing: false);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        return result;
    }

    /// <summary>
    /// Appends <paramref name="line"/> to the Task's "## Evidence" section (creating it before the Change log when
    /// absent), records "evidence added by {actor}" in the Change log, and never rewrites an earlier line. Like
    /// <see cref="Close"/>, it has no base version: a concurrent change is reported as a <see cref="TaskResult.Conflict"/>.
    /// </summary>
    /// <param name="id">The Task to add Evidence to.</param>
    /// <param name="line">The Evidence line to append.</param>
    /// <param name="actor">Who adds it; becomes the Change log entry's actor.</param>
    public TaskResult AddEvidence(TaskId id, EvidenceLine line, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.AddEvidenceCore(id, line, actor);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }

        return result;
    }

    /// <summary>The body of <see cref="AddEvidence"/>, run under <see cref="mutateGate"/>.</summary>
    private (TaskResult Result, TaskChange? Change) AddEvidenceCore(TaskId id, EvidenceLine line, TaskActor actor)
    {
        TaskItem? current = this.store.Get(id);
        string? currentText = current is null ? null : this.store.ReadText(id);
        if (current is null || currentText is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        ChangeLogEntry entry = new(at, actor.Name, string.Create(CultureInfo.InvariantCulture, $"evidence added by {actor.Name}"));
        string withEvidence = TaskFileFormat.AddEvidenceLine(currentText, line.Format().ReplaceLineEndings(" "));
        string finalText = TaskFileFormat.AppendEntry(withEvidence, entry);

        TaskItem? written = this.store.Write(current, current.Version, finalText);
        if (written is null)
        {
            return (new TaskResult.Conflict(this.store.Get(id) ?? current, []), null);
        }

        TaskChange change = new(current, written, [], actor, entry);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>
    /// Turns an edit made outside Huddle (Spec §9.4) into a Change log entry, subscribed to
    /// <see cref="TaskStore.OutsideEditDetected"/> in the constructor. Runs under
    /// <see cref="mutateGate"/> like every other mutation (Settled corrections-B2 D6 item 2), and
    /// raises <see cref="TaskEvents.TaskChanged"/> outside both locks.
    /// </summary>
    /// <param name="edit">The Task's state before and after the outside edit.</param>
    private void OnOutsideEdit(OutsideEdit edit)
    {
        TaskChange? change;
        lock (this.mutateGate)
        {
            change = this.OnOutsideEditCore(edit);
        }

        if (change is not null)
        {
            this.RaiseChange(change);
        }
    }

    /// <summary>
    /// The body of <see cref="OnOutsideEdit"/>, run under <see cref="mutateGate"/>. Appends the
    /// Change log entry to the file exactly as it now reads (Spec §9.4: the Human's formatting and
    /// unknown keys aren't recomposed), through <see cref="TaskStore.AppendEntry"/> rather than
    /// <see cref="TaskFileFormat.Compose"/>.
    /// </summary>
    /// <param name="edit">The Task's state before and after the outside edit.</param>
    private TaskChange? OnOutsideEditCore(OutsideEdit edit)
    {
        TaskActor actor = new(TaskActorKind.OutsideHuddle, this.human.Name, KnownIds.Human);
        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());

        // Settled corrections-B2 D6 item 8: a file created outside Huddle (no previous Task to
        // diff against) gets the bare summary, with no diff suffix.
        if (edit.Before is null)
        {
            return this.OnOutsideCreation(edit.After, actor, at);
        }

        if (Acceptance.AcceptedCount(edit.After) > Acceptance.AcceptedCount(edit.Before))
        {
            // The forged entry is refused and logged, then the rest of the same write (a status move, a gate switch) is
            // judged against the re-read Task, so one write cannot forge an acceptance and use it.
            TaskChange? forged = this.RefuseForgedAcceptance(edit.After, actor, at);
            return forged is null ? null : this.ContinueOutsideEdit(new OutsideEdit(edit.Before, forged.After), actor, at) ?? forged;
        }

        return this.ContinueOutsideEdit(edit, actor, at);
    }

    /// <summary>The part of an outside edit of an existing Task that follows the forged-acceptance check: the Human gate, the process gates and the log entry.</summary>
    /// <param name="edit">The Task's state before and after the outside edit.</param>
    /// <param name="actor">The outside-edit actor.</param>
    /// <param name="at">When the edit was seen.</param>
    private TaskChange? ContinueOutsideEdit(OutsideEdit edit, TaskActor actor, DateTimeOffset at)
    {
        if (edit.Before is null)
        {
            return null;
        }

        IReadOnlyList<FieldChange> changes = this.LabelSprintChange(TaskDiff.Compare(edit.Before, edit.After), edit.Before, edit.After);
        if (changes.Count == 0)
        {
            return null;
        }

        // An outside edit cannot dodge a gate: a move into a gated status that the gate refuses is reverted
        // (head only; the body and Evidence stay as written) and logged. Evidence-only and approved edits are
        // not reverted: they only matter when the status next moves, which runs the gate again.
        bool statusChanged = edit.Before.Status != edit.After.Status;
        bool gateTouched = edit.Before.HumanGate != edit.After.HumanGate
            || !string.Equals(edit.Before.Action, edit.After.Action, StringComparison.Ordinal)
            || edit.Before.IsGated
            || edit.After.IsGated;
        string? afterText = (statusChanged && edit.After.Status is TaskState.InProgress or TaskState.Review or TaskState.Done or TaskState.NeedsYou) || gateTouched
            ? this.store.ReadText(edit.After.Id)
            : null;
        if (afterText is not null && this.RevertHumanGateEdit(edit, afterText, actor, at) is { } humanReverted)
        {
            return humanReverted;
        }

        if (afterText is not null && statusChanged && edit.After.Status is TaskState.InProgress or TaskState.Review or TaskState.Done
            && this.CheckGates(edit.Before, edit.After, afterText, actor) is { } refusal)
        {
            ChangeLogEntry refused = new(at, actor.Name, string.Create(CultureInfo.InvariantCulture, $"outside edit refused by gate: {refusal.Message}"));
            string reverted = TaskFileFormat.AppendEntry(TaskFileFormat.ReplaceHead(afterText, edit.Before), refused);
            if (this.store.Write(edit.After, edit.After.Version, reverted) is { } restored)
            {
                return new TaskChange(edit.After, restored, TaskDiff.Compare(edit.After, restored), actor, refused);
            }
        }

        string summary = string.Create(CultureInfo.InvariantCulture, $"edited outside Huddle: {TaskDiff.Summarise(changes)}");
        ChangeLogEntry entry = new(at, actor.Name, summary);
        TaskItem? written = this.store.AppendEntry(edit.After.Id, edit.After.Version, entry);

        return written is null ? null : new TaskChange(edit.Before, written, changes, actor, entry);
    }

    /// <summary>A file created outside Huddle: reset when the Human gate refuses its status, else logged with the bare summary.</summary>
    /// <param name="created">The new Task as the file reads.</param>
    /// <param name="actor">The outside-edit actor.</param>
    /// <param name="at">When the file was seen.</param>
    private TaskChange? OnOutsideCreation(TaskItem created, TaskActor actor, DateTimeOffset at)
    {
        if (this.ResetGatedCreation(created, actor, at) is { } reset)
        {
            return reset;
        }

        ChangeLogEntry bareEntry = new(at, actor.Name, "edited outside Huddle");
        TaskItem? bareWritten = this.store.AppendEntry(created.Id, created.Version, bareEntry);

        // null means the disk version moved again before this append reached it; a newer save
        // will be reported by the next rebuild, so this one is silently skipped (Settled
        // corrections-B2 D6 item 8).
        return bareWritten is null ? null : new TaskChange(null, bareWritten, [], actor, bareEntry);
    }

    /// <summary>
    /// Reverts what the Human gate refuses in an outside edit: the status, <c>human_gate</c> and <c>action</c> go back to
    /// what they were, while the rest of the edit (title, description, Evidence) stays, and the refusal is logged.
    /// </summary>
    /// <param name="edit">The outside edit.</param>
    /// <param name="afterText">The file text as it now reads.</param>
    /// <param name="actor">The outside-edit actor.</param>
    /// <param name="at">When the edit was seen.</param>
    /// <returns>The change to raise, or <see langword="null"/> when the Human gate refuses nothing (or the file moved on).</returns>
    private TaskChange? RevertHumanGateEdit(OutsideEdit edit, string afterText, TaskActor actor, DateTimeOffset at)
    {
        TaskItem before = edit.Before ?? edit.After;
        bool intoDoneOrNeedsYou = before.Status != edit.After.Status && edit.After.Status is TaskState.NeedsYou or TaskState.Done;
        Proof proof = intoDoneOrNeedsYou ? this.ProofOf(edit.After) : Proof.Empty;
        if (HumanGateRules.Check(before, edit.After, actor, proof, this.human.Name) is not { } refusal)
        {
            return null;
        }

        ChangeLogEntry refused = new(at, actor.Name, string.Create(CultureInfo.InvariantCulture, $"outside edit refused by Human gate: {refusal.Message}"));
        TaskItem reverted = edit.After with { Status = before.Status, HumanGate = before.HumanGate, Action = before.Action };
        string text = TaskFileFormat.AppendEntry(TaskFileFormat.ReplaceHead(afterText, reverted), refused);
        return this.store.Write(edit.After, edit.After.Version, text) is { } restored
            ? new TaskChange(edit.After, restored, TaskDiff.Compare(edit.After, restored), actor, refused)
            : null;
    }

    /// <summary>
    /// An <c>accepted</c> entry that an outside edit added is not an acceptance: only Huddle's Accept writes one. The refusal
    /// is logged, and <see cref="Acceptance.Of"/> reads that entry as voiding the acceptance.
    /// </summary>
    /// <param name="after">The Task as the file now reads.</param>
    /// <param name="actor">The outside-edit actor.</param>
    /// <param name="at">When the edit was seen.</param>
    /// <returns>The change to raise, or <see langword="null"/> when the file moved on.</returns>
    private TaskChange? RefuseForgedAcceptance(TaskItem after, TaskActor actor, DateTimeOffset at)
    {
        string summary = string.Create(CultureInfo.InvariantCulture, $"{Acceptance.RefusedPrefix}only {this.human.Name} can accept {after.Id}, in Huddle.");
        ChangeLogEntry refused = new(at, actor.Name, summary);
        return this.store.AppendEntry(after.Id, after.Version, refused) is { } written
            ? new TaskChange(after, written, TaskDiff.Compare(after, written), actor, refused)
            : null;
    }

    /// <summary>
    /// A Task that appears outside Huddle already in Needs You or Done against the Human gate (H-5, H-3) is reset to Review and the refusal logged.
    /// </summary>
    /// <param name="created">The new Task as the file reads.</param>
    /// <param name="actor">The outside-edit actor.</param>
    /// <param name="at">When the file was seen.</param>
    /// <returns>The change to raise, or <see langword="null"/> when H-5 refuses nothing (or the file moved on).</returns>
    private TaskChange? ResetGatedCreation(TaskItem created, TaskActor actor, DateTimeOffset at)
    {
        if (HumanGateRules.Check(null, created, actor, Proof.Empty, this.human.Name) is not { } refusal
            || this.store.ReadText(created.Id) is not { } text)
        {
            return null;
        }

        ChangeLogEntry refused = new(at, actor.Name, string.Create(CultureInfo.InvariantCulture, $"outside edit refused by Human gate: {refusal.Message}"));
        string reset = TaskFileFormat.AppendEntry(TaskFileFormat.ReplaceHead(text, created with { Status = TaskState.Review }), refused);
        return this.store.Write(created, created.Version, reset) is { } restored
            ? new TaskChange(null, restored, [], actor, refused)
            : null;
    }

    /// <summary>
    /// Rewrites <c>creator:</c>/<c>assignee:</c> in every Task, including Closed ones, whose value
    /// equals <paramref name="oldName"/> (Spec §9.6, ordinal case-insensitive). A change of identity,
    /// not of the Task - ADR-0011 says a rename moves the Teammate, not its history - so this writes
    /// with no Change log entry and raises no <see cref="TaskEvents.TaskChanged"/>. Called from
    /// <see cref="Acp.PersonaRenameCascade.OnPersonaRenamed"/> before <c>RenameUser</c>, and must
    /// never throw (Settled corrections-B2 D6 item 11) so a bad Task file never blocks the
    /// race-critical Team Directory rename above it.
    /// </summary>
    /// <param name="oldName">The Teammate's current Name.</param>
    /// <param name="newName">The Teammate's new Name.</param>
    internal void RenameTeammate(string oldName, string newName)
    {
        lock (this.mutateGate)
        {
            this.RenameTeammateCore(oldName, newName);
        }
    }

    /// <summary>The body of <see cref="RenameTeammate"/>, run under <see cref="mutateGate"/>.</summary>
    /// <param name="oldName">The Teammate's current Name.</param>
    /// <param name="newName">The Teammate's new Name.</param>
    private void RenameTeammateCore(string oldName, string newName)
    {
        List<(TaskItem Task, string ExpectedVersion, string Text)> writes = this.CollectRenameWrites(oldName, newName);
        if (writes.Count == 0)
        {
            return;
        }

        IReadOnlyList<TaskItem?> results;
        try
        {
            // One batch, one IndexChanged (Settled corrections-B2 D5 item 7), re-raised as
            // TasksReloaded exactly once by the subscription this class's constructor already holds
            // - no separate raise call is needed here.
            results = this.store.WriteMany(writes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A whole-batch failure - the composed text failed to parse (an invariant break) or a
            // file could not be written at all. Logged and swallowed: renaming a Teammate must never
            // throw, because it runs synchronously before the race-critical Team Directory rename in
            // PersonaRenameCascade.OnPersonaRenamed.
            this.logger.LogWarning(ex, "Could not rename Teammate '{OldName}' to '{NewName}' in one or more Task files.", oldName, newName);
            return;
        }

        for (int i = 0; i < writes.Count; i++)
        {
            if (results[i] is null)
            {
                // A version conflict: the file changed on disk (by hand, or a concurrent Update)
                // between this method's read and its write. Skipped, not retried - the next Update or
                // outside-edit rebuild will see the file as it now is. Logged and continues with the
                // rest of the batch, matching the per-file "never throws" contract.
                this.logger.LogWarning(
                    "Could not rename Teammate '{OldName}' to '{NewName}' in Task '{TaskId}': the file changed on disk since it was read.",
                    oldName,
                    newName,
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                    writes[i].Task.Id);
#pragma warning restore HAA0601
            }
        }
    }

    /// <summary>The write for each Task whose creator or assignee is <paramref name="oldName"/>, with the head recomposed under <paramref name="newName"/>.</summary>
    /// <param name="oldName">The Teammate's current Name.</param>
    /// <param name="newName">The Teammate's new Name.</param>
    private List<(TaskItem Task, string ExpectedVersion, string Text)> CollectRenameWrites(string oldName, string newName)
    {
        List<(TaskItem Task, string ExpectedVersion, string Text)> writes = [];
        foreach (TaskItem task in this.store.All)
        {
            bool matchesAssignee = task.Assignee is { Length: > 0 } assignee && string.Equals(assignee, oldName, StringComparison.OrdinalIgnoreCase);
            bool matchesCreator = string.Equals(task.Creator, oldName, StringComparison.OrdinalIgnoreCase);
            if (!matchesAssignee && !matchesCreator)
            {
                continue;
            }

            string? currentText = this.store.ReadText(task.Id);
            if (currentText is null)
            {
                continue;
            }

            TaskItem candidate = task with
            {
                Assignee = matchesAssignee ? newName : task.Assignee,
                Creator = matchesCreator ? newName : task.Creator,
            };

            string newText = TaskFileFormat.ReplaceHead(currentText, candidate);
            writes.Add((task, task.Version, newText));
        }

        return writes;
    }

    /// <summary>The body of <see cref="Create"/>, run under <see cref="mutateGate"/>. Returns the change to raise, or <see langword="null"/> when nothing was written.</summary>
    private (TaskResult Result, TaskChange? Change) CreateCore(TaskDraft draft, TaskActor actor, bool humanOverridesGates)
    {
        TaskItem candidate = this.BuildCandidate(draft, actor);
        string team = candidate.Location.Team;

        List<string> problems = this.Validate(candidate, touchedFields: null, reason: null);
        if (problems.Count > 0)
        {
            return (new TaskResult.Refused(problems), null);
        }

        // The Human gate (H-5 and H-3) runs first and is never overridable.
        Proof proof = candidate.Status == TaskState.NeedsYou ? this.ProofOf(candidate) : Proof.Empty;
        if (HumanGateRules.Check(null, candidate, actor, proof, this.human.Name) is { } humanRefusal)
        {
            return (new TaskResult.GateRefused(humanRefusal.Gate, humanRefusal.Message, CanOverride: false), null);
        }

        // A Task may be created in a gated status only when no gate refuses it (the Human may override on request).
        GateRefusal? refusal = candidate.Status is TaskState.InProgress or TaskState.Review or TaskState.Done
            ? this.CheckGates(candidate with { Status = TaskState.Backlog }, candidate, string.Empty, actor)
            : null;
        if (refusal is not null && (actor.Kind != TaskActorKind.Human || !humanOverridesGates || candidate.IsGated))
        {
            return (new TaskResult.GateRefused(refusal.Gate, refusal.Message, CanOverride: !candidate.IsGated), null);
        }

        return this.WriteNewTask(candidate, team, refusal, actor);
    }

    /// <summary>The not-yet-numbered Task a draft asks for: its Team and Project made canonical and its assignee resolved.</summary>
    private TaskItem BuildCandidate(TaskDraft draft, TaskActor actor)
    {
        string team = this.CanonicalizeTeam(draft.Team);
        string? project = this.CanonicalizeProject(team, draft.Project);
        string? assignee = this.ResolveAssignee(draft.Assignee);

        return new()
        {
            Id = default,
            Title = draft.Title,
            Status = draft.Status,
            Priority = draft.Priority,
            Creator = actor.Name,
            Assignee = assignee,
            OriginRoomId = draft.OriginRoomId,
            Parent = draft.Parent,
            BlockedBy = draft.BlockedBy ?? [],
            DuplicateOf = null,
            Tags = draft.Tags ?? [],
            StartDate = draft.StartDate,
            DueDate = draft.DueDate,
            Sprint = draft.Sprint,
            HumanGate = draft.HumanGate,
            Action = draft.Action,
            Description = draft.Description,
            UnknownFields = draft.ExtraFields ?? [],
            Location = new TaskLocation(team, project, false),
            Path = "",
            Version = "",
        };
    }

    /// <summary>Numbers and writes a validated candidate, noting a gate the Human overrode; throws when the allocated path is taken.</summary>
    private (TaskResult Result, TaskChange? Change) WriteNewTask(TaskItem candidate, string team, GateRefusal? refusal, TaskActor actor)
    {
        string prefix = this.ids.PrefixFor(team);
        int highestSeen = this.store.HighestNumber(prefix);
        TaskId id = this.ids.Next(team, highestSeen);

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        string createdSummary = refusal is null
            ? "created"
            : string.Create(CultureInfo.InvariantCulture, $"created (gate overridden by Human: {ProcessDocument.ToId(refusal.Gate)})");
        ChangeLogEntry entry = new(at, actor.Name, createdSummary);

        TaskItem toWrite = candidate with
        {
            Id = id,
            Path = TaskLayout.PathFor(this.store.RootDirectory, candidate.Location, id),
            ChangeLog = [entry],
        };

        string text = TaskFileFormat.Compose(toWrite);
        TaskItem? written = this.store.Create(toWrite, text);
        if (written is null)
        {
            // The id allocator guarantees a fresh number per Team, so this only fires when
            // something outside TaskService raced a file onto the exact path it just allocated -
            // an invariant break, not an expected failure, so it's logged and thrown rather than a
            // TaskResult (house principle: expected failures are TaskResults, invariant breaks are
            // exceptions).
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            this.logger.LogError("Task '{TaskId}' could not be created: a file already exists at '{Path}'.", id, toWrite.Path);
#pragma warning restore HAA0601
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Task '{id}' already exists."));
        }

        TaskChange change = new(null, written, [], actor, entry);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>The body of <see cref="Update"/>, run under <see cref="mutateGate"/>. Returns the change to raise, or <see langword="null"/> when nothing was written.</summary>
    private (TaskResult Result, TaskChange? Change) UpdateCore(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor, bool humanOverridesGates, out RefusedMove? refusedMove)
    {
        refusedMove = null;
        TaskItem? current = this.store.Get(id);
        if (current is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        if (patch.ApprovedBy.IsSet && actor.Kind != TaskActorKind.Human)
        {
            return (new TaskResult.Refused([ApprovedByHumanOnly]), null);
        }

        TaskPatch resolvedPatch = this.ResolvePatch(patch, current.Location.Team);
        TaskItem candidate = resolvedPatch.ApplyTo(current);

        List<string> problems = this.Validate(candidate, resolvedPatch.Fields(), resolvedPatch.Reason, !current.HumanGate && candidate.HumanGate);
        if (problems.Count > 0)
        {
            return (new TaskResult.Refused(problems), null);
        }

        IReadOnlyList<FieldChange> diff = this.LabelSprintChange(TaskDiff.Compare(current, candidate), current, candidate);

        if (baseVersion is not null && !string.Equals(baseVersion, current.Version, StringComparison.Ordinal))
        {
            List<TaskField> conflictFields = this.FindConflictingFields(id, baseVersion, current, resolvedPatch, diff);
            if (conflictFields.Count > 0)
            {
                return (new TaskResult.Conflict(current, conflictFields), null);
            }
        }

        if (diff.Count == 0)
        {
            return (new TaskResult.Unchanged(current), null);
        }

        string? currentText = this.store.ReadText(id);
        if (currentText is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        if (LeavesBacklogWithoutTriage(current, candidate) && (actor.Kind != TaskActorKind.Human || !humanOverridesGates))
        {
            return (new TaskResult.Refused([LeaveBacklogRefusal]), null);
        }

        if (this.RefuseUpdate(current, candidate, currentText, actor, humanOverridesGates, out GateRefusal? refusal, out Proof proof) is { } refused)
        {
            if (refused.CanOverride && actor.Kind == TaskActorKind.Agent)
            {
                refusedMove = new RefusedMove(current, candidate.Status);
            }

            return (refused, null);
        }

        return this.WriteUpdate(current, candidate, resolvedPatch, diff, currentText, refusal, actor, proof);
    }

    /// <summary>
    /// Runs the Human gate (Spec §6.4: never overridable, before the process gates) and then the process gates (Spec §Gates),
    /// after validation, the version check and the "nothing changed" exit, so a stale or no-op update never reports a gate.
    /// The Human overrides a process gate, except on a Human-gated Task.
    /// </summary>
    /// <param name="current">The Task now.</param>
    /// <param name="candidate">The Task as the patch would leave it.</param>
    /// <param name="text">The Task's file text.</param>
    /// <param name="actor">Who makes the change.</param>
    /// <param name="humanOverridesGates">Whether the Human asked to override.</param>
    /// <param name="overridden">The process gate refusal the Human overrode, or <see langword="null"/>.</param>
    /// <param name="proof">The Proof evaluated for a move into Needs You or Done, otherwise empty.</param>
    /// <returns>The refusal to return, or <see langword="null"/> when the change may be written.</returns>
    private TaskResult.GateRefused? RefuseUpdate(TaskItem current, TaskItem candidate, string text, TaskActor actor, bool humanOverridesGates, out GateRefusal? overridden, out Proof proof)
    {
        overridden = null;
        bool gated = current.IsGated || candidate.IsGated;
        proof = current.Status != candidate.Status && candidate.Status is TaskState.NeedsYou or TaskState.Done ? this.ProofOf(candidate) : Proof.Empty;
        if (HumanGateRules.Check(current, candidate, actor, proof, this.human.Name) is { } humanRefusal)
        {
            return new TaskResult.GateRefused(humanRefusal.Gate, humanRefusal.Message, CanOverride: false);
        }

        GateRefusal? refusal = current.Status != candidate.Status ? this.CheckGates(current, candidate, text, actor) : null;
        if (refusal is not null && (actor.Kind != TaskActorKind.Human || !humanOverridesGates || gated))
        {
            return new TaskResult.GateRefused(refusal.Gate, refusal.Message, CanOverride: !gated);
        }

        overridden = refusal;
        return null;
    }

    /// <summary>A move an Agent attempted that a process gate refused and only the Human can override.</summary>
    /// <param name="Current">The Task as it is.</param>
    /// <param name="Requested">The status the Agent asked for.</param>
    private sealed record RefusedMove(TaskItem Current, TaskState Requested);

    /// <summary>Raises <see cref="TaskEvents.TaskChanged"/>, then hands the change to the Feed.</summary>
    /// <param name="change">The change that was just written.</param>
    private void RaiseChange(TaskChange change)
    {
        this.events.RaiseTaskChanged(change);
        this.PublishFeed(change);
    }

    /// <summary>Posts and resolves the Feed items for a change. The Feed can never hurt the work, so a failure is logged and dropped.</summary>
    /// <param name="change">The change that was just written.</param>
    private void PublishFeed(TaskChange change)
    {
        if (this.feed is null)
        {
            return;
        }

        try
        {
            FeedEffects effects = TaskFeedRules.For(change, this.human.Name, team => this.confirmers?.TeamHasConfirmer(team) ?? false, this.ProofOf);
            foreach (FeedMessage message in effects.Posts)
            {
                this.feed.Post(message);
            }

            foreach (FeedResolution resolution in effects.Resolutions)
            {
                this.feed.Resolve(resolution.TargetKey, resolution.Type);
            }
        }
        catch (Exception ex)
        {
            // The Feed records and never decides (Feed spec policy 4): a failing rule or publisher must not fail the Task write that caused it.
            this.logger.LogWarning(ex, "The Feed could not record the change to Task {TaskId}.", change.After.Id.ToString());
        }
    }

    /// <summary>Posts the Notification for a refused move that only the Human can override (Feed spec §6.5.1).</summary>
    /// <param name="move">The refused move.</param>
    /// <param name="result">The refusal that was returned.</param>
    /// <param name="actor">The Agent that asked.</param>
    private void PublishRefusal(RefusedMove move, TaskResult result, TaskActor actor)
    {
        if (this.feed is null || result is not TaskResult.GateRefused refusal)
        {
            return;
        }

        try
        {
            string id = move.Current.Id.ToString();
            GateDecisionPayload payload = new(id, move.Current.Title, move.Current.Location.Team, ProcessDocument.ToId(refusal.Gate), refusal.Message, move.Current.Status.ToWire(), move.Requested.ToWire());
            this.feed.Post(new FeedMessage(FeedSourceType.GateDecisionDue, id, new FeedActor(FeedActorKind.Agent, actor.Name), true, payload));
        }
        catch (Exception ex)
        {
            // Same rule as PublishFeed: the refusal the Agent sees must not change because the Feed failed.
            this.logger.LogWarning(ex, "The Feed could not record the refused move of Task {TaskId}.", move.Current.Id.ToString());
        }
    }

    /// <summary>Evaluates a Task's Proof, or returns the empty (complete) Proof when no source is wired.</summary>
    /// <param name="task">The Task.</param>
    private Proof ProofOf(TaskItem task) => this.proofSource?.For(task) ?? Proof.Empty;

    /// <summary>Appends the Change log entry for an accepted update and writes the Task, moving it when its Team or Project changed.</summary>
    private (TaskResult Result, TaskChange? Change) WriteUpdate(TaskItem current, TaskItem candidate, TaskPatch resolvedPatch, IReadOnlyList<FieldChange> diff, string currentText, GateRefusal? refusal, TaskActor actor, Proof proof)
    {
        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        string summary = TaskDiff.Summarise(diff);
        if (current.Status != TaskState.NeedsYou && candidate.Status == TaskState.NeedsYou)
        {
            string counts = string.Create(CultureInfo.InvariantCulture, $"{proof.MetCount}/{proof.Total}");
            summary = summary.Replace(" → Needs You", $" → Needs You (Proof {counts})", StringComparison.Ordinal);
        }

        if (resolvedPatch.Reason is { Length: > 0 } reason)
        {
            summary = string.Create(CultureInfo.InvariantCulture, $"{summary} (reason: {reason})");
        }

        if (refusal is not null)
        {
            summary = string.Create(CultureInfo.InvariantCulture, $"{summary} (gate overridden by Human: {ProcessDocument.ToId(refusal.Gate)})");
        }

        ChangeLogEntry entry = new(at, actor.Name, summary);

        string headReplaced = TaskFileFormat.ReplaceHead(currentText, candidate);
        string finalText = TaskFileFormat.AppendEntry(headReplaced, entry);

        bool moved = !string.Equals(current.Location.Team, candidate.Location.Team, StringComparison.Ordinal) ||
            !string.Equals(current.Location.Project, candidate.Location.Project, StringComparison.Ordinal);

        TaskItem? written = moved
            ? this.store.Move(current, current.Version, candidate.Location, finalText)
            : this.store.Write(current, current.Version, finalText);

        if (written is null)
        {
            return (new TaskResult.Conflict(this.store.Get(current.Id) ?? current, []), null);
        }

        TaskChange change = new(current, written, diff, actor, entry, Reason: resolvedPatch.Reason);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>
    /// Asks the Team's process gates whether a status change is allowed (PR-7). Checks both the Task's
    /// current Team and, when the same patch moves it, the destination Team, so a move cannot dodge a gate.
    /// </summary>
    /// <param name="current">The Task before the change.</param>
    /// <param name="candidate">The Task as the patch would leave it.</param>
    /// <param name="text">The Task's file text, for its Evidence section.</param>
    /// <param name="actor">Who makes the change.</param>
    /// <returns>The first refusal, or <see langword="null"/> when no gate refuses or none is configured.</returns>
    private GateRefusal? CheckGates(TaskItem current, TaskItem candidate, string text, TaskActor actor)
    {
        if (this.gates is null)
        {
            return null;
        }

        IReadOnlyList<EvidenceLine> evidence = EvidenceLine.ParseSection(text);
        Dictionary<string, string> covers = new(StringComparer.Ordinal);
        foreach (string author in evidence.Select(static line => line.Author).Where(static name => name.Length > 0).Distinct(StringComparer.Ordinal))
        {
            if (this.personas.Get(author) is { } persona
                && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
                && identity.Role is { } role
                && this.gates.CoverageOf(role) is { } coverage)
            {
                covers[author] = coverage;
            }
        }

        bool isBug = HasBugMarker(current) || HasBugMarker(candidate);
        bool parentApproved = candidate.Parent is not { } parentId
            || (this.store.Get(parentId) is { } parent && IsApproved(parent));

        foreach (string team in new[] { current.Location.Team, candidate.Location.Team }.Distinct(StringComparer.Ordinal))
        {
            GateContext context = new(team, current.Id, current.Status.ToWire(), candidate.Status.ToWire(), isBug, parentApproved, evidence, covers, actor.Name);
            if (this.gates.CheckTaskChange(context) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>Refusal for an agent moving a Request out of Backlog without a triage accept or decline.</summary>
    private const string LeaveBacklogRefusal = "A Request leaves Backlog only when its receiving manager accepts or declines it with triage_request.";

    /// <summary>Refusal for an agent closing a Request nobody confirmed.</summary>
    private const string CloseRequestRefusal = "A Request closes only after the requesting Team's QA confirms it with confirm_request.";

    /// <summary>Reads the Request fields of a Task.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="fields">The fields when the Task is a Request.</param>
    private static bool TryReadRequest(TaskItem task, [NotNullWhen(true)] out RequestFields? fields)
    {
        Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in task.UnknownFields)
        {
            _ = frontmatter.TryAdd(field.Key, field.Value);
        }

        return RequestFields.TryRead(frontmatter, out fields) && fields is not null;
    }

    /// <summary>Whether a change takes a Request out of Backlog other than by the triage that accepts or declines it (Spec §Lifecycle).</summary>
    /// <param name="current">The Task now.</param>
    /// <param name="candidate">The Task after the change.</param>
    private static bool LeavesBacklogWithoutTriage(TaskItem current, TaskItem candidate)
    {
        if (current.Status != TaskState.Backlog || candidate.Status == TaskState.Backlog || !TryReadRequest(current, out _))
        {
            return false;
        }

        TriageState triage = TryReadRequest(candidate, out RequestFields? next) ? next.Triage : TriageState.None;
        return !((candidate.Status == TaskState.ToDo && triage == TriageState.Accepted) || (candidate.Status == TaskState.Rejected && triage == TriageState.Declined));
    }

    /// <summary>Whether closing the Task closes a Request that was neither confirmed by the requesting Team's QA nor declined.</summary>
    /// <param name="task">The Task to close.</param>
    private static bool ClosesUnconfirmedRequest(TaskItem task) =>
        TryReadRequest(task, out RequestFields? fields) && fields.Triage != TriageState.Declined && string.IsNullOrWhiteSpace(fields.ConfirmedBy);

    /// <summary>Whether the Task is a bug: a <c>request: bug</c> field or a <c>bug</c> label.</summary>
    /// <param name="task">The Task.</param>
    internal static bool HasBugMarker(TaskItem task) =>
        task.Tags.Contains("bug", StringComparer.OrdinalIgnoreCase)
        || string.Equals(UnknownValue(task, "request"), "bug", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the Task carries <c>approved: true</c> or a non-empty <c>approved_by</c> (Spec §Gates).</summary>
    /// <param name="task">The parent spec Task.</param>
    private static bool IsApproved(TaskItem task) =>
        string.Equals(UnknownValue(task, "approved"), "true", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(UnknownValue(task, TaskPatch.ApprovedByKey));

    /// <summary>The last value of an unknown frontmatter key, matched case-insensitively.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="key">The key.</param>
    private static string? UnknownValue(TaskItem task, string key) =>
        task.UnknownFields.LastOrDefault(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// The shared body of <see cref="Close"/> and <see cref="Reopen"/>, run under
    /// <see cref="mutateGate"/>. Skips §9.4's diff-then-Unchanged step (Settled corrections-B2 D6
    /// item 7: <see cref="TaskDiff.Compare(TaskItem, TaskItem)"/> is empty for a Closed flip alone,
    /// since it ignores <see cref="TaskLocation.Closed"/>) and appends the entry to the Task's
    /// current file text directly, then moves it - never <see cref="TaskFileFormat.Compose"/> or
    /// <see cref="TaskFileFormat.ReplaceHead"/>, since Closed lives in the folder, not the
    /// frontmatter (Spec ~:125).
    /// </summary>
    /// <param name="id">The Task to close or reopen.</param>
    /// <param name="actor">Who is making the change.</param>
    /// <param name="closing"><see langword="true"/> for <see cref="Close"/>, <see langword="false"/> for <see cref="Reopen"/>.</param>
    private (TaskResult Result, TaskChange? Change) CloseOrReopenCore(TaskId id, TaskActor actor, bool closing)
    {
        TaskItem? current = this.store.Get(id);
        if (current is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        if (current.Location.Closed == closing)
        {
            string problem = closing
                ? string.Create(CultureInfo.InvariantCulture, $"{id} is already closed.")
                : string.Create(CultureInfo.InvariantCulture, $"{id} is already active.");
            return (new TaskResult.Refused([problem]), null);
        }

        if (closing && actor.Kind != TaskActorKind.Human && ClosesUnconfirmedRequest(current))
        {
            return (new TaskResult.Refused([CloseRequestRefusal]), null);
        }

        string? currentText = this.store.ReadText(id);
        if (currentText is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        string summary = closing ? "closed" : "reopened";
        ChangeLogEntry entry = new(at, actor.Name, summary);
        string finalText = TaskFileFormat.AppendEntry(currentText, entry);

        TaskLocation newLocation = current.Location with { Closed = closing };
        TaskItem? written = this.store.Move(current, current.Version, newLocation, finalText);
        if (written is null)
        {
            return (new TaskResult.Conflict(this.store.Get(id) ?? current, []), null);
        }

        TaskChange change = new(current, written, [], actor, entry);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>
    /// Spec §9.3's merge/conflict check for a stale <paramref name="baseVersion"/> (already known to
    /// differ from <paramref name="current"/>'s own version): finds what the base referred to via
    /// <see cref="TaskStore.GetVersion"/>, and returns every field the caller's patch and an
    /// intervening change both touch, restricted (corrections-B5 decision C) to the fields whose
    /// patched value actually differs from <paramref name="current"/>'s - a patch that happens to
    /// resubmit the value already on disk is never a conflict. When the base version has aged out of
    /// <see cref="TaskStore.GetVersion"/>'s history (step 5), every patched field that differs from
    /// <paramref name="current"/> conflicts, since there's no way to tell which of them an
    /// intervening change actually touched.
    /// </summary>
    /// <param name="id">The Task being updated.</param>
    /// <param name="baseVersion">The caller's stale base version.</param>
    /// <param name="current">The Task as it is now.</param>
    /// <param name="resolvedPatch">The patch, with its Assignee/Team/Project already resolved.</param>
    /// <param name="patchVsCurrent">What the resolved patch actually changes relative to <paramref name="current"/>.</param>
    private List<TaskField> FindConflictingFields(TaskId id, string baseVersion, TaskItem current, TaskPatch resolvedPatch, IReadOnlyList<FieldChange> patchVsCurrent)
    {
        TaskItem? baseTask = this.store.GetVersion(id, baseVersion);
        List<TaskField> patchedFieldsThatDiffer = [.. patchVsCurrent.Select(change => change.Field)];

        if (baseTask is null)
        {
            return patchedFieldsThatDiffer;
        }

        IReadOnlyList<FieldChange> othersChanges = TaskDiff.Compare(baseTask, current);
        HashSet<TaskField> touchedByBoth = new(resolvedPatch.Fields());
        touchedByBoth.IntersectWith(othersChanges.Select(change => change.Field));

        return [.. patchedFieldsThatDiffer.Where(touchedByBoth.Contains)];
    }

    /// <summary>Resolves a patch's Assignee alias to its Name and canonicalises its Team/Project casing (Settled corrections-B2 D6 item 4), leaving every other field untouched.</summary>
    private TaskPatch ResolvePatch(TaskPatch patch, string currentTeam)
    {
        string? resolvedTeam = patch.Team is not null ? this.CanonicalizeTeam(patch.Team) : null;
        string teamForProject = resolvedTeam ?? currentTeam;

        return patch with
        {
            Assignee = patch.Assignee.IsSet ? Optional<string?>.Set(this.ResolveAssignee(patch.Assignee.Value)) : patch.Assignee,
            Team = resolvedTeam ?? patch.Team,
            Project = patch.Project.IsSet ? Optional<string?>.Set(this.CanonicalizeProject(teamForProject, patch.Project.Value)) : patch.Project,
        };
    }

    /// <summary>Resolves an assignee's Alias to its Name, or the Human's own Name to its canonical casing. An unresolvable value passes through unchanged, so <see cref="Validate"/> can report it.</summary>
    private string? ResolveAssignee(string? assignee)
    {
        if (assignee is not { Length: > 0 })
        {
            return assignee;
        }

        if (this.human.Matches(assignee))
        {
            return this.human.Name;
        }

        return this.personas.ResolveByNameOrAlias(assignee)?.Name ?? assignee;
    }

    /// <summary>Returns an existing Team folder's casing for <paramref name="team"/> (Settled corrections-B2 D6 item 4), or <paramref name="team"/> unchanged when no folder exists yet.</summary>
    private string CanonicalizeTeam(string team)
    {
        foreach (TeamFolder folder in this.store.Teams)
        {
            if (string.Equals(folder.Name, team, StringComparison.OrdinalIgnoreCase))
            {
                return folder.Name;
            }
        }

        return team;
    }

    /// <summary>Returns an existing Project sub-folder's casing under <paramref name="canonicalTeam"/> for <paramref name="project"/>, or <paramref name="project"/> unchanged.</summary>
    private string? CanonicalizeProject(string canonicalTeam, string? project)
    {
        if (project is not { Length: > 0 })
        {
            return project;
        }

        foreach (TeamFolder folder in this.store.Teams)
        {
            if (!string.Equals(folder.Name, canonicalTeam, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string existingProject in folder.Projects)
            {
                if (string.Equals(existingProject, project, StringComparison.OrdinalIgnoreCase))
                {
                    return existingProject;
                }
            }
        }

        return project;
    }

    /// <summary>True when a Team label a Persona names, or an existing Task Team folder, matches <paramref name="team"/> case-insensitively.</summary>
    private bool IsKnownTeam(string team) =>
        this.personas.Teams.Any(known => string.Equals(known, team, StringComparison.OrdinalIgnoreCase)) ||
        this.store.Teams.Any(folder => string.Equals(folder.Name, team, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every known Team label, from Persona declarations and existing Task folders, deduplicated case-insensitively and sorted for a problem message.</summary>
    private List<string> KnownTeamNames()
    {
        List<string> names = [.. this.personas.Teams];
        foreach (TeamFolder folder in this.store.Teams)
        {
            if (!names.Contains(folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(folder.Name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>
    /// Spec §9.2's rules, scoped by <paramref name="touchedFields"/>: <see langword="null"/> checks
    /// every rule (Create), a list checks only the rules for those fields (Update - Settled
    /// corrections-B2 D6 item 3: missing references elsewhere in the Task are left alone). The
    /// reason/status coupling rule always runs, since <see cref="TaskPatch.Reason"/> isn't itself a
    /// <see cref="TaskField"/>.
    /// </summary>
    /// <param name="candidate">The Task as it would be after the change.</param>
    /// <param name="touchedFields">The fields being changed, or <see langword="null"/> for every field.</param>
    /// <param name="reason">The reason offered with the change, if any.</param>
    /// <param name="turnsGateOn">Whether the change turns the Human gate from off to on, which is the one other change that may carry a reason.</param>
    private List<string> Validate(TaskItem candidate, IReadOnlyCollection<TaskField>? touchedFields, string? reason, bool turnsGateOn = false)
    {
        List<string> problems = [];

        bool Touches(TaskField field) => touchedFields is null || touchedFields.Contains(field);

        if (Touches(TaskField.Title))
        {
            AddTitleProblems(candidate.Title, problems);
        }

        if (Touches(TaskField.Description))
        {
            AddDescriptionProblems(candidate.Description, problems);
        }

        if (Touches(TaskField.Location))
        {
            this.AddTeamAndProjectProblems(candidate.Location, problems);
        }

        if (Touches(TaskField.Assignee))
        {
            this.AddAssigneeProblems(candidate.Assignee, problems);
        }

        if (Touches(TaskField.Status) || Touches(TaskField.DuplicateOf))
        {
            AddDuplicateCouplingProblems(candidate.Status, candidate.DuplicateOf, problems);
        }

        if (Touches(TaskField.Parent))
        {
            this.AddParentProblems(candidate, problems);
        }

        if (Touches(TaskField.BlockedBy))
        {
            this.AddBlockedByProblems(candidate, problems);
        }

        if (Touches(TaskField.DuplicateOf))
        {
            this.AddDuplicateOfReferenceProblems(candidate, problems);
        }

        if (Touches(TaskField.Tags))
        {
            AddTagProblems(candidate.Tags, problems);
        }

        if (Touches(TaskField.Sprint))
        {
            this.AddSprintProblems(candidate.Sprint, problems);
        }

        AddReasonProblems(reason, candidate.Status, turnsGateOn, problems);

        return problems;
    }

    /// <summary>Spec §9.2: the title is 1-200 characters and one line.</summary>
    private static void AddTitleProblems(string title, List<string> problems)
    {
        string trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            problems.Add("Title is empty.");
            return;
        }

        if (trimmed.Length > 200)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Title is {trimmed.Length} characters; the limit is 200."));
            return;
        }

        if (title.Contains('\n') || title.Contains('\r'))
        {
            problems.Add("Title must be one line.");
        }
    }

    /// <summary>Spec §9.2: the description doesn't contain a reserved Change log heading.</summary>
    private static void AddDescriptionProblems(string description, List<string> problems)
    {
        if (TaskFileFormat.ContainsChangeLogHeading(description))
        {
            problems.Add("The description must not contain a '## Change log' heading; that heading is reserved for the task's history.");
        }
    }

    /// <summary>Spec §9.2: the Team and Project are legal folder names, and the Team is known.</summary>
    private void AddTeamAndProjectProblems(TaskLocation location, List<string> problems)
    {
        if (TryGetIllegalFolderNameProblem("team", location.Team, out string? teamProblem))
        {
            problems.Add(teamProblem);
        }
        else if (!this.IsKnownTeam(location.Team))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Unknown team '{location.Team}'. Known teams: {string.Join(", ", this.KnownTeamNames())}."));
        }

        if (location.Project is not { Length: > 0 } project)
        {
            return;
        }

        if (project.StartsWith('_'))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The Project name '{project}' cannot be a folder name on this computer (it starts with '_')."));
        }
        else if (string.Equals(project, TeamNames.MemoryFolder, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(TeamNames.MemoryReservedProblem);
        }
        else if (string.Equals(project, TeamNames.UploadsFolder, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(TeamNames.UploadsReservedProblem);
        }
        else if (TryGetIllegalFolderNameProblem("Project", project, out string? projectProblem))
        {
            problems.Add(projectProblem);
        }
    }

    /// <summary>Spec §9.2: the assignee is a known Persona Name or Alias, or the Human's Name.</summary>
    private void AddAssigneeProblems(string? assignee, List<string> problems)
    {
        if (assignee is not { Length: > 0 })
        {
            return;
        }

        if (this.human.Matches(assignee))
        {
            return;
        }

        if (this.personas.ResolveByNameOrAlias(assignee) is not null)
        {
            return;
        }

        problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown teammate '{assignee}'."));
    }

    /// <summary>Spec §9.2: DuplicateOf is set exactly when the status is Duplicate.</summary>
    private static void AddDuplicateCouplingProblems(TaskState status, TaskId? duplicateOf, List<string> problems)
    {
        if (status == TaskState.Duplicate && duplicateOf is null)
        {
            problems.Add("Status Duplicate needs duplicate_of.");
        }
        else if (status != TaskState.Duplicate && duplicateOf is not null)
        {
            problems.Add("duplicate_of is only allowed when status is Duplicate.");
        }
    }

    /// <summary>Spec §9.2: a parent isn't the Task itself, must exist, and can't create a cycle.</summary>
    private void AddParentProblems(TaskItem candidate, List<string> problems)
    {
        if (candidate.Parent is not { } parent)
        {
            return;
        }

        if (parent == candidate.Id)
        {
            problems.Add("A task cannot block itself.");
            return;
        }

        if (this.store.Get(parent) is null)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{parent}'."));
            return;
        }

        if (this.CreatesCycle(candidate.Id, parent))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{candidate.Id} is already a descendant of {parent}, so it cannot be its parent."));
        }
    }

    /// <summary>True when walking up from <paramref name="parentCandidate"/> through recorded parents reaches <paramref name="candidateId"/>, bounded against a malformed cycle already on disk.</summary>
    private bool CreatesCycle(TaskId candidateId, TaskId parentCandidate)
    {
        TaskId? current = parentCandidate;
        int guard = 0;
        while (current is { } id && guard < 10_000)
        {
            if (id == candidateId)
            {
                return true;
            }

            current = this.store.Get(id)?.Parent;
            guard++;
        }

        return false;
    }

    /// <summary>Spec §9.2: every blocked-by id isn't the Task itself, and must exist.</summary>
    private void AddBlockedByProblems(TaskItem candidate, List<string> problems)
    {
        foreach (TaskId blocker in candidate.BlockedBy)
        {
            if (blocker == candidate.Id)
            {
                problems.Add("A task cannot block itself.");
                continue;
            }

            if (this.store.Get(blocker) is null)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{blocker}'."));
            }
        }
    }

    /// <summary>Spec §9.2: duplicate_of isn't the Task itself, and must exist.</summary>
    private void AddDuplicateOfReferenceProblems(TaskItem candidate, List<string> problems)
    {
        if (candidate.DuplicateOf is not { } duplicateOf)
        {
            return;
        }

        if (duplicateOf == candidate.Id)
        {
            problems.Add("A task cannot block itself.");
            return;
        }

        if (this.store.Get(duplicateOf) is null)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{duplicateOf}'."));
        }
    }

    /// <summary>Sprints spec §9.4: a Sprint being set is in the catalogue. Clearing (<see langword="null"/>) is always valid.</summary>
    private void AddSprintProblems(SprintId? sprint, List<string> problems)
    {
        if (sprint is not { } id || this.sprints.Get(id) is not null)
        {
            return;
        }

        IReadOnlyList<Sprint> known = this.sprints.All;
        string tail = known.Count == 0
            ? "There are no sprints yet."
            : string.Create(CultureInfo.InvariantCulture, $"Known sprints: {string.Join(", ", known.Select(static entry => $"{entry.Id} {entry.Name}"))}.");
        problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown sprint '{id}'. {tail}"));
    }

    /// <summary>
    /// Sprints spec §9.2: returns <paramref name="changes"/> with the Sprint entry's Old and New replaced
    /// by <c>S1 (Sprint 1)</c> labels (the bare id when the Sprint is not in the file), so the Change log
    /// keeps a removed Sprint's name. <see cref="TaskDiff.Compare"/> stays pure. Must run before
    /// <see cref="TaskDiff.Summarise"/>, and while the vacated Sprint's line is still in the catalogue.
    /// </summary>
    /// <param name="changes">What <see cref="TaskDiff.Compare"/> returned for <paramref name="before"/> and <paramref name="after"/>.</param>
    /// <param name="before">The Task before the change.</param>
    /// <param name="after">The Task after the change.</param>
    private IReadOnlyList<FieldChange> LabelSprintChange(IReadOnlyList<FieldChange> changes, TaskItem before, TaskItem after)
    {
        if (!changes.Any(static change => change.Field == TaskField.Sprint))
        {
            return changes;
        }

        FieldChange labelled = new(TaskField.Sprint, this.SprintLabel(before.Sprint), this.SprintLabel(after.Sprint));
        return [.. changes.Select(change => change.Field == TaskField.Sprint ? labelled : change)];
    }

    /// <summary>The id and name of a Sprint, <c>S1 (Sprint 1)</c>; the bare id when it is not in the file; <see langword="null"/> for none.</summary>
    /// <param name="sprint">The Sprint a Task holds, or <see langword="null"/>.</param>
    private string? SprintLabel(SprintId? sprint)
    {
        if (sprint is not { } id)
        {
            return null;
        }

        return this.sprints.Get(id) is { } found
            ? string.Create(CultureInfo.InvariantCulture, $"{id} ({found.Name})")
            : id.ToString();
    }

    /// <summary>Spec §9.2/§7.2: a tag has no ',' or ';'.</summary>
    private static void AddTagProblems(IReadOnlyList<string> tags, List<string> problems)
    {
        foreach (string tag in tags)
        {
            if (tag.Contains(',', StringComparison.Ordinal) || tag.Contains(';', StringComparison.Ordinal))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Tag '{tag}' must not contain ',' or ';'."));
            }
        }
    }

    /// <summary>Spec §9.2: a reason is at most 200 characters, and only given with Cancelled or Rejected, or when the change turns the Human gate on (Helpers Spec §6.11).</summary>
    private static void AddReasonProblems(string? reason, TaskState status, bool turnsGateOn, List<string> problems)
    {
        if (reason is not { Length: > 0 })
        {
            return;
        }

        if (reason.Length > 200)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Reason is {reason.Length} characters; the limit is 200."));
        }

        if (!turnsGateOn && status is not (TaskState.Cancelled or TaskState.Rejected))
        {
            problems.Add("A reason is only recorded when the status becomes Cancelled or Rejected.");
        }
    }

    /// <summary>Settled corrections-B2 D6 item 5: the fixed Windows-illegal-name set, checked on every OS.</summary>
    private static bool TryGetIllegalFolderNameProblem(string label, string name, [NotNullWhen(true)] out string? problem)
    {
        foreach (char c in name)
        {
            if (c < ' ' || Array.IndexOf(IllegalFolderNameCharacters, c) >= 0)
            {
                problem = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The {label} name '{name}' cannot be a folder name on this computer (it contains '{c}').");
                return true;
            }
        }

        if (name.Length > 0 && name[^1] is '.' or ' ')
        {
            problem = string.Create(
                CultureInfo.InvariantCulture,
                $"The {label} name '{name}' cannot be a folder name on this computer (it ends with '{name[^1]}').");
            return true;
        }

        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = dot < 0 ? name : name[..dot];
        if (ReservedFolderNames.Contains(name) || ReservedFolderNames.Contains(stem))
        {
            problem = string.Create(
                CultureInfo.InvariantCulture,
                $"The {label} name '{name}' cannot be a folder name on this computer (it is reserved).");
            return true;
        }

        problem = null;
        return false;
    }

    /// <summary>Truncates to whole seconds, per Spec §9.4's Change log entry timestamp.</summary>
    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
}
