namespace Agency.Huddle.App.Tasks;

/// <summary>What a <see cref="TaskChange"/> is, for the changes whose <c>Changes</c> list is empty (Smart wakes spec 3.2 and 5.1).</summary>
internal enum TaskChangeKind
{
    /// <summary>A change with fields: an in-app update, a decision, an outside edit or a gate's refusal. The default.</summary>
    Edit,

    /// <summary>A new Task, made in Huddle or outside it.</summary>
    Create,

    /// <summary>A comment added to a Task.</summary>
    Comment,

    /// <summary>Evidence added to a Task.</summary>
    Evidence,

    /// <summary>The Task moved into <c>_closed/</c> through <c>close_task</c> or the Board; the status is unchanged.</summary>
    Close,

    /// <summary>The Task moved out of <c>_closed/</c> through <c>reopen_task</c> or the Board; the status is unchanged.</summary>
    Reopen,

    /// <summary>Huddle released a Task whose Proof is complete, so the merge may go ahead without an Accept.</summary>
    Release,

    /// <summary>A step Huddle took under a Needs You level, such as an approved Sprint: a log line only.</summary>
    HuddleStep,
}
