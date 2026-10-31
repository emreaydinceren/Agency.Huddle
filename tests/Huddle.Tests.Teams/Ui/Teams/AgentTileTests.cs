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
using AgentsPage = Agency.Huddle.App.Components.Pages.Agents;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins U20 T20.1 (UX-001 Agents, UX-051 to UX-054): an Agents tile shows the shared
/// <c>AgentAvatar</c> at <c>Size.Large</c> with no size or radius override, its alias in a small
/// <c>MudChip</c>, and left-aligned content; Build a team's roster draws its avatars through
/// <c>AgentAvatar</c> rather than a bare <c>MudAvatar</c>.
/// </summary>
public sealed class AgentTileTests
{
    /// <summary>The tile's avatar is the 40px card size (MudBlazor's Medium), not the page's square override, with a presence badge at its rim.</summary>
    [Fact]
    public async Task Tile_Avatar_IsMediumWithNoSizeOverrideAndABadge()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        var avatar = cut.Find(".agent-tile .mud-avatar");
        Assert.Contains("mud-avatar-medium", avatar.ClassList);
        Assert.DoesNotContain("agent-avatar", avatar.ClassList);
        Assert.NotNull(cut.Find(".agent-tile .presence-anchor > .agent-dot.on-avatar"));
    }

    /// <summary>The tile states its state once, as plain meta text beside the alias: no chip, no second dot.</summary>
    [Fact]
    public async Task Tile_State_IsPlainMetaTextBesideTheAlias()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        var state = cut.Find(".agent-tile .agent-tile-chips .agent-tile-state");
        Assert.Contains("chip-meta", state.ClassList);
        Assert.Equal(cut.Find(".agent-tile .agent-dot").GetAttribute("aria-label"), state.TextContent.Trim());
        Assert.Single(cut.FindAll(".agent-tile .agent-dot"));
    }

    /// <summary>A group heading carries its count in a separate caption-size span.</summary>
    [Fact]
    public async Task GroupHeading_ShowsTheCountInItsOwnSpan()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        Assert.Equal("1", cut.Find("h2.agents-group-heading .agents-group-count").TextContent.Trim());
    }

    /// <summary>The grid has columns of at least 240px that share the width, and the tile text sizes follow the spec (name 14/600, role 13, meta and context 12).</summary>
    [Fact]
    public void Stylesheet_TheGridFillsTheWidthAndTheTileTextFollowsTheSpec()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Contains("grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains(".agents-item-name {\r\n    font-size: var(--mud-typography-subtitle1-size);\r\n    font-weight: 600;", css.ReplaceLineEndings("\r\n"), StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains(".agent-tile-role {\r\n    font-size: var(--mud-typography-body2-size);", css.ReplaceLineEndings("\r\n"), StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains(".agent-tile-context {", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
    }

    /// <summary>The page opens with the shared header: one h1 reading "Agents" (UX-001).</summary>
    [Fact]
    public async Task Page_HasAgentsHeader()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        Assert.Equal("Agents", cut.Find("h1").TextContent.Trim());
    }

    /// <summary>The Team filter select is Outlined and Dense.</summary>
    [Fact]
    public async Task TeamFilter_IsOutlinedAndDense()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        Assert.NotEmpty(cut.FindAll(".agents-filter .mud-input-outlined"));
        Assert.NotEmpty(cut.FindAll(".agents-filter .mud-input-margin-dense"));
    }

    /// <summary>The stylesheet no longer declares the <c>.agent-avatar</c> size and radius override.</summary>
    [Fact]
    public void Stylesheet_HasNoAgentAvatarOverride()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain(".agent-avatar", css, StringComparison.Ordinal);
    }

    /// <summary>The alias reads '@alias' as plain caption text (the "count or meta" style), not as a chip.</summary>
    [Fact]
    public async Task Tile_Alias_IsPlainCaptionText()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        var alias = cut.FindAll(".agent-tile .agent-tile-chips .chip-meta")
            .First(item => item.TextContent.Trim() == "@coo");
        Assert.Contains("chip-meta", alias.ClassList);
        Assert.Empty(cut.FindAll(".agent-tile .mud-chip"));
        Assert.Empty(cut.FindAll(".agent-tile .agent-chip"));
    }

    /// <summary>
    /// The tile's avatar and text start at its left padding: MudBlazor's global <c>button</c> rule centres a flex button's
    /// content, which put the avatar 50px in and left the "Working on" line's fixed indent off the name's edge.
    /// </summary>
    [Fact]
    public void Stylesheet_TileContentIsLeftAligned()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int start = css.IndexOf(".agent-tile {", StringComparison.Ordinal);
        int end = css.IndexOf('}', start);
        string tile = css[start..end];

        Assert.Contains("justify-content: flex-start;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
        Assert.Contains("text-align: left;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
        Assert.Contains("align-items: flex-start;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
    }

    /// <summary>The page intro says what an Agent is to the Human, in the defined terms, and no longer describes the Markdown file.</summary>
    [Fact]
    public async Task Intro_SaysWhatAAgentIsToTheHuman()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        string intro = string.Join(" ", cut.Find(".page-header-subtitle").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("Agents are the Agents who work with you in Rooms and on Tasks. Open one to see its role and Skills, or to change how it works.", intro);
    }

    /// <summary>The "Working on" line is left-aligned on the same edge as the name column: it sets its own <c>text-align: left</c> and indents by the tile padding, avatar and gap, so nothing inherited can centre it.</summary>
    [Fact]
    public void Stylesheet_ContextLineIsLeftAlignedOnTheNameEdge()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int start = css.IndexOf(".agent-tile-context {", StringComparison.Ordinal);
        int end = css.IndexOf('}', start);
        string context = css[start..end];

        Assert.Contains("text-align: left;", context, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
        Assert.Contains("padding: 0 12px 12px calc(12px + 40px + 16px);", context, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source, not markup
    }

    /// <summary>Build a team's roster card is the shared <c>ProfileCard</c>, and that card draws its avatar through <c>AgentAvatar</c>, not a bare <c>MudAvatar</c>.</summary>
    [Fact]
    public void BuildTeam_Source_UsesAgentAvatar()
    {
        string buildTeam = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "BuildTeam.razor"));
        string card = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "ProfileCard.razor"));

        Assert.Contains("<ProfileCard ", buildTeam, StringComparison.Ordinal); // contains-ok: haystack is the razor source file, which a render cannot distinguish
        Assert.DoesNotContain("<MudAvatar", buildTeam, StringComparison.Ordinal);
        Assert.Contains("<AgentAvatar ", card, StringComparison.Ordinal); // contains-ok: haystack is the razor source file, which a render cannot distinguish
        Assert.DoesNotContain("<MudAvatar", card, StringComparison.Ordinal);
    }

    /// <summary>The create action is the page header's Small filled button at the right, not a text button at the toolbar's left (B1, A7).</summary>
    [Fact]
    public async Task Page_NewAgent_IsTheHeadersCreateButton()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        var create = cut.Find(".page-header-actions .page-header-create");
        Assert.Equal("New agent", create.TextContent.Trim());
        Assert.Contains("mud-button-filled", create.ClassList);
        Assert.Empty(cut.FindAll(".mud-toolbar .page-header-create"));
        Assert.Empty(cut.FindAll(".mud-toolbar button"));
    }

    /// <summary>The Team select in the toolbar wears the compact 28px class (A7).</summary>
    [Fact]
    public async Task Page_TeamFilter_IsTheCompactToolbarField()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, Xunit.TestContext.Current.CancellationToken);

        IRenderedComponent<AgentsPage> cut = ctx.Render<AgentsPage>();

        Assert.NotEmpty(cut.FindAll(".mud-toolbar .mud-input-control.toolbar-compact.agents-filter"));
    }

    private static async Task<MudBunitContext> NewContextAsync(TempDataDir dataDir, CancellationToken ct)
    {
        Agency.Huddle.Tests.Acp.TestPersonaFiles.Write(
            new AgentPaths(dataDir.Options()), "coo", "---\nName: coo\nTitle: coo\nAlias: coo\n---\nYou are the Chief of Staff.");

        SqliteTeamDirectory directory = new(dataDir.Options());
        await directory.InitializeAsync("You", ct);
        _ = await directory.UpsertAgentUserAsync("coo", null, ct);

        PersonaHealth health = new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        PersonaStore personas = new(
            new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
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
