namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Produces the Proof of a Task from the live stores.</summary>
internal interface IProofSource
{
    /// <summary>Evaluates the Task's Proof.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>The Proof.</returns>
    Proof For(TaskItem task);
}
