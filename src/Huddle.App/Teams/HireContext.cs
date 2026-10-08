using Agency.Huddle.App.Acp;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The production <see cref="IHireContext"/>: every configured adapter counts as available, and the model
/// ids configured under <c>Team:Teams:Tiers</c> are the models it lists. Nothing is probed here (the probe
/// is asynchronous and a hire is not), so a tier without a configured model uses the adapter's default.
/// </summary>
/// <param name="adapters">The configured adapters.</param>
/// <param name="options">The options carrying <c>Team:Teams:Tiers</c>.</param>
/// <param name="connectors">The connector status.</param>
internal sealed class HireContext(AdapterCatalog adapters, IOptions<TeamOptions> options, IConnectorStatus connectors) : IHireContext
{
    /// <inheritdoc />
    public IReadOnlyList<AdapterAvailability> Answered => [.. adapters.Profiles.Select(this.Available)];

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap =>
        options.Value.Teams.Tiers.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyDictionary<ModelTier, string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<string> AvoidAdapters => [];

    /// <inheritdoc />
    public IConnectorStatus Connectors => connectors;

    /// <summary>Builds the availability of one adapter from the models configured for it.</summary>
    private AdapterAvailability Available(AdapterProfile profile)
    {
        IReadOnlyList<string> models = options.Value.Teams.Tiers
            .Where(pair => string.Equals(pair.Key, profile.Id, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static pair => pair.Value.Values)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new AdapterAvailability(profile.Id, models);
    }
}
