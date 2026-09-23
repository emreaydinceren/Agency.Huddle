namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>
/// Describes the agent an <see cref="IAgentHost"/> is connected to. <c>SupportsResumeSession</c>
/// is whether the agent advertised <c>agentCapabilities.sessionCapabilities.resume</c> (RS §6.4 A-3).
/// </summary>
public sealed record AgentHostInfo(string AgentName, string? AgentVersion, int ProtocolVersion, IReadOnlyList<AuthMethodInfo> AuthMethods, bool SupportsLoadSession, bool SupportsResumeSession = false);