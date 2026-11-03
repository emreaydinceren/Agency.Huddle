using Agency.Huddle.App;
using Agency.Huddle.App.SecondBrain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that the retired-file scan runs at start-up (correction C7).</summary>
public sealed class RetiredFileScanRegistrationTests
{
    /// <summary>The service is registered as a hosted service.</summary>
    [Fact]
    public async Task AddTeamServices_RegistersRetiredFileScanService()
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

        Assert.Contains(provider.GetServices<IHostedService>(), static service => service is RetiredFileScanService);
    }
}
