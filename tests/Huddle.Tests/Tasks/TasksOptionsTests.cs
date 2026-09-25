using Agency.Huddle.App;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TasksOptions"/> configuration.</summary>
public sealed class TasksOptionsTests
{
    /// <summary>Defaults match the specification in §14.</summary>
    [Fact]
    public void Defaults_MatchSpec()
    {
        TasksOptions options = new();

        Assert.True(options.Enabled);
        Assert.Equal("Tasks", options.Dir);
        Assert.True(options.WakeEnabled);
        Assert.Equal(5, options.WakeCoalesceSeconds);
        Assert.Equal(10, options.AgentWakeBudget);
    }

    /// <summary>Binding from configuration reads the Team:Tasks section.</summary>
    [Fact]
    public void Bind_FromConfiguration_ReadsTeamTasksSection()
    {
        Dictionary<string, string?> config = new()
        {
            { "Team:Tasks:Dir", "Work" },
            { "Team:Tasks:AgentWakeBudget", "3" },
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        TeamOptions options = new();
        configuration.GetSection("Team").Bind(options);

        Assert.Equal("Work", options.Tasks.Dir);
        Assert.Equal(3, options.Tasks.AgentWakeBudget);
        // Check that unspecified options keep their defaults
        Assert.True(options.Tasks.Enabled);
        Assert.True(options.Tasks.WakeEnabled);
        Assert.Equal(5, options.Tasks.WakeCoalesceSeconds);
    }
}
