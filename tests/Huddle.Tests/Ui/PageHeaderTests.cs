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

        var row = cut.Find("h1").ParentElement;
        Assert.NotNull(row);
        Assert.NotNull(row.QuerySelector("button.page-action"));
    }

    /// <summary>The header row is a stack with bottom margin mb-4, as U6 T6.2 names it.</summary>
    [Fact]
    public async Task Row_HasBottomMargin()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Library"));

        var row = cut.Find("h1").ParentElement;
        Assert.NotNull(row);
        Assert.Contains("mb-4", row.ClassList);
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

    /// <summary>The same text is set as the document title through PageTitle.</summary>
    [Fact]
    public async Task Title_AlsoSetsThePageTitle()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PageHeader>(parameters => parameters.Add(p => p.Title, "Teammates"));

        // PageTitle writes into the document head, not into the component's own markup, so bUnit reads the
        // component itself; the rendered <title> text is pinned by the integration GET in TeamPageRouteTests.
        Assert.NotNull(cut.FindComponent<PageTitle>());
    }
}
