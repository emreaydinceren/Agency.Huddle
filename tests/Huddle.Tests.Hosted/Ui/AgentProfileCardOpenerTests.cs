using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using AgentsPage = Agency.Huddle.App.Components.Pages.Agents;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="AgentProfileCardOpener"/> (Global Search spec §9, OP-9): the one shared door to the
/// Agent card, used by the Agents page and by search so the two cannot drift.
/// </summary>
public sealed class AgentProfileCardOpenerTests
{
    /// <summary>An Agent definition for "Nova".</summary>
    private const string Nova = "---\nName: Nova\nTitle: Researcher\nAlias: Scout\n---\nYou research.";

    /// <summary>An Agent definition for "Kim".</summary>
    private const string Kim = "---\nName: Kim\nTitle: Editor\nAlias: Scribe\n---\nYou edit.";

    /// <summary>Opening an Agent by name, in any letter case, shows its card with the Agent's own Name, Title and Alias.</summary>
    [Fact]
    public async Task Opener_OpensTheCardForThatName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await AgentProfileCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = AgentProfileCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = AgentProfileCardOpenerTests.RenderProviders(ctx);
        AgentProfileCardOpener opener = ctx.Services.GetRequiredService<AgentProfileCardOpener>();

        bool opened = await cut.InvokeAsync(() => opener.OpenAsync("kim", ct));

        Assert.True(opened);
        cut.WaitForState(() => cut.FindComponents<AgentProfileCard>().Count == 1);
        AgentProfileCard card = cut.FindComponent<AgentProfileCard>().Instance;
        Assert.Equal("Kim", card.Name);
        Assert.Equal("Editor", card.Title);
        Assert.Equal("Scribe", card.Alias);
    }

    /// <summary>A name no Agent has opens nothing and says so by returning <see langword="false"/>.</summary>
    [Fact]
    public async Task Opener_UnknownName_OpensNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await AgentProfileCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = AgentProfileCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = AgentProfileCardOpenerTests.RenderProviders(ctx);
        AgentProfileCardOpener opener = ctx.Services.GetRequiredService<AgentProfileCardOpener>();

        bool opened = await cut.InvokeAsync(() => opener.OpenAsync("Nobody", ct));

        Assert.False(opened);
        Assert.Empty(cut.FindComponents<AgentProfileCard>());
        Assert.Empty(cut.FindAll(".mud-dialog"));
    }

    /// <summary>A tile on the Agents page still opens that Agent's card, now through the opener.</summary>
    [Fact]
    public async Task AgentsPage_TileStillOpensTheCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await AgentProfileCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = AgentProfileCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AgentsPage>(0);
            builder.CloseComponent();
        });

        _ = cut.InvokeAsync(() => cut.FindAll("button.agent-tile")
            .Single(candidate => candidate.TextContent.Contains("Nova", StringComparison.Ordinal))
            .ClickAsync());

        cut.WaitForState(() => cut.FindComponents<AgentProfileCard>().Count == 1);
        AgentProfileCard card = cut.FindComponent<AgentProfileCard>().Instance;
        Assert.Equal("Nova", card.Name);
        Assert.Equal("Researcher", card.Title);
    }

    /// <summary>Writes the two Agent definitions the tests open.</summary>
    /// <param name="factory">The factory whose Agents folder to write to.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task SeedAsync(TeamWebApplicationFactory factory, CancellationToken ct)
    {
        await factory.WriteDefinitionAsync("Nova", AgentProfileCardOpenerTests.Nova, ct);
        await factory.WriteDefinitionAsync("Kim", AgentProfileCardOpenerTests.Kim, ct);
    }

    /// <summary>Renders only the popover and dialog providers, so an opened card has somewhere to paint.</summary>
    /// <param name="ctx">The context to render in.</param>
    private static IRenderedComponent<ContainerFragment> RenderProviders(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(static _ => { });

    /// <summary>Registers the services the Agents page and the card need, plus the opener built over them.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Agents));
        ctx.Services.AddTransient(provider => new AgentProfileCardOpener(
            provider.GetRequiredService<PersonaStore>(),
            provider.GetRequiredService<ITeamDirectory>(),
            provider.GetRequiredService<IAgentGateway>(),
            provider.GetRequiredService<PersonaHealth>(),
            provider.GetRequiredService<IDialogService>()));
        return ctx;
    }}
