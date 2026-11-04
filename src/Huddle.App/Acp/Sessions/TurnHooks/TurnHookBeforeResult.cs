namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>What the Before points produced for one Turn.</summary>
/// <param name="Blocks">The non-null texts, in hook order.</param>
/// <param name="Additions">Each hook's addition, keyed by <see cref="ITurnHook.Name"/>.</param>
internal sealed record TurnHookBeforeResult(IReadOnlyList<string> Blocks, IReadOnlyDictionary<string, TurnAddition> Additions)
{
    /// <summary>Gets a result with no blocks and no additions.</summary>
    public static TurnHookBeforeResult Empty { get; } =
        new([], new Dictionary<string, TurnAddition>(StringComparer.Ordinal));
}
