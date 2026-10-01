namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>
/// Describes the agent an <see cref="IAgentHost"/> is connected to. <c>SupportsResumeSession</c>
/// is whether the agent advertised <c>agentCapabilities.sessionCapabilities.resume</c> (RS §6.4 A-3).
/// <c>PromptCapabilities</c> is what the agent said a prompt may carry; a <see langword="null"/> member
/// means <see cref="AgentPromptCapabilities.None"/>, and a started host always supplies one.
/// </summary>
public sealed record AgentHostInfo(string AgentName, string? AgentVersion, int ProtocolVersion, IReadOnlyList<AuthMethodInfo> AuthMethods, bool SupportsLoadSession, bool SupportsResumeSession = false, AgentPromptCapabilities? PromptCapabilities = null);
