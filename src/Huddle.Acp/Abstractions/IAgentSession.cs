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

    /// <summary>
    /// The modes this session's agent advertised, in wire order. An EMPTY list is a real answer -
    /// "this agent offers no mode choice" - not a failure to load. Unlike <see cref="EffortLevels"/>
    /// it does not depend on the model, except that an agent may clamp one mode, "auto" for
    /// example, on a model that does not support it.
    /// </summary>
    IReadOnlyList<AgentModeOption> ModeOptions { get; }

    /// <summary>
    /// The mode id this session is running in once <see cref="AgentSessionOptions.Mode"/> has been
    /// applied and read back, so it is the EFFECTIVE mode when the agent clamped the request. Null
    /// when the agent advertised no mode option.
    /// </summary>
    string? CurrentModeId { get; }

    /// <summary>
    /// Sends one prompt: its text, then its Prompt blocks in order. This is the one method an
    /// implementer must write, so none can handle the string form and silently drop blocks.
    /// </summary>
    /// <param name="prompt">The text and the blocks that follow it.</param>
    /// <param name="cancellationToken">Cancels the Turn.</param>
    /// <returns>How the Turn ended.</returns>
    Task<PromptResult> PromptAsync(AgentPrompt prompt, CancellationToken cancellationToken);

    /// <summary>Sends a text-only prompt. A default method: it is <see cref="PromptAsync(AgentPrompt, CancellationToken)"/> with no blocks.</summary>
    /// <param name="text">The prompt text.</param>
    /// <param name="cancellationToken">Cancels the Turn.</param>
    /// <returns>How the Turn ended.</returns>
    Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken)
    {
        return this.PromptAsync(new AgentPrompt(text), cancellationToken);
    }

    Task CancelAsync(CancellationToken cancellationToken);
}