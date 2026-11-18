using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one label-and-value list (ComponentCatalog, PropertyList): a read list is a definition list of dt and dd pairs, an editing
/// list is a plain group whose labels are hidden because each input names itself, and both share the row's fixed 120 px label
/// column (Q-A6-1) and the hairline that only a Divided list draws (Q-A6-2).
/// </summary>
public sealed class PropertyListTests
{
    /// <summary>A read list is a dl; each row groups its dt label and dd value in a div, with the caller's hook classes beside the component's.</summary>
    [Fact]
    public async Task Read_Row_IsADefinitionTermAndItsValue()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PropertyList> cut = ctx.Render<PropertyList>(parameters => parameters
            .Add(static p => p.Class, "task-read-view-props")
            .AddChildContent<PropertyRow>(row => row
                .Add(static p => p.Label, "Status")
                .Add(static p => p.Class, "task-read-view-status")
                .Add(static p => p.LabelClass, "status-key")
                .Add(static p => p.ValueClass, "status-value")
                .AddChildContent("Open")));

        IElement list = cut.Find("dl.property-list.task-read-view-props");
        IElement row = Assert.Single(list.Children);
        Assert.Equal("div", row.LocalName);
        Assert.Equal("property-row task-read-view-status", row.ClassName);
        Assert.Equal("Status", row.QuerySelector("dt.property-row-label.status-key")?.TextContent);
        Assert.Equal("Open", row.QuerySelector("dd.property-row-value.status-value")?.TextContent);
    }

    /// <summary>An editing list is a div, not a dl: its label is a span hidden from assistive technology and no dt or dd is drawn.</summary>
    [Fact]
    public async Task Editing_Row_HidesItsLabelAndDrawsNoDefinitionList()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PropertyList> cut = ctx.Render<PropertyList>(parameters => parameters
            .Add(static p => p.Editing, true)
            .AddChildContent<PropertyRow>(row => row
                .Add(static p => p.Label, "Parent")
                .AddChildContent("<input aria-label=\"Parent\" />")));

        IElement list = cut.Find("div.property-list.property-list-editing");
        IElement label = list.QuerySelector(".property-row > span.property-row-label") ?? throw new InvalidOperationException("No label.");
        Assert.Equal("true", label.GetAttribute("aria-hidden"));
        Assert.Equal("Parent", label.TextContent);
        Assert.NotNull(list.QuerySelector(".property-row > div.property-row-value > input"));
        Assert.Empty(cut.FindAll("dl, dt, dd"));
    }

    /// <summary>Compact and Divided each add their own class to the list, and a list with neither carries only its own.</summary>
    [Fact]
    public async Task Options_AddTheirClasses()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<PropertyList> plain = ctx.Render<PropertyList>();
        IRenderedComponent<PropertyList> both = ctx.Render<PropertyList>(parameters => parameters
            .Add(static p => p.Compact, true)
            .Add(static p => p.Divided, true));

        Assert.Equal("property-list", plain.Find("dl").ClassName);
        Assert.Equal("property-list property-list-compact property-list-divided", both.Find("dl").ClassName);
    }

    /// <summary>
    /// The shared rule: every row has the fixed 120 px label column (Q-A6-1), a default row is 32 px and a Compact one 28 px, and
    /// only a Divided list draws the hairline between rows (Q-A6-2).
    /// </summary>
    [Fact]
    public void Stylesheet_FixesTheLabelColumnAndTheRowHeights()
    {
        string css = CssText.AppCss();

        Assert.Contains("grid-template-columns: 120px minmax(0, 1fr);", CssText.RuleBody(css, ".property-row"), StringComparison.Ordinal);
        Assert.Contains("min-height: 32px;", CssText.RuleBody(css, ".property-row-label,\n.property-row-value"), StringComparison.Ordinal);
        Assert.Contains("min-height: 28px;", CssText.RuleBody(css, ".property-list-compact .property-row-label,\n.property-list-compact .property-row-value"), StringComparison.Ordinal);
        Assert.Contains("border-top: 1px solid", CssText.RuleBody(css, ".property-list-divided > .property-row + .property-row"), StringComparison.Ordinal);
    }
}
