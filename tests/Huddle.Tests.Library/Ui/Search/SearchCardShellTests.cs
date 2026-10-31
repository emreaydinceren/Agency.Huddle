namespace Agency.Huddle.Tests.Ui.Search;

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Components.Search;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Pins U26 row UX-R27: <see cref="SearchCardShell"/> lays a search card out from Icon, Title, Meta, Snippet
/// and Trailing slots, and the Task card gets the Outlined TaskAlt icon so it lines up with the other four.
/// </summary>
public sealed class SearchCardShellTests
{
    /// <summary>Every slot renders in its own element, in the order icon, text (title, meta, snippet), action.</summary>
    [Fact]
    public async Task Shell_RendersEverySlot()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchCardShell> cut = ctx.Render<SearchCardShell>(p => p
            .Add(c => c.AccessibleName, "Nova, platform")
            .Add(c => c.Icon, (RenderFragment)(b => b.AddContent(0, "I")))
            .Add(c => c.Title, (RenderFragment)(b => b.AddContent(0, "T")))
            .Add(c => c.Meta, (RenderFragment)(b => b.AddContent(0, "M")))
            .Add(c => c.Snippet, (RenderFragment)(b => b.AddContent(0, "S")))
            .Add(c => c.Trailing, (RenderFragment)(b => b.AddContent(0, "R"))));

        IElement root = cut.Find(".search-card");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Equal("Nova, platform", root.GetAttribute("aria-label"));
        Assert.Equal("I", cut.Find(".search-card-icon").TextContent);
        Assert.Equal("T", cut.Find(".search-card-text .search-card-title").TextContent);
        Assert.Equal("M", cut.Find(".search-card-text .search-card-meta").TextContent);
        Assert.Equal("S", cut.Find(".search-card-text .search-card-snippet").TextContent);
        Assert.Equal("R", cut.Find(".search-card-action").TextContent);
        Assert.Equal(["search-card-icon", "search-card-text", "search-card-action"], root.Children.Select(static c => c.ClassList[0]));
    }

    /// <summary>Without a Trailing slot the action slot is still there, empty and hidden from assistive tech, so every card keeps its width (D-12).</summary>
    [Fact]
    public async Task Shell_NoTrailing_KeepsTheEmptyHiddenActionSlot()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchCardShell> cut = ctx.Render<SearchCardShell>(p => p
            .Add(c => c.Title, (RenderFragment)(b => b.AddContent(0, "T"))));

        IElement slot = cut.Find(".search-card-action");
        Assert.Equal(string.Empty, slot.TextContent);
        Assert.Equal("true", slot.GetAttribute("aria-hidden"));
        Assert.Empty(cut.FindAll(".search-card-meta, .search-card-snippet"));
    }

    /// <summary>An archived card carries <c>search-card-archived</c>; a live one does not.</summary>
    [Fact]
    public async Task Shell_Archived_DimsTheCard()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SearchCardShell> archived = ctx.Render<SearchCardShell>(p => p.Add(c => c.Archived, true));
        IRenderedComponent<SearchCardShell> live = ctx.Render<SearchCardShell>(p => p.Add(c => c.Archived, false));

        Assert.Single(archived.FindAll(".search-card.search-card-archived"));
        Assert.Empty(live.FindAll(".search-card-archived"));
    }

    /// <summary>A Task hit shows the Outlined TaskAlt icon in the same leading icon slot the other cards use.</summary>
    [Fact]
    public async Task TaskCard_HasTheTaskAltIconInTheIconSlot()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.InProgress, "Nova", "Platform", false, MatchTier.ExactName, "Staff Engineer");

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        IElement icon = cut.Find(".search-card-icon");
        IRenderedComponent<MudIcon> expected = ctx.Render<MudIcon>(p => p.Add(c => c.Icon, Icons.Material.Outlined.TaskAlt).Add(c => c.Size, Size.Small));
        Assert.Equal(expected.Find("svg").InnerHtml, icon.QuerySelector("svg")?.InnerHtml);
        Assert.Equal("search-card-icon", cut.Find(".search-card").Children[0].ClassList[0]);
    }
}
