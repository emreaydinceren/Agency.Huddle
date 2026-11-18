using Agency.Huddle.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests;

/// <summary>Where the goldfish start-up pass sits in the hosted-service order.</summary>
public sealed class GoldfishSupervisorRegistrationTests
{
    /// <summary>
    /// <c>GoldfishSupervisor</c> resolves as a hosted service, after <c>TurnQueueRecovery</c> and before <c>PipeServer</c>, so no runner
    /// has connected when it ends what a restart left open.
    /// </summary>
    [Fact]
    public async Task HostedServices_SupervisorAfterTurnQueueRecoveryAndBeforePipeServer()
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
        services.AddSingleton<IHostApplicationLifetime>(new Agency.Huddle.Tests.Desktop.FakeLifetime());
        services.AddTeamServices(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        List<string> order = [.. provider.GetServices<IHostedService>().Select(service => service.GetType().Name)];

        int supervisor = order.IndexOf("GoldfishSupervisor");
        Assert.True(supervisor > order.IndexOf("TurnQueueRecovery") && order.IndexOf("TurnQueueRecovery") >= 0);
        Assert.True(order.IndexOf("PipeServer") > supervisor);
    }
}
