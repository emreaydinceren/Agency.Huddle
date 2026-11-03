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
    /// The model id this session is running once <see cref="AgentSessionOptions.Model"/> has been
    /// applied and read back, so it is the EFFECTIVE model when the agent clamped or ignored the
    /// request. Null when the agent advertised no model option. A default member returning null, so
    /// a session type that cannot report it need not implement it.
    /// </summary>
    string? CurrentModelId => null;

    /// <summary>
    /// The effort id this session is running once <see cref="AgentSessionOptions.Effort"/> has been
    /// applied and read back, so it is the EFFECTIVE effort when the agent clamped the request.
    /// Unfiltered: it may be the agent's "default" sentinel, which means "the model chooses". Null
    /// when the agent advertised no effort option. A default member returning null.
    /// </summary>
    string? CurrentEffortId => null;

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

    /// <summary>
    /// Asks the agent to delete this session's stored history (<c>session/delete</c>). Best effort: it completes
    /// normally when the agent does not support deletion, already forgot the session, or does not answer in time.
    /// It works after <c>DisposeAsync</c> too, because a fresh start closes the session first and deletes after.
    /// A default method that does nothing, for a session type with nothing to delete.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the caller; the send itself is bounded separately.</param>
    /// <returns>A task that completes once the request was answered, failed harmlessly or timed out.</returns>
    Task DeleteAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Binds the scope that answers this session's <see cref="ElicitationRequest"/>s; a second call
    /// replaces the first. A default method that does nothing: a session type that cannot receive
    /// elicitation ignores the scope, exactly as a session with no scope bound answers every request
    /// as cancelled.
    /// </summary>
    /// <param name="scope">The scope to answer with.</param>
    void BindElicitationScope(IElicitationScope scope)
    {
    }
}