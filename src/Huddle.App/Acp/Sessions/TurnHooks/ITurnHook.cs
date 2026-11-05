namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>A check that runs around a Turn. One class per feature; listed in <see cref="TurnHookPipeline"/>.</summary>
internal interface ITurnHook
{
    /// <summary>Gets the name in the log and the key of this hook's state.</summary>
    string Name { get; }

    /// <summary>Whether this hook runs for this Persona and Adapter. The one place its on and off rules live.</summary>
    /// <param name="scope">Who the pipeline serves.</param>
    /// <returns>True when the hook runs.</returns>
    bool AppliesTo(TurnHookScope scope);

    /// <summary>Sets this hook's state for a session that is opening, resuming or was just compacted.</summary>
    /// <param name="opening">The opening.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new state.</returns>
    Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken);

    /// <summary>Runs before the prompt is built.</summary>
    /// <param name="turn">The Turn about to start.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What to add and carry, or null when this hook has nothing to do after the Turn either.</returns>
    Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken);

    /// <summary>Runs only when the prompt was sent, so a Turn that never sent tells the Agent again.</summary>
    /// <param name="turn">The Turn that ended.</param>
    /// <param name="addition">What Before returned.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new state.</returns>
    Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken);
}
