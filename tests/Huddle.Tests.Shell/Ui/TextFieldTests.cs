using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one text field (ComponentCatalog, TextField): always Outlined and Dense, its shape sets the rows and the look class
/// (<c>Q-B3-1</c>: long text is 4 rows), a field with no name cannot render (D5), and two-way binding still works.
/// </summary>
public sealed class TextFieldTests
{
    /// <summary>Each shape draws its look: one line, the toolbar class, or a 4-row textarea with the long-text classes.</summary>
    /// <param name="shape">The shape the caller asks for.</param>
    /// <param name="classes">The field's own classes (MudBlazor's aside), in order: the shape's, then the caller's hook.</param>
    /// <param name="rows">The textarea's rows, or 0 for a one-line input.</param>
    [Theory]
    [InlineData(TextFieldShape.Line, "hook", 0)]
    [InlineData(TextFieldShape.Compact, "toolbar-compact hook", 0)]
    [InlineData(TextFieldShape.LongText, "long-text-field hook", 4)]
    [InlineData(TextFieldShape.Markdown, "long-text-field markdown-field hook", 4)]
    public async Task Render_EachShape_DrawsItsLook(TextFieldShape shape, string classes, int rows)
    {
        ArgumentNullException.ThrowIfNull(classes);
        await using MudBunitContext ctx = new();

        IRenderedComponent<TextField> cut = ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.Label, "Name")
            .Add(p => p.Shape, shape)
            .Add(static p => p.Class, "hook"));

        IElement control = cut.Find(".mud-input-control");
        Assert.Equal(classes, string.Join(' ', control.ClassList.Where(static c => !c.StartsWith("mud-", StringComparison.Ordinal))));
        Assert.NotNull(cut.Find(".mud-input-outlined"));
        Assert.NotNull(cut.Find(".mud-input-control-margin-dense"));

        if (rows == 0)
        {
            Assert.Empty(cut.FindAll("textarea"));
        }
        else
        {
            Assert.Equal(rows.ToString(System.Globalization.CultureInfo.InvariantCulture), cut.Find("textarea").GetAttribute("rows"));
        }
    }

    /// <summary>A long field's content-driven <c>Rows</c> replaces the house 4 rows.</summary>
    [Fact]
    public async Task Render_LongTextWithRows_UsesThem()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<TextField> cut = ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.AriaLabel, "Their Description")
            .Add(static p => p.Shape, TextFieldShape.LongText)
            .Add(static p => p.Rows, 6));

        Assert.Equal("6", cut.Find("textarea").GetAttribute("rows"));
    }

    /// <summary>A field with neither a Label nor an accessible name fails its first render, so a nameless field cannot ship.</summary>
    [Fact]
    public async Task Render_NoLabelAndNoAriaLabel_Throws()
    {
        await using MudBunitContext ctx = new();

        Assert.Throws<InvalidOperationException>(() => ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.Placeholder, "Name")));
    }

    /// <summary>An AriaLabel, or an <c>aria-label</c> passed through, names the input; other attributes reach it too.</summary>
    [Fact]
    public async Task Render_AriaLabelAndAttributes_ReachTheInput()
    {
        await using MudBunitContext ctx = new();

        IElement input = ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.AriaLabel, "Room name")
            .AddUnmatched("autocomplete", "off")).Find("input");
        IElement passed = ctx.Render<TextField>(parameters => parameters
            .AddUnmatched("aria-label", "Search")).Find("input");

        Assert.Equal("Room name", input.GetAttribute("aria-label"));
        Assert.Equal("off", input.GetAttribute("autocomplete"));
        Assert.Equal("Search", passed.GetAttribute("aria-label"));
    }

    /// <summary>Typing into an Immediate field raises ValueChanged with the text, so <c>@bind-Value</c> keeps working.</summary>
    [Fact]
    public async Task Input_Immediate_RaisesValueChanged()
    {
        await using MudBunitContext ctx = new();
        string? received = null;

        IRenderedComponent<TextField> cut = ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.Label, "Name")
            .Add(static p => p.Immediate, true)
            .Add(p => p.ValueChanged, text => received = text));
        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() => Assert.Equal("ada", received));
    }

    /// <summary>An end icon with a click handler is a named button that raises <c>OnEndIconClick</c>.</summary>
    [Fact]
    public async Task Click_EndIcon_RaisesOnEndIconClick()
    {
        await using MudBunitContext ctx = new();
        int clicks = 0;

        IRenderedComponent<TextField> cut = ctx.Render<TextField>(parameters => parameters
            .Add(static p => p.Label, "Folder")
            .Add(static p => p.EndIcon, Icons.Material.Outlined.Folder)
            .Add(static p => p.EndIconLabel, "Browse for library folder")
            .Add(p => p.OnEndIconClick, () => clicks++));
        cut.Find(".mud-input-adornment-end button").Click();

        Assert.Equal(1, clicks);
        Assert.Equal("Browse for library folder", cut.Find(".mud-input-adornment-end button").GetAttribute("aria-label"));
    }
}
