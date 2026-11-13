using Agency.Huddle.App;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that Team rename recovery runs at start-up before archive recovery, so a half-renamed Team folder is finished first (Team Rename Spec R16, tr6).</summary>
public sealed class TeamRenameRecoveryRegistrationTests
{
    /// <summary>The rename recovery service is a hosted service registered before <see cref="ArchiveRecoveryService"/>.</summary>
    [Fact]
    public async Task AddTeamServices_RegistersTeamRenameRecoveryBeforeArchiveRecovery()
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

        List<IHostedService> hosted = [.. provider.GetServices<IHostedService>()];
        int rename = hosted.FindIndex(static s => s is TeamRenameRecoveryService);
        Assert.True(rename >= 0);
        Assert.True(rename < hosted.FindIndex(static s => s is ArchiveRecoveryService));
    }
}
