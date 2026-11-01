using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>
/// Lists the gated Tasks waiting on the Human, oldest move into Needs You first (Human gate Spec §6.7). It holds no
/// list of its own: every read goes to <see cref="TaskStore"/>, so a restart loses nothing. It only reports, through
/// <see cref="Changed"/>, that a Task event may have changed the answer.
/// </summary>
internal sealed class NeedsYouTaskSource : IDisposable
{
    private readonly TaskStore store;
    private readonly IProofSource proofs;
    private readonly TaskEvents events;
    private readonly ILogger<NeedsYouTaskSource> logger;

    /// <summary>Creates the source and subscribes it to the Task events.</summary>
    /// <param name="store">Where the Tasks are read from.</param>
    /// <param name="proofs">Evaluates each listed Task's Proof.</param>
    /// <param name="events">The hub whose Task events raise <see cref="Changed"/>.</param>
    /// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
    public NeedsYouTaskSource(TaskStore store, IProofSource proofs, TaskEvents events, ILogger<NeedsYouTaskSource>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(proofs);
        ArgumentNullException.ThrowIfNull(events);

        this.store = store;
        this.proofs = proofs;
        this.events = events;
        this.logger = logger ?? NullLogger<NeedsYouTaskSource>.Instance;
        events.TaskChanged += this.OnTaskChanged;
        events.TasksReloaded += this.OnTasksReloaded;
    }

    /// <summary>A Task event happened that could change <see cref="Entries"/>; raised once per event.</summary>
    public event Action? Changed;

    /// <summary>The open gated Tasks in Needs You whose acceptance is not valid, oldest first.</summary>
    public IReadOnlyList<NeedsYouTaskEntry> Entries => [.. this.store.All
        .Where(NeedsYouTaskSource.Waits)
        .Select(this.ToEntry)
        .OrderBy(entry => entry.Since)
        .ThenBy(entry => entry.TaskId.ToString(), StringComparer.Ordinal)];

    /// <summary>Unsubscribes from the Task events.</summary>
    public void Dispose()
    {
        this.events.TaskChanged -= this.OnTaskChanged;
        this.events.TasksReloaded -= this.OnTasksReloaded;
    }

    /// <summary>True when the Task is gated, in Needs You and not validly accepted.</summary>
    /// <param name="task">The Task.</param>
    private static bool Waits(TaskItem task) =>
        task.IsGated
        && task.Status == TaskState.NeedsYou
        && Acceptance.Of(task).State != AcceptanceState.Valid;

    /// <summary>Builds the entry for a waiting Task.</summary>
    /// <param name="task">The Task.</param>
    private NeedsYouTaskEntry ToEntry(TaskItem task) =>
        new(task.Id, task.Title, Acceptance.LastEnteredNeedsYou(task) ?? task.Updated ?? DateTimeOffset.MinValue, this.proofs.For(task));

    /// <summary>Raises <see cref="Changed"/> for one Task change.</summary>
    /// <param name="change">The change; not inspected.</param>
    private void OnTaskChanged(TaskChange change)
    {
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>Raises <see cref="Changed"/> for one reload.</summary>
    private void OnTasksReloaded()
    {
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }
}
