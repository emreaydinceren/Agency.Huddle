using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
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

    /// <summary>The supervisor the real container builds plans a pipeline whose first hook is AGENTS.md, so the hook is wired, not just registered.</summary>
    [Fact]
    public async Task Container_SupervisorBuildsAPipelineWithAgentsMdFirst()
    {
        await using TeamWebApplicationFactory factory = new();
        PersonaSupervisor supervisor = factory.Services.GetRequiredService<PersonaSupervisor>();

        PersonaSupervisor.StartPlan plan = supervisor.PlanStart(new Persona("Vera", "---\nname: 'Vera'\n---\nYou help."));

        Assert.Equal("AgentsMd", plan.Hooks.HookNames[0]);
    }

    /// <summary>The supervisor's File Changes hook is built with the AGENTS.md scopes: an in-scope AGENTS.md change is left out of File Changes through the real provider.</summary>
    [Fact]
    public async Task Container_FileChangesHook_GetsTheScopes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        await using TeamWebApplicationFactory factory = new();
        string teamsRoot = Path.Combine(factory.DataDirPath, "Teams");
        Directory.CreateDirectory(teamsRoot);
        string agentsMd = Path.Combine(teamsRoot, "AGENTS.md");
        string note = Path.Combine(teamsRoot, "notes.md");
        PersonaSupervisor supervisor = factory.Services.GetRequiredService<PersonaSupervisor>();
        PersonaSupervisor.StartPlan plan = supervisor.PlanStart(new Persona("Vera", $"---\nname: 'Vera'\ntitle: 'Vera'\nalias: 'vera'\nwatches: ['{teamsRoot.Replace("\\", "/", StringComparison.Ordinal)}']\n---\nYou help."));
        Assert.Contains("FileChanges", plan.Hooks.HookNames);
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        Room room = await directory.CreateRoomAsync("scopes", [KnownIds.Human], bound.Token);
        Dictionary<string, string?> state = new(StringComparer.Ordinal);
        TurnHookBeforeResult first = await plan.Hooks.BeforeTurnAsync(room.Id, state, bound.Token, CancellationToken.None);
        await plan.Hooks.AfterTurnAsync(room.Id, state, first, [], bound.Token);
        await File.WriteAllTextAsync(agentsMd, "rule", bound.Token);
        await File.WriteAllTextAsync(note, "note", bound.Token);

        TurnHookBeforeResult second = await plan.Hooks.BeforeTurnAsync(room.Id, state, bound.Token, CancellationToken.None);

        string text = second.Additions["FileChanges"].Text ?? string.Empty;
        Assert.Contains("notes.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AGENTS.md", text, StringComparison.Ordinal);
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

    /// <summary>The dispatcher finds a Helper's invoker by id among the registered invokers, so one must exist for AGENTS.md Changes.</summary>
    [Fact]
    public async Task Container_RegistersAnInvokerForAgentsMdChange()
    {
        await using TeamWebApplicationFactory factory = new();

        IEnumerable<IHelperInvoker> invokers = factory.Services.GetServices<IHelperInvoker>();

        IHelperInvoker invoker = Assert.Single(invokers, static candidate => candidate.Helper.Id == AgentsMdChangeHelper.HelperId);
        Assert.IsType<AgentsMdChangeHelper>(invoker.Helper);
    }

    /// <summary>The watcher runs as a hosted service; without the registration the feature is silently dead.</summary>
    [Fact]
    public async Task Container_RegistersTheWatcherAsAHostedService()
    {
        await using TeamWebApplicationFactory factory = new();

        IEnumerable<IHostedService> hosted = factory.Services.GetServices<IHostedService>();

        Assert.Single(hosted, static service => service is AgentsMdWatcher);
    }
}
