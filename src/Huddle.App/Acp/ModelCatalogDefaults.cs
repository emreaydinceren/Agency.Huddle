namespace Agency.Huddle.App.Acp;

/// <summary>
/// The one rule for learning the model an Adapter runs on when none is chosen, shared by every place that stores
/// that model on a new Agent (Build a team, an approved Proposal) so they cannot drift apart.
/// </summary>
internal static class ModelCatalogDefaults
{
    /// <summary>
    /// Reads the id of the model the Adapter runs on when none is chosen. It answers from the cache when the default
    /// is already known; otherwise it reads the catalog, which probes the Adapter once and then answers from its
    /// cache, and peeks the default the probe reported. It must only run on a user action, never on a page load.
    /// </summary>
    /// <param name="catalog">The model catalog to read.</param>
    /// <param name="adapterId">The Adapter id, or null for the default.</param>
    /// <param name="logger">Logs a probe that failed.</param>
    /// <param name="ct">Cancels the probe.</param>
    /// <returns>The default model id, or <see langword="null"/> when the Adapter reported none or could not be probed.</returns>
    public static async Task<string?> ReadDefaultModelAsync(IModelCatalog catalog, string? adapterId, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(logger);

        string? cached = catalog.PeekDefaultModel(adapterId);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            _ = await catalog.GetAsync(adapterId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Safe to swallow: the caller simply stores no Model and the Agent runs on the Adapter's default,
            // which is what it did before; the catalog caches nothing on failure, so the next hire tries again.
            logger.LogWarning(ex, "Reading the Adapter's default model failed.");
            return null;
        }

        return catalog.PeekDefaultModel(adapterId);
    }
}
