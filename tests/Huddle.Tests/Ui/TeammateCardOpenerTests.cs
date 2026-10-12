using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;
using TeammatesPage = Agency.Huddle.App.Components.Pages.Teammates;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="TeammateCardOpener"/> (Global Search spec §9, OP-9): the one shared door to the
/// Teammate card, used by the Teammates page and by search so the two cannot drift.
/// </summary>
public sealed class TeammateCardOpenerTests
{
    /// <summary>A Teammate definition for "Nova".</summary>
    private const string Nova = "---\nName: Nova\nTitle: Researcher\nAlias: Scout\n---\nYou research.";

    /// <summary>A Teammate definition for "Kim".</summary>
    private const string Kim = "---\nName: Kim\nTitle: Editor\nAlias: Scribe\n---\nYou edit.";

    /// <summary>Opening a Teammate by name, in any letter case, shows its card with the Teammate's own Name, Title and Alias.</summary>
    [Fact]
    public async Task Opener_OpensTheCardForThatName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await TeammateCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = TeammateCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = TeammateCardOpenerTests.RenderProviders(ctx);
        TeammateCardOpener opener = ctx.Services.GetRequiredService<TeammateCardOpener>();

        bool opened = await cut.InvokeAsync(() => opener.OpenAsync("kim", ct));

        Assert.True(opened);
        cut.WaitForState(() => cut.FindComponents<TeammateCard>().Count == 1);
        TeammateCard card = cut.FindComponent<TeammateCard>().Instance;
        Assert.Equal("Kim", card.Name);
        Assert.Equal("Editor", card.Title);
        Assert.Equal("Scribe", card.Alias);
    }

    /// <summary>A name no Teammate has opens nothing and says so by returning <see langword="false"/>.</summary>
    [Fact]
    public async Task Opener_UnknownName_OpensNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await TeammateCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = TeammateCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = TeammateCardOpenerTests.RenderProviders(ctx);
        TeammateCardOpener opener = ctx.Services.GetRequiredService<TeammateCardOpener>();

        bool opened = await cut.InvokeAsync(() => opener.OpenAsync("Nobody", ct));

        Assert.False(opened);
        Assert.Empty(cut.FindComponents<TeammateCard>());
        Assert.Empty(cut.FindAll(".mud-dialog"));
    }

    /// <summary>A tile on the Teammates page still opens that Teammate's card, now through the opener.</summary>
    [Fact]
    public async Task TeammatesPage_TileStillOpensTheCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await TeammateCardOpenerTests.SeedAsync(factory, ct);
        await using MudBunitContext ctx = TeammateCardOpenerTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeammatesPage>(0);
            builder.CloseComponent();
        });

        _ = cut.InvokeAsync(() => cut.FindAll("button.teammate-tile")
            .Single(candidate => candidate.TextContent.Contains("Nova", StringComparison.Ordinal))
            .Click());

        cut.WaitForState(() => cut.FindComponents<TeammateCard>().Count == 1);
        TeammateCard card = cut.FindComponent<TeammateCard>().Instance;
        Assert.Equal("Nova", card.Name);
        Assert.Equal("Researcher", card.Title);
    }

    /// <summary>Writes the two Teammate definitions the tests open.</summary>
    /// <param name="factory">The factory whose Teammates folder to write to.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task SeedAsync(TeamWebApplicationFactory factory, CancellationToken ct)
    {
        await factory.WriteDefinitionAsync("Nova", TeammateCardOpenerTests.Nova, ct);
        await factory.WriteDefinitionAsync("Kim", TeammateCardOpenerTests.Kim, ct);
    }

    /// <summary>Renders only the popover and dialog providers, so an opened card has somewhere to paint.</summary>
    /// <param name="ctx">The context to render in.</param>
    private static IRenderedComponent<ContainerFragment> RenderProviders(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(static _ => { });

    /// <summary>Registers the services the Teammates page and the card need, plus the opener built over them.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSpend>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaCommands>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AdapterCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<BuiltinTeammateReset>());
        ctx.Services.AddTransient(provider => new TeammateCardOpener(
            provider.GetRequiredService<PersonaStore>(),
            provider.GetRequiredService<ITeamDirectory>(),
            provider.GetRequiredService<IAgentGateway>(),
            provider.GetRequiredService<PersonaHealth>(),
            provider.GetRequiredService<IDialogService>()));
        return ctx;
    }
}
