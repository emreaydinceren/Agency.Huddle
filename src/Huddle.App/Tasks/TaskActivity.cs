using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Tasks;

/// <summary>What a wake attempt did, for one Task (Spec §10.6–§10.7).</summary>
public enum WakeOutcome
{
    /// <summary>The assignee's session was posted to and will see the change.</summary>
    Woken,

    /// <summary>The assignee is not online, so no one was told.</summary>
    Offline,

    /// <summary>The Task's wake budget was already spent before this wake.</summary>
    BudgetSpent,

    /// <summary>Waking is paused for this Task; the change was saved and logged only.</summary>
    WakePaused,

    /// <summary>The wake could not be delivered (for example, no Room could be chosen).</summary>
    Failed,
}

/// <summary>One recorded wake attempt for a Task, driving the "AI reacting" toast.</summary>
/// <param name="TaskId">The Task the wake was for.</param>
/// <param name="Assignee">The name of the Agent the wake targeted.</param>
/// <param name="RoomId">The Room the wake was posted in, or <see langword="null"/> when none was chosen.</param>
/// <param name="RoomName">The name of <paramref name="RoomId"/>'s Room, or <see langword="null"/> when none was chosen.</param>
/// <param name="Outcome">What the wake attempt did.</param>
/// <param name="At">When the wake attempt happened.</param>
public sealed record WakeRecord(TaskId TaskId, string Assignee, string? RoomId, string? RoomName, WakeOutcome Outcome, DateTimeOffset At);

/// <summary>The Agent-wake budget for one Task (Spec §10.6): a guard against Agents looping.</summary>
/// <param name="Used">How many Agent-made wakes have been counted since the last Human change to the Task.</param>
/// <param name="Granted">The total wakes allowed before the budget is exhausted.</param>
public sealed record WakeBudget(int Used, int Granted)
{
    /// <summary>Whether the budget has no wakes left. A non-positive <see cref="Granted"/> never exhausts.</summary>
    public bool Exhausted => this.Granted > 0 && this.Used >= this.Granted;
}

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

    /// <summary>Creates a <see cref="TaskActivity"/> using <c>Team:Tasks:AgentWakeBudget</c> as the base budget.</summary>
    /// <param name="options">The Team options, for <see cref="TasksOptions.AgentWakeBudget"/>.</param>
    public TaskActivity(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
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

        this.Woken?.Invoke(record);
    }

    /// <summary>Counts one Agent-made wake against <paramref name="id"/>'s budget.</summary>
    /// <param name="id">The Task the wake was for.</param>
    public void CountAgentWake(TaskId id)
    {
        lock (this.gate)
        {
            WakeBudget current = this.GetOrCreate(id);
            this.budgets[id] = current with { Used = current.Used + 1 };
        }

        this.Changed?.Invoke();
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
            this.Changed?.Invoke();
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
            this.Changed?.Invoke();
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

        this.Changed?.Invoke();
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
            this.Changed?.Invoke();
        }
    }

    /// <summary>Returns the tracked budget for <paramref name="id"/>, or the fresh default if untracked. Caller holds <see cref="gate"/>.</summary>
    private WakeBudget GetOrCreate(TaskId id) =>
        this.budgets.TryGetValue(id, out WakeBudget? existing) ? existing : new WakeBudget(0, this.baseBudget);
}
