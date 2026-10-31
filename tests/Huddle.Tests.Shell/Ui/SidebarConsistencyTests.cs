using System.Text.RegularExpressions;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Css;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// U9 (UX-015, UX-016, OP-5): the sidebar's Room rows tell a 1:1 Room from a group Room by their leading
/// visual (a small <c>AgentAvatar</c> versus the <c>Filled.Chat</c> icon), and "Left menu" in
/// <c>app.css</c> sets each nesting level's indent once, as a named rem token, instead of repeating literals.
/// </summary>
public sealed class SidebarConsistencyTests
{
    /// <summary>The row element each Room link sits in; the "Chats" group header is outside it.</summary>
    private const string RowSelector = "div.hover-reveal-row";

    /// <summary>A 1:1 Room (one Agent) shows a small Agent avatar in its row and not the chat icon (UX-016, OP-5).</summary>
    [Fact]
    public async Task RoomRow_OneToOneRoom_ShowsSmallAgentAvatarNotChatIcon()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = SidebarConsistencyTests.NewContext(directory, chat, events, dir);
        var cut = SidebarConsistencyTests.RenderRoomList(ctx);

        var row = cut.Find(SidebarConsistencyTests.RowSelector);
        Assert.Single(row.QuerySelectorAll(".mud-avatar.mud-avatar-small"));
        Assert.Empty(row.QuerySelectorAll("svg.mud-nav-link-icon"));
    }

    /// <summary>A group Room (two or more Agents) shows the chat icon in its row and no avatar (UX-016, OP-5).</summary>
    [Fact]
    public async Task RoomRow_GroupRoom_ShowsChatIconNotAvatar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? first = await directory.UpsertAgentUserAsync("coo", null, ct);
        User? second = await directory.UpsertAgentUserAsync("cfo", null, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        ChatService chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        _ = await chat.CreateRoomForAsync([first.Id, second.Id], ct);

        await using MudBunitContext ctx = SidebarConsistencyTests.NewContext(directory, chat, events, dir);
        var cut = SidebarConsistencyTests.RenderRoomList(ctx);

        var row = cut.Find(SidebarConsistencyTests.RowSelector);
        Assert.Single(row.QuerySelectorAll("svg.mud-nav-link-icon"));
        Assert.Empty(row.QuerySelectorAll(".mud-avatar"));
    }

    /// <summary>"Left menu" declares each token of the sidebar grid exactly once, in px on the spacing scale (UX-015).</summary>
    [Theory]
    [InlineData("--nav-inset", "4px")]
    [InlineData("--nav-pad", "4px")]
    [InlineData("--nav-slot", "24px")]
    [InlineData("--nav-gap", "4px")]
    [InlineData("--nav-step", "calc(var(--nav-slot) + var(--nav-gap))")]
    [InlineData("--nav-end", "8px")]
    public void LeftMenu_DeclaresEachGridTokenOnce(string token, string value)
    {
        string section = SidebarConsistencyTests.LeftMenuSection();

        MatchCollection declarations = Regex.Matches(section, Regex.Escape(token) + @"\s*:\s*([^;]+);");

        Match declaration = Assert.Single(declarations);
        Assert.Equal(value, declaration.Groups[1].Value.Trim());
    }

    /// <summary>
    /// Every left-sidebar link starts from the one grid: its padding is the pad plus one step per level, its mark is
    /// centred in the slot, and a label with nothing before it keeps the slot and gap empty, so every label of a level
    /// starts on one line (the sidebar alignment request).
    /// </summary>
    [Fact]
    public void LeftMenu_EveryLink_SitsOnTheGrid()
    {
        string section = SidebarConsistencyTests.LeftMenuSection();

        ShellSidebarRowsTests.SourceHas(
            CssText.RuleBody(section, ".sidebar-views.sidebar-views .mud-navmenu .mud-nav-link"),
            "padding-inline: calc(var(--nav-pad) + var(--nav-level, 0) * var(--nav-step)) var(--nav-end)");
        ShellSidebarRowsTests.SourceHas(
            CssText.RuleBody(section, ".sidebar-views .mud-nav-link > .mud-nav-link-icon"),
            "padding-inline: calc((var(--nav-slot) - 1em) / 2)");
        ShellSidebarRowsTests.SourceHas(
            CssText.RuleBody(section, ".sidebar-views .mud-nav-link > .mud-nav-link-text"),
            "margin-inline: var(--nav-step) 0");
    }

    /// <summary>
    /// A nested row moves in by setting <c>--nav-level</c>, never a padding of its own: the Teams nav's Rooms and Library
    /// folders are level 1, the rows inside them a quarter level further, and no stale indent literal or token is left (UX-015).
    /// </summary>
    [Fact]
    public void TeamsNav_FoldersAreLevelOne_AndTheirRowsAQuarterFurther()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain("3.875rem", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--nav-indent", css, StringComparison.Ordinal);
        ShellSidebarRowsTests.SourceHas(CssText.RuleBodyInList(css, ".teams-nav-host .teams-nav-folder"), "--nav-level: 1;");
        ShellSidebarRowsTests.SourceHas(CssText.RuleBodyInList(css, ".teams-nav-host .teams-nav-folder .mud-collapse-container"), "--nav-level: 1.25");
    }

    /// <summary>
    /// Every view of the left sidebar starts its first row the same distance from the top, header or not, and every row is
    /// one 32px line: the view pads 16px, its first section header drops its own top margin, only a section after a visible
    /// one gets the 16px gap back, and the grid pins the row height with no block padding.
    /// </summary>
    [Fact]
    public void LeftMenu_EveryView_StartsAtOneTop_AndEveryRowIsOneHeight()
    {
        string section = SidebarConsistencyTests.LeftMenuSection();

        ShellSidebarRowsTests.SourceHas(CssText.RuleBody(section, ".sidebar-view"), "padding-top: 16px");
        ShellSidebarRowsTests.SourceHas(CssText.RuleBody(section, ".sidebar-views .mud-nav-group > .mud-nav-link"), "margin-top: 0");
        ShellSidebarRowsTests.SourceHas(CssText.RuleBody(section, ".sidebar-view > :not([hidden]) ~ * .mud-nav-group > .mud-nav-link"), "margin-top: 16px");
        string row = CssText.RuleBody(section, ".sidebar-views.sidebar-views .mud-navmenu .mud-nav-link");
        ShellSidebarRowsTests.SourceHas(row, "min-height: var(--nav-row-height)");
        ShellSidebarRowsTests.SourceHas(row, "padding-block: 0");
    }

    /// <summary>A Team's member count ends on the trailing line, over the link, so the Team's selected pill keeps the full width.</summary>
    [Fact]
    public void LeftMenu_RowMeta_EndsOnTheTrailingLine()
    {
        string rule = CssText.RuleBody(SidebarConsistencyTests.LeftMenuSection(), ".hover-reveal-row .sidebar-row-meta");

        ShellSidebarRowsTests.SourceHas(rule, "position: absolute");
        ShellSidebarRowsTests.SourceHas(rule, "inset-inline-end: calc(var(--nav-inset) + var(--nav-end))");
    }

    /// <summary>The "Left menu" block of <c>app.css</c>: from its banner comment to the next banner comment.</summary>
    private static string LeftMenuSection()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int start = css.IndexOf("/* ---- Left menu", StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no 'Left menu' section.");
        int end = css.IndexOf("/* ---- ", start + 10, StringComparison.Ordinal);
        return end < 0 ? css[start..] : css[start..end];
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir dir)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddNeedsYouTracker();
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderRoomList(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });
}
