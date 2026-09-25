namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The outcome of a <see cref="TaskService"/> mutation (Spec §9.1). Expected failures are values,
/// never exceptions - the house principle: only an <see cref="IOException"/> from the store
/// propagates. Internal (Settled corrections-B2 D6 item 12): nothing outside <c>Huddle.App</c>
/// needs to close over this hierarchy.
/// </summary>
internal abstract record TaskResult
{
    /// <summary>The change was validated, written and logged.</summary>
    /// <param name="Task">The Task as written.</param>
    /// <param name="Change">What changed, and the Change log entry recorded for it.</param>
    internal sealed record Saved(TaskItem Task, TaskChange Change) : TaskResult;

    /// <summary>The patch produced no diff against the current Task: no write, no event.</summary>
    /// <param name="Task">The Task, unchanged.</param>
    internal sealed record Unchanged(TaskItem Task) : TaskResult;

    /// <summary>The change failed validation (Spec §9.2), and nothing was written.</summary>
    /// <param name="Problems">Every validation problem found, reported together.</param>
    internal sealed record Refused(IReadOnlyList<string> Problems) : TaskResult;

    /// <summary>A stale <c>baseVersion</c> overlapped a field this patch also changes (Spec §9.3).</summary>
    /// <param name="Current">The Task as it is now, newer than the caller's base.</param>
    /// <param name="Fields">The fields both the caller's patch and the intervening change touched.</param>
    internal sealed record Conflict(TaskItem Current, IReadOnlyList<TaskField> Fields) : TaskResult;

    /// <summary>No Task with the given id exists.</summary>
    /// <param name="Id">The id that wasn't found.</param>
    internal sealed record NotFound(TaskId Id) : TaskResult;
}
