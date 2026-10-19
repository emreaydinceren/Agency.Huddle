namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Builds a Task's <see cref="ProofContext"/> and evaluates it.</summary>
/// <param name="builder">Gathers the context.</param>
internal sealed class ProofSource(ProofContextBuilder builder) : IProofSource
{
    /// <inheritdoc/>
    public Proof For(TaskItem task) => ProofEvaluator.Evaluate(task, builder.Build(task));
}
