namespace Agency.Huddle.App.Acp;

/// <summary>
/// The Adapter default models learned during one user action, such as one Approve: each Adapter is read once through
/// <see cref="ModelCatalogDefaults.ReadDefaultModelAsync"/>, and the answer, a failure's <see langword="null"/>
/// included, is reused for every later hire in the same action, so a broken Adapter is not probed once per Candidate.
/// Not thread-safe: one action uses it from one flow.
/// </summary>
/// <param name="catalog">The model catalog to read.</param>
/// <param name="logger">Logs a probe that failed.</param>
internal sealed class DefaultModelReads(IModelCatalog catalog, ILogger logger)
{
    /// <summary>The key that stands for the default Adapter, whose id is <see langword="null"/>.</summary>
    private const string DefaultAdapterKey = "";

    /// <summary>What each Adapter answered, keyed by its id.</summary>
    private readonly Dictionary<string, string?> learned = new(StringComparer.Ordinal);

    /// <summary>Reads the Adapter's default model, once per Adapter for the life of this instance.</summary>
    /// <param name="adapterId">The Adapter id, or null for the default.</param>
    /// <param name="ct">Cancels the probe.</param>
    /// <returns>The default model id, or <see langword="null"/> when the Adapter reported none or could not be probed.</returns>
    public async Task<string?> ReadAsync(string? adapterId, CancellationToken ct)
    {
        string key = adapterId ?? DefaultAdapterKey;
        if (this.learned.TryGetValue(key, out string? known))
        {
            return known;
        }

        string? model = await ModelCatalogDefaults.ReadDefaultModelAsync(catalog, adapterId, logger, ct).ConfigureAwait(false);
        this.learned[key] = model;
        return model;
    }
}
