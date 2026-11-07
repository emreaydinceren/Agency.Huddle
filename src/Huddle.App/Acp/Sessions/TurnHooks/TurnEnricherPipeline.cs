namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Runs the Turn enrichers in the order of the list it is given. Only a <see cref="WorkItemKind.Message"/> is enriched:
/// a Command is the bare command and a Greeting has no Message. One enricher failing is logged and skipped, because the
/// consumer loop, which nothing else drains, must not die for a block that is only an extra.
/// </summary>
internal sealed class TurnEnricherPipeline
{
    /// <summary>
    /// The one place that knows the production order of the enrichers, by name: Library documents first, because the
    /// prompt prints that block first, then the Referenced items header. A new enricher is one class and one entry here.
    /// </summary>
    private static readonly List<string> ProductionOrder =
    [
        LibraryDocumentsEnricher.EnricherName,
        ReferencedItemsEnricher.EnricherName,
    ];

    private readonly IReadOnlyList<ITurnEnricher> enrichers;
    private readonly ILogger logger;

    /// <summary>Initializes a new instance of the <see cref="TurnEnricherPipeline"/> class.</summary>
    /// <param name="enrichers">The enrichers, in the order they run.</param>
    /// <param name="logger">Receives the warning for an enricher that failed.</param>
    internal TurnEnricherPipeline(IReadOnlyList<ITurnEnricher> enrichers, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(enrichers);
        ArgumentNullException.ThrowIfNull(logger);
        this.enrichers = enrichers;
        this.logger = logger;
    }

    /// <summary>Gets the names of the enrichers, in the order they run.</summary>
    internal IReadOnlyList<string> EnricherNames => [.. this.enrichers.Select(enricher => enricher.Name)];

    /// <summary>Builds the production pipeline for one Persona from the registered enrichers, sorted into the production order.</summary>
    /// <param name="enrichers">The registered enrichers, in any order.</param>
    /// <param name="logger">Receives the warning for an enricher that failed.</param>
    /// <returns>The pipeline; it holds no enricher when none were registered.</returns>
    /// <exception cref="InvalidOperationException">An enricher's name is not in the production order.</exception>
    internal static TurnEnricherPipeline ForPersona(IEnumerable<ITurnEnricher> enrichers, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(enrichers);
        List<ITurnEnricher> sorted = [];
        foreach (ITurnEnricher enricher in enrichers)
        {
            if (!ProductionOrder.Contains(enricher.Name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Turn enricher '{enricher.Name}' is not in the production order; add its name to TurnEnricherPipeline.ProductionOrder.");
            }

            sorted.Add(enricher);
        }

        return new TurnEnricherPipeline([.. sorted.OrderBy(enricher => ProductionOrder.IndexOf(enricher.Name))], logger);
    }

    /// <summary>Runs every enricher on a Message item, in list order, each on the previous one's result.</summary>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="personaName">The Persona the Turn is for.</param>
    /// <param name="host">The Persona's running Adapter host, or <see langword="null"/> when there is none.</param>
    /// <param name="cancellationToken">The run's own cancellation token.</param>
    /// <returns>The item with each successful enricher's block set; any other kind of item comes back as it was.</returns>
    public async Task<WorkItem> EnrichAsync(WorkItem item, string personaName, IPersonaHost? host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != WorkItemKind.Message)
        {
            return item;
        }

        foreach (ITurnEnricher enricher in this.enrichers)
        {
            try
            {
                item = await enricher.EnrichAsync(new TurnEnriching(item, personaName, host), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    ex,
                    "Turn enricher {EnricherName} failed for Persona '{PersonaName}' in room {RoomId}; the item is unchanged.",
                    enricher.Name,
                    personaName,
                    item.RoomId);
            }
        }

        return item;
    }
}
