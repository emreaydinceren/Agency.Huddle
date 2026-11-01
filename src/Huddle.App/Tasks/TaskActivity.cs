using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// In-memory, per-Task record of Agent-made wake attempts (Spec §10.7) and the wake budget that
/// guards against a looping Agent (Spec §10.6). A leaf singleton with no dependency but the
/// configured base budget; its state does not survive a restart (known limit).
/// </summary>
internal sealed class TaskActivity
{
    private readonly Dictionary<TaskId, WakeBudget> budgets = [];
    private readonly Dictionary<TaskId, WakeRecord> lastWakes = [];
    private readonly Lock gate = new();
    private readonly int baseBudget;
    private readonly ILogger<TaskActivity> logger;

    /// <summary>Creates a <see cref="TaskActivity"/> using <c>Team:Tasks:AgentWakeBudget</c> as the base budget.</summary>
    /// <param name="options">The Team options, for <see cref="TasksOptions.AgentWakeBudget"/>.</param>
    /// <param name="logger">Reports a <see cref="Woken"/> or <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
    public TaskActivity(IOptions<TeamOptions> options, ILogger<TaskActivity>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.logger = logger ?? NullLogger<TaskActivity>.Instance;
        this.baseBudget = options.Value.Tasks.AgentWakeBudget;
    }

    /// <summary>Raised after a wake is recorded, carrying that wake (drives the toast).</summary>
    public event Action<WakeRecord>? Woken;

    /// <summary>Raised after a Task's budget changes (drives badges).</summary>
    public event Action? Changed;

    /// <summary>The most recent recorded wake for <paramref name="id"/>, or <see langword="null"/> if none.</summary>
    /// <param name="id">The Task to look up.</param>
    public WakeRecord? LastWake(TaskId id)
    {
        lock (this.gate)
        {
            return this.lastWakes.GetValueOrDefault(id);
        }
    }

    /// <summary>The current wake budget for <paramref name="id"/>, starting at zero Used and the base Granted.</summary>
    /// <param name="id">The Task to look up.</param>
    public WakeBudget Budget(TaskId id)
    {
        lock (this.gate)
        {
            return this.GetOrCreate(id);
        }
    }

    /// <summary>Records a wake attempt, setting <see cref="LastWake"/> and raising <see cref="Woken"/>.</summary>
    /// <param name="record">The wake attempt to record.</param>
    public void Record(WakeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (this.gate)
        {
            this.lastWakes[record.TaskId] = record;
        }

        EventRaise.Isolated(this.Woken, record, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Woken)));
    }

    /// <summary>
    /// Atomically checks and, if the budget is not exhausted, counts one Agent-made wake against
    /// <paramref name="id"/>'s budget in a single locked step.
    /// </summary>
    /// <param name="id">The Task the wake is for.</param>
    /// <returns><see langword="true"/> if the wake was counted; <see langword="false"/> if the budget was already exhausted.</returns>
    public bool TryConsumeAgentWake(TaskId id)
    {
        bool consumed;
        lock (this.gate)
        {
            WakeBudget current = this.GetOrCreate(id);
            consumed = !current.Exhausted;
            if (consumed)
            {
                this.budgets[id] = current with { Used = current.Used + 1 };
            }
        }

        if (consumed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }

        return consumed;
    }

    /// <summary>
    /// Decrements <paramref name="id"/>'s Used count by one, never below zero. D9's fire step calls
    /// this to give back a wake counted by <see cref="TryConsumeAgentWake"/> when the outcome turns
    /// out not to be <see cref="WakeOutcome.Woken"/> or <see cref="WakeOutcome.Offline"/>
    /// (corrections-B3 D9 item 9: only those two outcomes are actually counted).
    /// </summary>
    /// <param name="id">The Task whose wake should be refunded.</param>
    public void RefundAgentWake(TaskId id)
    {
        bool changed = false;
        lock (this.gate)
        {
            WakeBudget current = this.GetOrCreate(id);
            if (current.Used > 0)
            {
                this.budgets[id] = current with { Used = current.Used - 1 };
                changed = true;
            }
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>Resets <paramref name="id"/>'s budget to zero Used and the base Granted: a Human changed the Task.</summary>
    /// <param name="id">The Task the Human changed.</param>
    public void ResetForHuman(TaskId id)
    {
        lock (this.gate)
        {
            this.budgets[id] = new WakeBudget(0, this.baseBudget);
        }

        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>Adds another base budget to <paramref name="id"/>'s Granted count, if it is exhausted.</summary>
    /// <param name="id">The Task to grant more budget to.</param>
    public void Grant(TaskId id)
    {
        bool changed = false;
        lock (this.gate)
        {
            WakeBudget current = this.GetOrCreate(id);
            if (current.Exhausted)
            {
                this.budgets[id] = current with { Granted = current.Granted + this.baseBudget };
                changed = true;
            }
        }

        if (changed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>Returns the tracked budget for <paramref name="id"/>, or the fresh default if untracked. Caller holds <see cref="gate"/>.</summary>
    private WakeBudget GetOrCreate(TaskId id) =>
        this.budgets.TryGetValue(id, out WakeBudget? existing) ? existing : new WakeBudget(0, this.baseBudget);
}
