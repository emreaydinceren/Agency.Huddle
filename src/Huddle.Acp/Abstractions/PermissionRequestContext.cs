namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>Describes the tool call a permission request is being asked about.</summary>
/// <param name="ToolCallId">The Adapter's id for the call.</param>
/// <param name="Title">The display text the Adapter gave the call, or <see langword="null"/>.</param>
/// <param name="Kind">What sort of tool it is.</param>
/// <param name="Status">Where the call stands.</param>
/// <param name="RawInputJson">The call's arguments as JSON text, or <see langword="null"/>.</param>
/// <param name="ToolName">The Adapter's own name for the tool (<c>_meta.claudeCode.toolName</c>), or <see langword="null"/> when the request carries none.</param>
/// <param name="McpServerName">The MCP server that serves the tool (<c>_meta.claudeCode.mcpServer.name</c>), or <see langword="null"/>. Untrusted text: the config key as authored.</param>
/// <param name="McpServerSource">Where that server's definition came from (<c>_meta.claudeCode.mcpServer.source</c>: <c>sdk</c>, <c>claudeai</c>, <c>project</c> and so on, an open set), or <see langword="null"/>. The Adapter says to key trust on this, not on the name.</param>
public sealed record ToolCallInfo(
    string ToolCallId,
    string? Title,
    ToolKind Kind,
    ToolCallStatus Status,
    string? RawInputJson,
    string? ToolName = null,
    string? McpServerName = null,
    string? McpServerSource = null);

/// <summary>Describes one option the user may choose when answering a permission request.</summary>
public sealed record PermissionOptionInfo(string OptionId, string Name, PermissionOptionKind Kind);

/// <summary>Carries everything needed to decide a permission request.</summary>
/// <param name="SessionId">The session the request belongs to.</param>
/// <param name="ToolCall">The call being asked about.</param>
/// <param name="Options">The options the Adapter offered.</param>
/// <param name="Scope">
/// The session's scope for asking the Human, or <see langword="null"/> when none is bound. A handler that must stop for an
/// answer asks through it; it resolves the Turn's Room.
/// </param>
public sealed record PermissionRequestContext(
    string SessionId,
    ToolCallInfo ToolCall,
    IReadOnlyList<PermissionOptionInfo> Options,
    IElicitationScope? Scope = null);