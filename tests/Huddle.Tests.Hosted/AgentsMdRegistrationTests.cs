using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests;

/// <summary>Pins AGENTS.md Changes spec 6.2: the real container resolves the ledger and the Helper without a DI cycle.</summary>
public sealed class AgentsMdRegistrationTests
{
    /// <summary>The ledger, the Helper, the dispatcher, the registry and the scopes all resolve from the started container.</summary>
    [Fact]
    public async Task Container_ResolvesTheLedgerAndHelper()
    {
        await using TeamWebApplicationFactory factory = new();
        IServiceProvider services = factory.Services;

        Assert.NotNull(services.GetService<AgentsMdLedger>());
        Assert.NotNull(services.GetService<AgentsMdChangeHelper>());
        Assert.NotNull(services.GetService<HelperDispatcher>());
        Assert.NotNull(services.GetService<HelperRegistry>());
        Assert.NotNull(services.GetService<AgentsMdScopes>());
    }

    /// <summary>The Helpers tab draws one card per registry entry, so the registry must list AGENTS.md Changes.</summary>
    [Fact]
    public async Task HelperCard_ListsAgentsMdChange()
    {
        await using TeamWebApplicationFactory factory = new();
        HelperRegistry registry = factory.Services.GetRequiredService<HelperRegistry>();

        IHelperAgent? helper = registry.Find(AgentsMdChangeHelper.HelperId);

        Assert.NotNull(helper);
        Assert.Equal("AGENTS.md Changes", helper.DisplayName);
        Assert.Contains(registry.All, static entry => entry.Id == AgentsMdChangeHelper.HelperId);
    }
}
