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
}