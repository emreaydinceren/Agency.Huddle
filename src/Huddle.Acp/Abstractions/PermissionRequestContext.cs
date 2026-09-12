namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>Describes the tool call a permission request is being asked about.</summary>
public sealed record ToolCallInfo(string ToolCallId, string? Title, ToolKind Kind, ToolCallStatus Status, string? RawInputJson);

/// <summary>Describes one option the user may choose when answering a permission request.</summary>
public sealed record PermissionOptionInfo(string OptionId, string Name, PermissionOptionKind Kind);

/// <summary>Carries everything needed to decide a permission request.</summary>
public sealed record PermissionRequestContext(string SessionId, ToolCallInfo ToolCall, IReadOnlyList<PermissionOptionInfo> Options);