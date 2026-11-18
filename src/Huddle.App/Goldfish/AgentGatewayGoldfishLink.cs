using Agency.Huddle.App.Pipes;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Goldfish;

/// <summary>Reaches the runner through the <see cref="AgentGateway"/>.</summary>
internal sealed class AgentGatewayGoldfishLink(AgentGateway gateway) : IGoldfishRunnerLink
{
    /// <inheritdoc />
    public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct) =>
        gateway.SendAsync(agentId, new GoldfishEnd(possessionId, exit), ct);
}
