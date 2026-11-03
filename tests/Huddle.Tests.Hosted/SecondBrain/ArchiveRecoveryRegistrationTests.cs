using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that archive recovery runs once at start-up, before the Team folders are synchronised (corrections-12 items 4 and 8).</summary>
public sealed class ArchiveRecoveryRegistrationTests
{
    /// <summary>The recovery service is a hosted service and starts before the Team folder provisioner and the Room reconciler.</summary>
    [Fact]
    public async Task AddTeamServices_RegistersArchiveRecoveryBeforeProvisionerAndReconciler()
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
        int recovery = hosted.FindIndex(static s => s is ArchiveRecoveryService);
        Assert.True(recovery >= 0);
        Assert.True(recovery < hosted.FindIndex(static s => s is TeamFolderProvisioner));
        Assert.True(recovery < hosted.FindIndex(static s => s is TeamRoomReconciler));
    }
}
