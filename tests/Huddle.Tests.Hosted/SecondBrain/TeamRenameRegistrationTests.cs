using System.Reflection;
using Agency.Huddle.App;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins how the real host wires the Team rename (corrections-batch2 tr5 items 1, 8 and 10).</summary>
public sealed class TeamRenameRegistrationTests
{
    /// <summary>The rename service resolves, the journal is the store, both services share one gate, and the reconciler is one singleton that is also the hosted service.</summary>
    [Fact]
    public async Task AddTeamServices_RegistersRenameServiceJournalSharedGateAndReconcilerSingleton()
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

        TeamRenameService rename = provider.GetRequiredService<TeamRenameService>();
        Assert.IsType<TeamRenameStore>(provider.GetRequiredService<ITeamRenames>());
        TeamOperationGate gate = provider.GetRequiredService<TeamOperationGate>();
        Assert.Same(gate, GateOf(rename));
        Assert.Same(gate, GateOf(provider.GetRequiredService<ArchiveService>()));
        TeamRoomReconciler reconciler = provider.GetRequiredService<TeamRoomReconciler>();
        Assert.Same(reconciler, provider.GetRequiredService<TeamRoomReconciler>());
        Assert.Same(reconciler, Assert.Single(provider.GetServices<IHostedService>().OfType<TeamRoomReconciler>()));
    }

    /// <summary>The one instance of <see cref="TeamOperationGate"/> a service holds, found by field type.</summary>
    /// <param name="service">The service.</param>
    private static TeamOperationGate GateOf(object service)
    {
        FieldInfo field = Assert.Single(
            service.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            static f => f.FieldType == typeof(TeamOperationGate));
        return Assert.IsType<TeamOperationGate>(field.GetValue(service));
    }
}
