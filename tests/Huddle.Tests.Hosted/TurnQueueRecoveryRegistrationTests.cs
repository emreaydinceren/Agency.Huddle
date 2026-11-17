using Agency.Huddle.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests;

/// <summary>Where the Turn Queue's start-up pass sits in the hosted-service order (spec §5.7).</summary>
public sealed class TurnQueueRecoveryRegistrationTests
{
    /// <summary>
    /// <c>TurnQueueRecovery</c> starts after <c>DataInitializer</c> and before <c>PipeServer</c> and every poster that can post at
    /// start-up, because hosted services start in registration order.
    /// </summary>
    [Fact]
    public async Task HostedServices_RecoveryAfterDataInitializerAndBeforePipeServerAndPosters()
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

        int recovery = order.IndexOf("TurnQueueRecovery");
        Assert.True(recovery >= 0, "TurnQueueRecovery was not registered as a hosted service.");
        Assert.True(order.IndexOf("DataInitializer") is { } initializer && initializer >= 0 && initializer < recovery);
        foreach (string later in new[] { "PipeServer", "TaskTriggerService", "CadenceService", "JobScheduler" })
        {
            int index = order.IndexOf(later);
            Assert.True(index > recovery, $"{later} (index {index}) must start after TurnQueueRecovery (index {recovery}).");
        }
    }
}
