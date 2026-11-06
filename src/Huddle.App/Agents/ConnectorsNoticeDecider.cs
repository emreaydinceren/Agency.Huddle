using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Agents;

/// <summary>
/// Decides, at the first start of the build that turned the account connectors off, whether the Agents page shows the one-time
/// notice (docs/Huddle.AgentConnectors-Specifications.md section 13). Registered before <see cref="BuiltinAgentSeeder"/> so it
/// sees only the Agents the Human already had: a fresh install has none yet, and has nothing to be told it lost.
/// </summary>
/// <param name="personas">The Persona library, read for whether any Agent exists.</param>
/// <param name="notice">The stored flag the decision is written to.</param>
internal sealed class ConnectorsNoticeDecider(PersonaStore personas, ConnectorsNoticeStore notice) : IHostedService
{
    /// <summary>Writes the decision once; a later start changes nothing.</summary>
    /// <param name="cancellationToken">Unused; the work is synchronous local I/O.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        notice.DecideOnce(personas.Entries.Count > 0);
        return Task.CompletedTask;
    }

    /// <summary>Nothing to stop.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
