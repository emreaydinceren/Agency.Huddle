namespace Agency.Huddle.Acp.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Manages the lifecycle of a connection to an agent process and its sessions.</summary>
public interface IAgentHost : IAsyncDisposable
{
    AgentHostInfo Info { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task<IAgentSession> StartSessionAsync(AgentSessionOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes a previously closed session by id (RS §6.4 A-1), built like <see cref="StartSessionAsync"/>:
    /// registers the sink, then applies <see cref="AgentSessionOptions.Model"/> and
    /// <see cref="AgentSessionOptions.Effort"/>.
    /// </summary>
    /// <param name="sessionId">The id a previous <see cref="StartSessionAsync"/> or resume minted.</param>
    /// <param name="options">The same shape <see cref="StartSessionAsync"/> takes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AgentSessionNotFoundException">The agent no longer has a session for <paramref name="sessionId"/>.</exception>
    Task<IAgentSession> ResumeSessionAsync(string sessionId, AgentSessionOptions options, CancellationToken cancellationToken);
}