using Agency.Huddle.App.References;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Spec section 9.1: the Referenced items header describes only the references in the triggering Message's own text,
/// never the catch-up or Transcript, looked up now.
/// </summary>
/// <remarks>
/// Expanded before the locked check-and-publish, like the Library collect, so a slow lookup is not counted as Adapter
/// silence.
/// </remarks>
/// <param name="expander">Builds the header.</param>
internal sealed class ReferencedItemsEnricher(ReferenceExpander expander) : ITurnEnricher
{
    /// <summary>The enricher's name, as the pipeline's production order knows it.</summary>
    internal const string EnricherName = "ReferencedItems";

    /// <inheritdoc />
    public string Name => EnricherName;

    /// <inheritdoc />
    public async Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        string? header = await expander
            .ExpandAsync(turn.Item.Text, turn.Host?.Profile.UsesToolNamePrefix ?? true, cancellationToken)
            .ConfigureAwait(false);
        return turn.Item with { ReferencedItems = header };
    }
}
