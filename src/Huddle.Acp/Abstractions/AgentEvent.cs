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

/// <summary>The ACP <c>diff</c> content block of a tool call: one change to one file.</summary>
/// <param name="Path">The absolute path the Adapter names.</param>
/// <param name="OldText">The text replaced, or <see langword="null"/> when the call creates the file.</param>
/// <param name="NewText">The replacement text, or the whole new file for a write.</param>
/// <param name="OmittedChanges">How many further <c>diff</c> blocks the call carried and this record drops.</param>
public sealed record ToolCallDiffInfo(string Path, string? OldText, string NewText, int OmittedChanges = 0);

/// <summary>One entry of a tool call's ACP <c>locations</c>: a file it touches, and the line when known.</summary>
/// <param name="Path">The file the call touches.</param>
/// <param name="Line">The 1-based line, or <see langword="null"/> when the Adapter did not say.</param>
public sealed record ToolCallLocationInfo(string Path, int? Line);

/// <summary>The ACP <c>cost</c> of a <c>usage_update</c>: a running total for the session, not a per-Turn figure.</summary>
/// <param name="Amount">The running total the Adapter reports.</param>
/// <param name="Currency">The ISO 4217 currency code the Adapter reports.</param>
public sealed record UsageCost(decimal Amount, string Currency);

/// <summary>A tool call has been started by the agent.</summary>
/// <param name="SessionId">The id of the session this update belongs to.</param>
/// <param name="ToolCallId">The tool call's id.</param>
/// <param name="Title">The tool call's human-readable title, if the agent supplied one.</param>
/// <param name="Kind">The kind of tool call, e.g. <see cref="ToolKind.Edit"/>.</param>
/// <param name="Status">The tool call's current status.</param>
/// <param name="RawInputJson">The tool call's raw input, as JSON, when this notification carries it.</param>
/// <param name="Location">The first entry of the call's <c>locations</c>, or <see langword="null"/> when this notification has none.</param>
/// <param name="Diff">The first <c>diff</c> content block, or <see langword="null"/> when this notification has none.</param>
public sealed record ToolCallStarted(string SessionId, string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawInputJson, ToolCallLocationInfo? Location = null, ToolCallDiffInfo? Diff = null) : AgentEvent(SessionId);

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
/// <param name="Location">The first entry of the call's <c>locations</c>, or <see langword="null"/> when this update omits it (ACP: omitted means unchanged).</param>
/// <param name="Diff">The first <c>diff</c> content block, or <see langword="null"/> when this update omits it (ACP: omitted means unchanged).</param>
public sealed record ToolCallUpdated(string SessionId, string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawOutputJson, string? RawInputJson = null, ToolCallLocationInfo? Location = null, ToolCallDiffInfo? Diff = null) : AgentEvent(SessionId);

/// <summary>Describes a single entry within a plan.</summary>
public sealed record PlanEntryInfo(string Content, PlanEntryPriority Priority, PlanEntryStatus Status);

/// <summary>The agent published an updated plan.</summary>
public sealed record PlanUpdated(string SessionId, IReadOnlyList<PlanEntryInfo> Entries) : AgentEvent(SessionId);

/// <summary>The agent published updated token usage figures.</summary>
/// <param name="SessionId">The id of the session this update belongs to.</param>
/// <param name="Size">The context window size, in tokens.</param>
/// <param name="Used">The tokens in use.</param>
/// <param name="Cost">The session's running cost, or <see langword="null"/> when this update carries none.</param>
public sealed record UsageUpdated(string SessionId, long Size, long Used, UsageCost? Cost = null) : AgentEvent(SessionId);

/// <summary>The agent's current mode changed.</summary>
public sealed record ModeChanged(string SessionId, string ModeId) : AgentEvent(SessionId);

/// <summary>The agent's turn finished with the given stop reason.</summary>
public sealed record TurnCompleted(string SessionId, StopReason StopReason) : AgentEvent(SessionId);

/// <summary>One command an agent advertises for a session (ACP <c>available_commands_update</c>).</summary>
/// <param name="Name">The command's name, without the leading slash, in the agent's own casing.</param>
/// <param name="Description">The agent's one-line description of the command.</param>
/// <param name="InputHint">The placeholder the agent suggests for the command's free-text input, or <see langword="null"/> when it takes none.</param>
public sealed record AvailableCommandInfo(string Name, string Description, string? InputHint);

/// <summary>
/// The agent replaced its list of available commands. Each update is the complete list, never a delta, and
/// an agent may send it more than once (after <c>session/new</c>, and again at the start of a turn).
/// </summary>
/// <param name="SessionId">The id of the session this update belongs to.</param>
/// <param name="Commands">Every command the agent currently advertises.</param>
public sealed record AvailableCommandsUpdated(string SessionId, IReadOnlyList<AvailableCommandInfo> Commands) : AgentEvent(SessionId);

/// <summary>An update of a kind not recognised by this client.</summary>
public sealed record UnknownUpdate(string SessionId, string TypeName) : AgentEvent(SessionId);