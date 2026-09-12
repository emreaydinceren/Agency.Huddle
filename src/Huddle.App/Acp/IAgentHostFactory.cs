using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Creates the <see cref="IAgentHost"/>/<see cref="IAgentSession"/> pair that backs one Agent.
/// This interface exists purely as a test seam: it is what lets the entire pipe path be exercised
/// against a fake agent for zero tokens. The real implementation, against <c>DotAcpAgentHost</c>, is
/// built by a separate task.
/// </summary>
internal interface IAgentHostFactory
{
    Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken);
}