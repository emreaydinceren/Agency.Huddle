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
public sealed record ToolCallUpdated(string SessionId, string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawOutputJson) : AgentEvent(SessionId);

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