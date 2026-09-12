namespace Agency.Huddle.Acp.Hosting;

using System.Collections.Generic;

/// <summary>Describes how to launch an agent process.</summary>
public sealed record AgentProcessOptions(
    string Command,
    IReadOnlyList<string> Args,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? EnvironmentOverrides = null);