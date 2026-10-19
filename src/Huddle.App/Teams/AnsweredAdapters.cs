using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Teams;

/// <summary>Builds the list of adapters that answered their model probe, for <see cref="TierModelPicker"/>.</summary>
/// <param name="adapters">The configured adapters.</param>
/// <param name="models">The model catalog probed per adapter.</param>
internal sealed class AnsweredAdapters(AdapterCatalog adapters, IModelCatalog models)
{
    /// <summary>Probes each configured adapter and keeps those that listed at least one model.</summary>
    /// <param name="cancellationToken">Cancels the probes.</param>
    /// <returns>The adapters that answered, in configuration order.</returns>
    public async ValueTask<IReadOnlyList<AdapterAvailability>> GetAsync(CancellationToken cancellationToken)
    {
        List<AdapterAvailability> answered = [];
        foreach (AdapterProfile profile in adapters.Profiles)
        {
            IReadOnlyList<AgentModelOption> options = await models.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            if (options.Count > 0)
            {
                answered.Add(new AdapterAvailability(profile.Id, [.. options.Select(static option => option.Id)]));
            }
        }

        return answered;
    }
}
