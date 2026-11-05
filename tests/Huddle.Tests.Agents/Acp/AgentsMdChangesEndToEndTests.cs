using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.Tests.Conformance;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// AGENTS.md changes end to end through the real host: the mock-adapter fixture builds the whole Turn pipeline from the container
/// and answers the Helper from a fake process runner, with no child process and no token spent.
/// </summary>
public sealed class AgentsMdChangesEndToEndTests
{
    /// <summary>The fixture resolves the fake in place of the real runner and the AGENTS.md hook from the container, and a real Helper call reaches the fake with a reply valid against the verdict.</summary>
    [Fact]
    public async Task FixtureWiring_ResolvesTheFakeRunner_AndAFullPipeline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartFullPipelineAsync(persona, cancellationToken: bound.Token);

        Assert.Same(fixture.HelperProcess, fixture.Services.GetRequiredService<IHelperProcessRunner>());
        Assert.NotNull(fixture.Services.GetService<AgentsMdTurnHook>());
        Assert.Equal(0, fixture.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Agency.Huddle.App.TeamOptions>>().Value.AgentsMd.NoticeWaitSeconds);

        const string OldText = "Use tabs.";
        const string NewText = "Use spaces.";
        fixture.ScriptVerdict(NewText);
        AgentsMdChangeInput input = new("AGENTS.md", "Teams", "a", "b", OldText, NewText, AgentsMdComparison.Between(OldText, NewText));
        fixture.Services.GetRequiredService<HelperDispatcher>().Enqueue(AgentsMdChangeHelper.HelperId, "wiring-proof", input);

        await Eventually.UntilAsync(() => fixture.HelperProcess.Calls > 0, bound.Token, TestBudgets.Slowest);

        AgentsMdChangeVerdict? verdict = JsonSerializer.Deserialize<AgentsMdChangeVerdict>(fixture.HelperProcess.Text, JsonSerializerOptions.Web);
        Assert.NotNull(verdict);
        Assert.True(verdict.Meaningful);
        Assert.Equal(NewText, Assert.Single(verdict.Changes ?? []).Quote);
    }
}
