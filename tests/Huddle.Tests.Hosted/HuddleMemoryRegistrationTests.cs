using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests;

/// <summary>Pins the production wiring of the Huddle-wide Memory hook (#361): registered in the container and handed to the supervisor's pipeline.</summary>
public sealed class HuddleMemoryRegistrationTests
{
    /// <summary>The real container resolves the hook; without the registration the supervisor gets none.</summary>
    [Fact]
    public async Task Container_ResolvesTheHuddleMemoryHook()
    {
        await using TeamWebApplicationFactory factory = new();

        Assert.NotNull(factory.Services.GetService<HuddleMemoryTurnHook>());
    }

    /// <summary>The real container registers all three hooks as <see cref="ITurnHook"/>, so a new hook is one class and one registration.</summary>
    [Fact]
    public async Task Container_RegistersEveryTurnHook()
    {
        await using TeamWebApplicationFactory factory = new();

        IEnumerable<string> names = factory.Services.GetServices<ITurnHook>().Select(static hook => hook.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["AgentsMd", "FileChanges", "HuddleMemory"], names);
    }

    /// <summary>The supervisor the real container builds plans a pipeline that runs AGENTS.md, then the Huddle-wide hook, so it is wired and not only registered.</summary>
    [Fact]
    public async Task Container_SupervisorBuildsAPipelineWithHuddleMemoryAfterAgentsMd()
    {
        await using TeamWebApplicationFactory factory = new();
        PersonaSupervisor supervisor = factory.Services.GetRequiredService<PersonaSupervisor>();

        PersonaSupervisor.StartPlan plan = supervisor.PlanStart(new Persona("Vera", "---\nname: 'Vera'\n---\nYou help."));

        Assert.Equal(["AgentsMd", "HuddleMemory"], plan.Hooks.HookNames.Take(2));
    }
}
