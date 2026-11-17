using Agency.Huddle.App.Agents;
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
/// <param name="isChiefOfStaff">Whether a Persona, by Name, is the built-in Chief of Staff; asked at each Turn.</param>
internal sealed class ReferencedItemsEnricher(ReferenceExpander expander, Func<string, bool> isChiefOfStaff) : ITurnEnricher
{
    /// <summary>The enricher's name, as the pipeline's production order knows it.</summary>
    internal const string EnricherName = "ReferencedItems";

    /// <summary>Builds the enricher over the live Persona list, where the Chief of Staff is the Persona carrying its marker.</summary>
    /// <param name="expander">Builds the header.</param>
    /// <param name="personas">The Personas, read at each Turn.</param>
    /// <returns>The enricher.</returns>
    internal static ReferencedItemsEnricher ForPersonas(ReferenceExpander expander, PersonaStore personas)
    {
        ArgumentNullException.ThrowIfNull(expander);
        ArgumentNullException.ThrowIfNull(personas);

        return new ReferencedItemsEnricher(
            expander,
            name => personas.Entries.Any(entry =>
                string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)
                && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public string Name => EnricherName;

    /// <inheritdoc />
    public async Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        string triggerText = turn.Item.Folded is { Count: > 1 } folded
            ? string.Join('\n', folded.Select(message => message.Text))
            : turn.Item.Text;
        string? header = await expander
            .ExpandAsync(triggerText, turn.Host?.Profile.UsesToolNamePrefix ?? true, isChiefOfStaff(turn.PersonaName), cancellationToken)
            .ConfigureAwait(false);
        return turn.Item with { ReferencedItems = header };
    }
}
