namespace Agency.Huddle.Acp.DotAcp;

/// <summary>Options controlling how a <see cref="DotAcpAgentHost"/> connects to and identifies itself to an ACP agent.</summary>
public sealed record DotAcpHostOptions(string ClientName = "Team.Console", string? ClientVersion = null, bool TraceWire = false);