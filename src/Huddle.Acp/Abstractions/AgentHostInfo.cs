namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>Describes the agent an <see cref="IAgentHost"/> is connected to.</summary>
public sealed record AgentHostInfo(string AgentName, string? AgentVersion, int ProtocolVersion, IReadOnlyList<AuthMethodInfo> AuthMethods, bool SupportsLoadSession);