using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestContext = Xunit.TestContext;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins the round-3 Right sidebar rows (audit B11a-B11h): ink rows, row heights, presence on the avatar, the current Outline heading and the Edge line between panels.</summary>
public sealed class R3RightSidebarTests
{
    private static readonly LibraryPath NotePath = new(
        new LibraryRoot("teams", "Teams", "C:\\teams", LibraryRootKind.Teams),
        "note.md",
        "C:\\teams\\note.md",
        LibraryNodeRole.File);

    /// <summary>A panel row is a 32px line, and an Outline row is 28px (B11b).</summary>
    [Fact]
    public void AppCss_Rows_Are32pxAndTheOutlineIs28px()
    {
        Assert.Contains("min-height: 32px", CssText.RuleBody(CssText.AppCss(), ".sidebar-row"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("min-height: 28px", CssText.RuleBody(CssText.AppCss(), ".sidebar-row.outline-row"), StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A row's text is one ellipsised line in Ink; Accent ink is only for hover, focus and the current heading (B11a, B11b).</summary>
    [Fact]
    public void AppCss_RowText_IsInkOnOneLineWithAccentInkOnHover()
    {
        string text = CssText.RuleBody(CssText.AppCss(), ".sidebar-row-text");
        Assert.Contains("text-overflow: ellipsis", text, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("white-space: nowrap", text, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("color: var(--mud-palette-text-primary)", text, StringComparison.Ordinal); // contains-ok: stylesheet source text

        string accent = CssText.RuleBody(CssText.AppCss(), ".sidebar-row .mud-link.sidebar-row-text:hover,\n.sidebar-row .mud-link.sidebar-row-text:focus-visible,\n.sidebar-row .mud-link.sidebar-row-text.is-current");
        Assert.Contains("var(--hu-accent-ink)", accent, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>The panel header is 32px with a 13px/600 title, and the count is 12px Quiet ink (B11e).</summary>
    [Fact]
    public void AppCss_PanelHeader_IsThirtyTwoPixelsWithA12pxQuietCount()
    {
        string header = CssText.RuleBody(CssText.AppCss(), ".sidebar-panel-header");
        Assert.Contains("min-height: 32px", header, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-size: var(--mud-typography-body2-size)", header, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-weight: 600", header, StringComparison.Ordinal); // contains-ok: stylesheet source text

        string count = CssText.RuleBody(CssText.AppCss(), ".sidebar-panel-count");
        Assert.Contains("font-size: var(--mud-typography-caption-size)", count, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("color: var(--mud-palette-text-secondary)", count, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>Panels are separated by an Edge line, with no gap between them (B11f).</summary>
    [Fact]
    public void AppCss_Panels_AreSeparatedByAnEdgeLine()
    {
        Assert.Contains("border-top: 1px solid var(--ink-line)", CssText.RuleBody(CssText.AppCss(), ".sidebar-panel + .sidebar-panel"), StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("gap: 0", CssText.RuleBody(CssText.AppCss(), ".right-sidebar .mud-drawer-content"), StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>The current Outline heading wears the Selected treatment and is the only one marked; clicking another heading moves it (B11g).</summary>
    [Fact]
    public async Task Outline_ClickedHeading_WearsTheSelectedTreatment()
    {
        await using MudBunitContext ctx = new();
        SidebarPanelContext panelCtx = new();
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<OutlinePanel>(s => s
                .Add(c => c.Subject, new DocumentSubject(NotePath, LibraryFileKind.Markdown, "## First\n\n## Second\n"))
                .Add(c => c.TabId, "t1")));
        Assert.Empty(cut.FindAll(".selected-shape"));

        await cut.FindAll(".outline-item")[1].ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".outline-row.selected-shape")));
        Assert.Equal("Second", cut.Find(".selected-shape .outline-item").TextContent.Trim());
        Assert.Equal("location", cut.Find(".selected-shape .outline-item").GetAttribute("aria-current"));
        Assert.Equal("First", cut.FindAll(".outline-item")[0].GetAttribute("title"));
    }

    /// <summary>An Agent's presence badge sits on its avatar, before the name; a Human has none (B11d).</summary>
    [Fact]
    public async Task Members_PresenceBadge_SitsOnTheAvatarBeforeTheName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        User human = await fixture.Directory.GetHumanAsync(ct);
        User agent = await fixture.AddAgentAsync("Ada", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Room", [human.Id, agent.Id], "Alpha", false, ct);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, new SidebarPanelContext())
            .Add(c => c.IsFixed, true)
            .AddChildContent<MembersPanel>(s => s.Add(c => c.Subject, new RoomSubject(room.Id)).Add(c => c.TabId, "t1")));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".members-row").Count));
        Assert.Single(cut.FindAll(".members-row .presence-anchor > .agent-dot.on-avatar"));
        Assert.Empty(cut.FindAll(".members-row .members-name ~ .agent-dot"));
        Assert.Equal("presence-anchor", cut.FindAll(".members-row")[0].FirstElementChild?.ClassName);
    }
}
