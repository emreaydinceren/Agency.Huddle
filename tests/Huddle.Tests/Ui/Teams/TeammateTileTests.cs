using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Acp.Fakes;
using TeammatesPage = Agency.Huddle.App.Components.Pages.Teammates;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins U20 T20.1 (UX-001 Teammates, UX-051 to UX-054): a Teammates tile shows the shared
/// <c>TeammateAvatar</c> at <c>Size.Large</c> with no size or radius override, its alias in a small
/// <c>MudChip</c>, and left-aligned content; Build a team's roster draws its avatars through
/// <c>TeammateAvatar</c> rather than a bare <c>MudAvatar</c>.
/// </summary>
public sealed class TeammateTileTests
{
    /// <summary>The tile's avatar is a Large circle: MudBlazor's own large size, not the page's square override.</summary>
    [Fact]
    public async Task Tile_Avatar_IsLargeWithNoSizeOverride()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<TeammatesPage> cut = ctx.Render<TeammatesPage>();

        var avatar = cut.Find(".teammate-tile .mud-avatar");
        Assert.Contains("mud-avatar-large", avatar.ClassList);
        Assert.DoesNotContain("teammate-avatar", avatar.ClassList);
    }

    /// <summary>The page opens with the shared header: one h1 reading "Teammates" (UX-001).</summary>
    [Fact]
    public async Task Page_HasTeammatesHeader()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<TeammatesPage> cut = ctx.Render<TeammatesPage>();

        Assert.Equal("Teammates", cut.Find("h1").TextContent.Trim());
    }

    /// <summary>The Team filter select is Outlined and Dense.</summary>
    [Fact]
    public async Task TeamFilter_IsOutlinedAndDense()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<TeammatesPage> cut = ctx.Render<TeammatesPage>();

        Assert.NotEmpty(cut.FindAll(".teammates-filter .mud-input-outlined"));
        Assert.NotEmpty(cut.FindAll(".teammates-filter .mud-input-margin-dense"));
    }

    /// <summary>The stylesheet no longer declares the <c>.teammate-avatar</c> size and radius override.</summary>
    [Fact]
    public void Stylesheet_HasNoTeammateAvatarOverride()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.DoesNotContain(".teammate-avatar", css, StringComparison.Ordinal);
    }

    /// <summary>The alias reads '@alias' inside a small MudChip, not a hand-styled span.</summary>
    [Fact]
    public async Task Tile_Alias_IsASmallMudChip()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<TeammatesPage> cut = ctx.Render<TeammatesPage>();

        var chip = cut.FindAll(".teammate-tile .teammate-tile-chips .mud-chip")
            .First(item => item.TextContent.Trim() == "@coo");
        Assert.Contains("mud-chip-size-small", chip.ClassList);
        Assert.Empty(cut.FindAll(".teammate-tile .teammate-chip"));
    }

    /// <summary>The tile's text and chips are left-aligned inside the tile.</summary>
    [Fact]
    public void Stylesheet_TileContentIsLeftAligned()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));
        int start = css.IndexOf(".teammate-tile {", StringComparison.Ordinal);
        int end = css.IndexOf('}', start);
        string tile = css[start..end];

        Assert.Contains("text-align: left;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
        Assert.Contains("align-items: flex-start;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
    }

    /// <summary>Build a team's roster card draws its avatar through <c>TeammateAvatar</c>, not a bare <c>MudAvatar</c>.</summary>
    [Fact]
    public void BuildTeam_Source_UsesTeammateAvatar()
    {
        string razor = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "BuildTeam.razor"));

        Assert.Contains("<TeammateAvatar ", razor, StringComparison.Ordinal); // contains-ok: haystack is the razor source file, which a render cannot distinguish
        Assert.DoesNotContain("<MudAvatar", razor, StringComparison.Ordinal);
    }

    private static async Task<MudBunitContext> NewContextAsync(TempDataDir dataDir, CancellationToken ct)
    {
        Agency.Huddle.Tests.Acp.TestPersonaFiles.Write(
            new TeammatePaths(dataDir.Options()), "coo", "---\nName: coo\nTitle: coo\nAlias: coo\n---\nYou are the Chief of Staff.");

        SqliteTeamDirectory directory = new(dataDir.Options());
        await directory.InitializeAsync("You", ct);
        _ = await directory.UpsertAgentUserAsync("coo", null, ct);

        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        PersonaStore personas = new(
            new TeammatePaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        AdapterProfileResolver resolver = new(new AdapterCatalog(dataDir.Options()));
        SkillStore skillStore = new(dataDir.Options(), NullLogger<SkillStore>.Instance);
        PersonaSupervisor supervisor = new(
            dataDir.Options(), personas, new FakeAgentHostFactory(), resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);
        AvatarStore avatars = new(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamDirectory>(directory);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(health);
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        ctx.Services.AddSingleton<IModelCatalog>(new FakeModelCatalog());
        ctx.Services.AddSingleton(personas);
        ctx.Services.AddSingleton(supervisor);
        ctx.Services.AddSingleton(avatars);
        return ctx;
    }
}
