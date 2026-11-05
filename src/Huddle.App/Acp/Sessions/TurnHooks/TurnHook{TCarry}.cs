namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// A hook with a typed value from Before to After. It does the one cast of <see cref="TurnAddition.Carry"/>
/// and defaults the points a hook does not need.
/// </summary>
/// <typeparam name="TCarry">The type carried from Before to After.</typeparam>
internal abstract class TurnHook<TCarry> : ITurnHook
    where TCarry : class
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract bool AppliesTo(TurnHookScope scope);

    /// <inheritdoc />
    public virtual Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);
        return Task.FromResult(opening.State);
    }

    /// <inheritdoc />
    public abstract Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken);

    /// <summary>Runs only when the prompt was sent; the default returns the state unchanged.</summary>
    /// <param name="turn">The Turn that ended.</param>
    /// <param name="carry">The value Before carried, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new state.</returns>
    protected virtual Task<string?> AfterTurnAsync(TurnEnded turn, TCarry? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return Task.FromResult(turn.State);
    }

    /// <inheritdoc />
    Task<string?> ITurnHook.AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken) =>
        this.AfterTurnAsync(turn, (TCarry?)addition?.Carry,cancellationToken);
}
