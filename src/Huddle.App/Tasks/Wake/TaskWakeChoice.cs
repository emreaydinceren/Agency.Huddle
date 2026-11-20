namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>What a wake rule chose for a round.</summary>
internal abstract record TaskWakeChoice
{
    /// <summary>Wake <paramref name="Name"/>.</summary>
    /// <param name="Name">The Persona to wake.</param>
    internal sealed record Wake(string Name) : TaskWakeChoice;

    /// <summary>Wake each of <paramref name="Names"/>, in order: the Agents a batch's comments Mention.</summary>
    /// <param name="Names">The Personas to wake, in first-mention order, none of them the actor or the Human.</param>
    internal sealed record WakeEach(IReadOnlyList<string> Names) : TaskWakeChoice;

    /// <summary>Hold <paramref name="Name"/>'s wake until the Task's wake budget is lifted.</summary>
    /// <param name="Name">The Persona whose wake is held.</param>
    internal sealed record HoldForBudget(string Name) : TaskWakeChoice;

    /// <summary>Wake nobody.</summary>
    /// <param name="Block">The guard that stopped the wake, to be logged; <see langword="null"/> logs nothing.</param>
    internal sealed record Skip(WakeBlock? Block) : TaskWakeChoice;
}
