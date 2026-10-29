using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="PageHeader"/> on its own. Every routable page opens with this header (design-system "A page",
/// UX-001, UX-004): the title as the page's h1 at <c>Typo.h5</c>, the page's actions on the right, and the same text
/// as the document title.
/// </summary>
public sealed class PageHeaderTests
{
    /// <summary>The title renders as the page's h1 at Typo.h5, with the text verbatim.</summary>
    [Fact]
    public async Task Title_RendersAsH1AtH5()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Tasks"));

        var heading = cut.Find("h1");
        Assert.Equal("Tasks", heading.TextContent);
        Assert.Contains("mud-typography-h5", heading.ClassList);
    }

    /// <summary>The actions sit in the same row as the title, so the title and its actions share one line.</summary>
    [Fact]
    public async Task Actions_RenderInTheTitleRow()
    {
        await using MudBunitContext ctx = new();
        RenderFragment actions = builder => builder.AddMarkupContent(0, "<button class=\"page-action\">Save</button>");

        var cut = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Settings")
            .Add(p => p.Actions, actions));

        var row = cut.Find("h1").Closest(".page-header-row");
        Assert.NotNull(row);
        Assert.NotNull(row.QuerySelector("button.page-action"));
    }

    /// <summary>The header is one row with the title cluster on the left and the actions on the right; its 32px height and 16px margin live in <c>.page-header-row</c> and <c>.page-header</c>.</summary>
    [Fact]
    public async Task Row_HoldsTheTitleClusterAndTheActions()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Library"));

        var row = cut.Find(".page-header > .page-header-row");
        Assert.NotNull(row.QuerySelector(".page-header-title > h1"));
        Assert.NotNull(row.QuerySelector(".page-header-actions"));
        Assert.Empty(cut.FindAll(".mb-4"));
    }

    /// <summary>A page that sets its own document title turns the header's PageTitle off (UX-001); every other page uses the bare title.</summary>
    [Fact]
    public async Task SetPageTitleFalse_RendersNoPageTitle()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Business")
            .Add(p => p.SetPageTitle, false));

        Assert.Empty(cut.FindComponents<PageTitle>());
        Assert.Equal("Business", cut.Find("h1").TextContent);
    }

    /// <summary>There is no way to hide the title: every page shows its h1, and the parameter is gone.</summary>
    [Fact]
    public void HideTitle_IsRemoved()
    {
        Assert.Null(typeof(PageHeader).GetProperty("HideTitle"));
    }

    /// <summary>A Subtitle is the intro under the row, drawn at <c>body1</c> in the <c>page-header-subtitle</c> class (Quiet ink, 75ch).</summary>
    [Fact]
    public async Task Subtitle_IsBody1UnderTheRow()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Agents")
            .Add(p => p.Subtitle, "Your people, one place."));

        var intro = cut.Find(".page-header > .page-header-subtitle");
        Assert.Equal("Your people, one place.", intro.TextContent.Trim());
        Assert.Contains("mud-typography-body1", intro.ClassList);
    }

    /// <summary>The create action is a Small button at the right: filled and Primary by default, outlined when it is not the page's one primary action, and it raises <c>OnCreate</c>.</summary>
    [Fact]
    public async Task CreateAction_IsASmallButtonAtTheRight()
    {
        await using MudBunitContext ctx = new();
        int created = 0;

        var primary = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Agents")
            .Add(p => p.CreateText, "New agent")
            .Add(p => p.OnCreate, EventCallback.Factory.Create<MouseEventArgs>(ctx, () => created++)));
        var quiet = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Agents")
            .Add(p => p.CreateText, "New agent")
            .Add(p => p.CreatePrimary, false));

        var button = primary.Find(".page-header-actions button.page-header-create");
        Assert.Equal("New agent", button.TextContent.Trim());
        Assert.Contains("mud-button-filled-primary", button.ClassList);
        Assert.Contains("mud-button-filled-size-small", button.ClassList);
        await primary.InvokeAsync(() => button.ClickAsync());
        Assert.Equal(1, created);
        var outlined = quiet.Find(".page-header-actions button.page-header-create");
        Assert.Contains("mud-button-outlined", outlined.ClassList);
        Assert.DoesNotContain("mud-button-filled", outlined.ClassList);
    }

    /// <summary>With no <c>CreateText</c> there is no create button, so a page without a create action draws none.</summary>
    [Fact]
    public async Task NoCreateText_DrawsNoCreateButton()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Settings"));

        Assert.Empty(cut.FindAll("button"));
    }

    /// <summary>The Room's header: a <c>TitleEditor</c> replaces the h1 while it is set, <c>TitleTrailing</c> sits on the title's line, <c>Aside</c> sits at the right before the create button, and <c>Chrome</c> adds the Chrome line.</summary>
    [Fact]
    public async Task RoomShape_EditorTrailingAsideAndChrome()
    {
        await using MudBunitContext ctx = new();
        RenderFragment editor = builder => builder.AddMarkupContent(0, "<input class=\"title-edit\" />");
        RenderFragment trailing = builder => builder.AddMarkupContent(0, "<button class=\"rename\">Rename</button>");
        RenderFragment aside = builder => builder.AddMarkupContent(0, "<span class=\"members\">Members</span>");

        var cut = ctx.Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Planning")
            .Add(p => p.TitleEditor, editor)
            .Add(p => p.TitleTrailing, trailing)
            .Add(p => p.Aside, aside)
            .Add(p => p.CreateText, "Invite")
            .Add(p => p.Chrome, true));

        Assert.Empty(cut.FindAll("h1"));
        Assert.NotNull(cut.Find(".page-header-title input.title-edit"));
        Assert.NotNull(cut.Find(".page-header-title button.rename"));
        var actions = cut.Find(".page-header-actions");
        Assert.True(actions.FirstElementChild?.ClassList.Contains("members"));
        Assert.NotNull(actions.QuerySelector("button.page-header-create"));
        Assert.Contains("has-chrome", cut.Find(".page-header").ClassList);
        Assert.NotNull(cut.FindComponent<PageTitle>());
    }

    /// <summary>Without <c>Chrome</c> the header draws no Chrome line.</summary>
    [Fact]
    public async Task Header_DrawsNoChromeLineByDefault()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Tasks"));

        Assert.DoesNotContain("has-chrome", cut.Find(".page-header").ClassList);
    }

    /// <summary>The same text is set as the document title through PageTitle.</summary>
    [Fact]
    public async Task Title_AlsoSetsThePageTitle()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Agents"));

        // PageTitle writes into the document head, not into the component's own markup, so bUnit reads the
        // component itself; the rendered <title> text is pinned by the integration GET in TeamPageRouteTests.
        Assert.NotNull(cut.FindComponent<PageTitle>());
    }
}
