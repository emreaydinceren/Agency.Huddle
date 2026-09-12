namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Console.Configuration;
using Xunit;

public sealed class AgentProcessOptionsResolverTests
{
    [Fact]
    public void Resolve_UsesConfigWhenNoEnv()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Acp:Process:Command"] = "node",
            ["Acp:Process:Args:0"] = "script.js",
        });

        AgentProcessOptions result = AgentProcessOptionsResolver.Resolve(configuration, name => null, "E:\\r");

        Assert.Equal("node", result.Command);
        Assert.Equal(new[] { "script.js" }, result.Args);
    }

    [Fact]
    public void Resolve_TeamAcpCommandOverrides()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Acp:Process:Command"] = "node",
        });

        AgentProcessOptions result = AgentProcessOptionsResolver.Resolve(
            configuration,
            name => name == "TEAM_ACP_COMMAND" ? "custom-command" : null,
            "E:\\r");

        Assert.Equal("custom-command", result.Command);
    }

    [Fact]
    public void Resolve_TeamAcpArgsOverridesAndSplits()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Acp:Process:Command"] = "node",
            ["Acp:Process:Args:0"] = "from-config.js",
        });

        AgentProcessOptions result = AgentProcessOptionsResolver.Resolve(
            configuration,
            name => name == "TEAM_ACP_ARGS" ? "a b  c" : null,
            "E:\\r");

        Assert.Equal(new[] { "a", "b", "c" }, result.Args);
    }

    [Fact]
    public void Resolve_ExpandsRepoRootToken()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Acp:Process:Command"] = "${RepoRoot}/tool",
            ["Acp:Process:Args:0"] = "${RepoRoot}/x",
        });

        AgentProcessOptions result = AgentProcessOptionsResolver.Resolve(configuration, name => null, "E:\\r");

        Assert.Equal("E:\\r/tool", result.Command);
        Assert.Equal(new[] { "E:\\r/x" }, result.Args);
    }

    [Fact]
    public void Resolve_MissingCommand_ThrowsInvalidOperation()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>());

        Assert.Throws<InvalidOperationException>(() =>
            AgentProcessOptionsResolver.Resolve(configuration, name => null, "E:\\r"));
    }

    [Fact]
    public void Resolve_SetsWorkingDirectoryToRepoRoot()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Acp:Process:Command"] = "node",
        });

        AgentProcessOptions result = AgentProcessOptionsResolver.Resolve(configuration, name => null, "E:\\r");

        Assert.Equal("E:\\r", result.WorkingDirectory);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
