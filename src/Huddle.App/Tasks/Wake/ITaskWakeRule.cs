namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// One reason to wake someone about a Task change. A rule only chooses: it is synchronous, does no I/O and never delivers;
/// <see cref="TaskTriggerService"/> acts on the <see cref="TaskWakeChoice"/> (delivery, budget, holds, Room choice, Feed).
/// </summary>
internal interface ITaskWakeRule
{
    /// <summary>The rule's name, used when a failing rule is logged.</summary>
    string Name { get; }

    /// <summary>Chooses whom this rule wakes for <paramref name="round"/>, if anyone.</summary>
    /// <param name="round">The batch's Task, actor and flags.</param>
    /// <returns>The choice.</returns>
    TaskWakeChoice Choose(TaskWakeRound round);
}
