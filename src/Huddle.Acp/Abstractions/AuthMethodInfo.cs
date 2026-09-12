namespace Agency.Huddle.Acp.Abstractions;

/// <summary>Describes an authentication method advertised by the agent.</summary>
public sealed record AuthMethodInfo(string Id, string Name, string? Description);