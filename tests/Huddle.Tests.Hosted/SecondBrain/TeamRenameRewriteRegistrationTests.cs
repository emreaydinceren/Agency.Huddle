using Agency.Huddle.App;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that the real host registers the non-Task step-6 rewrites of a Team rename (tr5c) in the order Views, Workspace, History, Ledgers.</summary>
public sealed class TeamRenameRewriteRegistrationTests
{
    /// <summary>The container resolves the four rewrites as <see cref="ITeamRenameRewrite"/>, in that order.</summary>
    [Fact]
    public async Task AddTeamServices_RegistersTheFourNonTaskRewritesInOrder()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:DataDir"] = dataDir.Path,
                ["Team:HumanName"] = "You",
                ["Team:DemoAgent:Enabled"] = "false",
                ["Team:Acp:Enabled"] = "false",
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();

        Type[] expected = [typeof(ViewsRenameRewrite), typeof(WorkspaceRenameRewrite), typeof(DocHistoryRenameRewrite), typeof(LedgerRenameRewrite)];
        Type[] actual = [.. provider.GetServices<ITeamRenameRewrite>().Select(static rewrite => rewrite.GetType()).Where(expected.Contains)];
        Assert.Equal(expected, actual);
    }
}
