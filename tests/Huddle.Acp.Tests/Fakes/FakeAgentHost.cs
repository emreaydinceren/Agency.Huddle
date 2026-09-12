namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>A scripted <see cref="IAgentHost"/> whose info and session-start behaviour the test controls.</summary>
internal sealed class FakeAgentHost : IAgentHost
{
    private readonly Lock gate = new Lock();

    private readonly List<AgentSessionOptions> sessionRequests = new List<AgentSessionOptions>();

    internal FakeAgentHost()
    {
        this.Info = new AgentHostInfo("fake-agent", "0.0.1", 1, Array.Empty<AuthMethodInfo>(), false);
    }

    /// <summary>Called by <see cref="StartSessionAsync"/>. Default: returns a new <see cref="FakeAgentSession"/>.</summary>
    internal Func<AgentSessionOptions, Task<IAgentSession>>? OnStartSession { get; set; }

    internal IReadOnlyList<AgentSessionOptions> SessionRequests
    {
        get
        {
            lock (this.gate)
            {
                return this.sessionRequests.ToArray();
            }
        }
    }

    internal bool Disposed { get; private set; }

    public AgentHostInfo Info { get; set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<IAgentSession> StartSessionAsync(AgentSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (this.gate)
        {
            this.sessionRequests.Add(options);
        }

        Func<AgentSessionOptions, Task<IAgentSession>>? handler = this.OnStartSession;
        if (handler is not null)
        {
            return await handler(options).ConfigureAwait(false);
        }

        return new FakeAgentSession();
    }

    public ValueTask DisposeAsync()
    {
        this.Disposed = true;
        return ValueTask.CompletedTask;
    }
}
