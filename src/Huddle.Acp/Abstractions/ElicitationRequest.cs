namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// One form the agent asks the Human to fill in (ACP <c>elicitation/create</c>, form mode): an
/// AskUserQuestion, a refusal-fallback choice, or an MCP server's form. A plain value: no protocol
/// library type appears in it.
/// </summary>
/// <param name="SessionId">The session the request belongs to.</param>
/// <param name="ToolCallId">The tool call that triggered the request, or null when there is none (a refusal dialog or an MCP form).</param>
/// <param name="Message">The prompt text the agent wants shown.</param>
/// <param name="RequestedSchemaJson">The form's JSON Schema as one compact JSON text, exactly as the agent sent it; the caller parses it.</param>
/// <param name="UntrustedDetail">
/// Text the card shows as plain text under a label that says it came from the Agent's request (a tool call's arguments), or
/// <see langword="null"/>. Set only by the host itself, never read from the wire.
/// </param>
public sealed record ElicitationRequest(string SessionId, string? ToolCallId, string Message, string RequestedSchemaJson, string? UntrustedDetail = null);
