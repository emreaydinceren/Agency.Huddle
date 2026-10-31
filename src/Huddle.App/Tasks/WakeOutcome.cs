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
