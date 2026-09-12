namespace Agency.Huddle.Acp.Abstractions;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>Represents a single, ongoing conversation with an agent.</summary>
public interface IAgentSession : IAsyncDisposable
{
    string SessionId { get; }

    ChannelReader<AgentEvent> Events { get; }

    /// <summary>
    /// The models this session's agent advertised through <c>session/new</c>, in wire order.
    /// An EMPTY list means UNKNOWN, never "no models are available" - several agents take their
    /// model from configuration or a CLI flag and advertise nothing through this mechanism at all.
    /// Callers must not treat an empty list as proof the agent has exactly one, fixed model.
    /// </summary>
    IReadOnlyList<AgentModelOption> Models { get; }

    /// <summary>
    /// The effort levels this session's agent advertised for the CURRENT model, in wire order.
    /// Unlike <see cref="Models"/>, an EMPTY list here is a REAL answer - "this model offers no
    /// effort choice" - not "unknown": the agent emits this option only while the current model
    /// reports effort support, and rebuilds the list on every model switch. Callers must not treat
    /// an empty list as a sign the catalog failed to load.
    /// </summary>
    IReadOnlyList<AgentEffortOption> EffortLevels { get; }

    Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);
}