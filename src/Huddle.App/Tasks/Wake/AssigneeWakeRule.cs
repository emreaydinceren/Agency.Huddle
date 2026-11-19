namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>Turns the assignee's <see cref="WakePreview"/> (the Spec §10.2 guards, already applied) into a choice.</summary>
internal sealed class AssigneeWakeRule : ITaskWakeRule
{
    /// <inheritdoc />
    public string Name => "Assignee";

    /// <inheritdoc />
    public TaskWakeChoice Choose(TaskWakeRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        if (!round.WakesAssignee)
        {
            return new TaskWakeChoice.Skip(null);
        }

        WakePreview preview = round.AssigneePreview;
        if (preview.Block == WakeBlock.BudgetPaused && preview.AssigneeName is { } pausedAssignee)
        {
            return new TaskWakeChoice.HoldForBudget(pausedAssignee);
        }

        if (preview.Block != WakeBlock.None || preview.AssigneeName is not { } assigneeName)
        {
            return new TaskWakeChoice.Skip(preview.Block);
        }

        return new TaskWakeChoice.Wake(assigneeName);
    }
}
