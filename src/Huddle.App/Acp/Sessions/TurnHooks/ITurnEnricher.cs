namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Adds one block to a Message Turn's <see cref="WorkItem"/> after the Transcript Catch-up and before the Turn is published.
/// Unlike an <see cref="ITurnHook"/> it runs after the catch-up, because it scans it, and it never shows the early Draft.
/// </summary>
internal interface ITurnEnricher
{
    /// <summary>Gets the enricher's name, as the pipeline's production order knows it.</summary>
    string Name { get; }

    /// <summary>Returns the item with this enricher's block set.</summary>
    /// <param name="turn">The item so far, the Persona and its host.</param>
    /// <param name="cancellationToken">The run's own cancellation token.</param>
    /// <returns>The item, with this enricher's block set.</returns>
    Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken);
}
