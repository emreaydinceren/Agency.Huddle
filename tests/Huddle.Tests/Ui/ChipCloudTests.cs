using Agency.Huddle.App.Components.Shared;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for the shared <c>ChipCloud</c>: selected chips first and marked by brackets and <c>aria-pressed</c>, a pressed chip reported to the caller, and a capped pool behind "Show all".</summary>
public sealed class ChipCloudTests
{
    /// <summary>An empty pool renders nothing, so a cloud with no chips leaves no empty heading behind.</summary>
    [Fact]
    public async Task Render_EmptyPool_ShowsNothing()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters.Add(p => p.Title, "Tags"));

        Assert.Empty(cut.FindAll(".chip-cloud"));
        Assert.Empty(cut.FindAll(".chip-cloud-title"));
    }

    /// <summary>Selected chips come first in press order and read as pressed with brackets, so selection never depends on colour alone.</summary>
    [Fact]
    public async Task Render_SelectedChips_ComeFirstAndAreMarkedPressed()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters
            .Add(p => p.Title, "Tags")
            .Add(p => p.Pool, ["tax", "legal", "care"])
            .Add(p => p.Selected, ["care", "tax"]));

        Assert.Equal(["care", "tax", "legal"], cut.FindAll(".chip-cloud-chip-text").Select(item => item.TextContent.Trim()));
        Assert.Equal(["true", "true", "false"], cut.FindAll(".chip-cloud-chip").Select(item => item.GetAttribute("aria-pressed")));
        Assert.Equal(2, cut.FindAll(".chip-cloud-chip-selected").Count);
        Assert.Equal(4, cut.FindAll(".chip-cloud-bracket").Count);
        Assert.Equal("Tags", cut.Find(".chip-cloud-title").TextContent.Trim());
    }

    /// <summary>Pressing a chip raises the toggle callback with that chip's text.</summary>
    [Fact]
    public async Task Click_Chip_RaisesToggleWithItsText()
    {
        await using MudBunitContext ctx = new();
        List<string> toggled = [];
        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters
            .Add(p => p.Pool, ["tax", "legal"])
            .Add(p => p.OnToggle, (string text) => toggled.Add(text)));

        await cut.InvokeAsync(() => cut.FindAll(".chip-cloud-chip")[1].ClickAsync());

        Assert.Equal(["legal"], toggled);
    }

    /// <summary>A capped pool shows only its first chips until "Show all" is pressed, which then reads "Show fewer"; selected chips always show.</summary>
    [Fact]
    public async Task Render_CappedPool_ShowsMoreWhenExpanded()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ChipCloud> cut = ctx.Render<ChipCloud>(parameters => parameters
            .Add(p => p.Pool, ["a", "b", "c", "d"])
            .Add(p => p.Selected, ["d"])
            .Add(p => p.Cap, 2));

        Assert.Equal(["d", "a", "b"], cut.FindAll(".chip-cloud-chip-text").Select(item => item.TextContent.Trim()));
        Assert.Equal("Show all", cut.Find(".chip-cloud-all").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".chip-cloud-all").ClickAsync());

        Assert.Equal(["d", "a", "b", "c"], cut.FindAll(".chip-cloud-chip-text").Select(item => item.TextContent.Trim()));
        Assert.Equal("Show fewer", cut.Find(".chip-cloud-all").TextContent.Trim());
    }
}
