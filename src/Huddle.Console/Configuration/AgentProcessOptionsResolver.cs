namespace Agency.Huddle.Console.Configuration;

using System;
using Microsoft.Extensions.Configuration;
using Agency.Huddle.Acp.Hosting;

internal static class AgentProcessOptionsResolver
{
    private const string RepoRootToken = "${RepoRoot}";

    internal static AgentProcessOptions Resolve(IConfiguration configuration, Func<string, string?> getEnvironmentVariable, string repoRoot)
    {
        string? commandEnvironmentValue = getEnvironmentVariable("TEAM_ACP_COMMAND");
        string? command = !string.IsNullOrWhiteSpace(commandEnvironmentValue)
            ? commandEnvironmentValue
            : configuration["Acp:Process:Command"];

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Acp:Process:Command is not configured.");
        }

        string? argsEnvironmentValue = getEnvironmentVariable("TEAM_ACP_ARGS");
        string[] args = !string.IsNullOrWhiteSpace(argsEnvironmentValue)
            ? CommandLineSplitter.Split(argsEnvironmentValue)
            : configuration.GetSection("Acp:Process:Args").Get<string[]>() ?? [];

        command = command.Replace(RepoRootToken, repoRoot, StringComparison.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            args[i] = args[i].Replace(RepoRootToken, repoRoot, StringComparison.Ordinal);
        }

        return new AgentProcessOptions(command, args, repoRoot, null);
    }
}
