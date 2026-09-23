namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>Base type for every event published by an agent session.</summary>
public abstract record AgentEvent(string SessionId);

/// <summary>A chunk of the agent's visible reply text.</summary>
public sealed record MessageChunk(string SessionId, string Text) : AgentEvent(SessionId);

/// <summary>A chunk of the agent's internal reasoning text.</summary>
public sealed record ThoughtChunk(string SessionId, string Text) : AgentEvent(SessionId);

/// <summary>A chunk of text echoed back on behalf of the user.</summary>
public sealed record UserMessageChunk(string SessionId, string Text) : AgentEvent(SessionId);

/// <summary>Content the client does not know how to render.</summary>
public sealed record UnsupportedContent(string SessionId, string ContentType) : AgentEvent(SessionId);

/// <summary>A tool call has been started by the agent.</summary>
public sealed record ToolCallStarted(string SessionId, string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawInputJson) : AgentEvent(SessionId);

/// <summary>An existing tool call has been updated.</summary>
/// <param name="SessionId">The id of the session this update belongs to.</param>
/// <param name="ToolCallId">The tool call's id.</param>
/// <param name="Title">The tool call's human-readable title, if the agent supplied one.</param>
/// <param name="Kind">The kind of tool call, e.g. <see cref="ToolKind.Edit"/>.</param>
/// <param name="Status">The tool call's current status.</param>
/// <param name="RawOutputJson">The tool call's raw output, as JSON, when this update carries it.</param>
/// <param name="RawInputJson">
/// The tool call's complete input, as JSON, when this update carries it. Request A-6 (finding P-1):
/// <c>claude-agent-acp</c> reports a streamed tool call's input as empty on the initial <c>tool_call</c>
/// and sends the complete input only on a later <c>tool_call_update</c> in the same turn, so a
/// consumer that reads only <see cref="ToolCallStarted.RawInputJson"/> would see it dropped.
/// </param>
public sealed record ToolCallUpdated(string SessionId, string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawOutputJson, string? RawInputJson = null) : AgentEvent(SessionId);

/// <summary>Describes a single entry within a plan.</summary>
public sealed record PlanEntryInfo(string Content, PlanEntryPriority Priority, PlanEntryStatus Status);

/// <summary>The agent published an updated plan.</summary>
public sealed record PlanUpdated(string SessionId, IReadOnlyList<PlanEntryInfo> Entries) : AgentEvent(SessionId);

/// <summary>The agent published updated token usage figures.</summary>
public sealed record UsageUpdated(string SessionId, long Size, long Used) : AgentEvent(SessionId);

/// <summary>The agent's current mode changed.</summary>
public sealed record ModeChanged(string SessionId, string ModeId) : AgentEvent(SessionId);

/// <summary>The agent's turn finished with the given stop reason.</summary>
public sealed record TurnCompleted(string SessionId, StopReason StopReason) : AgentEvent(SessionId);

/// <summary>An update of a kind not recognised by this client.</summary>
public sealed record UnknownUpdate(string SessionId, string TypeName) : AgentEvent(SessionId);