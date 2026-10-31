using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>What <see cref="CardResolver"/> needs to know about the machine: which adapters answered and which connectors are connected.</summary>
internal interface IHireContext
{
    /// <summary>Gets the adapters that answered their model probe.</summary>
    IReadOnlyList<AdapterAvailability> Answered { get; }

    /// <summary>Gets the configured model ids by adapter id, then tier.</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; }

    /// <summary>Gets the adapter ids to avoid when another adapter answered.</summary>
    IReadOnlyCollection<string> AvoidAdapters { get; }

    /// <summary>Gets the connector status.</summary>
    IConnectorStatus Connectors { get; }
}
