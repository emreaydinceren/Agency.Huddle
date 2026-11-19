using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one select (ComponentCatalog, SelectField): always Outlined and Dense, <c>Compact</c> is the toolbar box, and a select with no
/// name cannot render (D5).
/// </summary>
public sealed class SelectFieldTests
{
    /// <summary>Every select is outlined and dense; Compact adds the toolbar class beside the caller's hook.</summary>
    /// <param name="compact">Whether the select is in a toolbar.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_AnySelect_IsOutlinedDenseAndCompactWhenAsked(bool compact)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SelectField<string>> cut = ctx.Render<SelectField<string>>(parameters => parameters
            .Add(static p => p.Label, "Team")
            .Add(p => p.Compact, compact)
            .Add(static p => p.Class, "hook")
            .Add(static p => p.ChildContent, Items));

        IElement control = cut.Find(".mud-input-control");
        Assert.Contains("hook", control.ClassList);
        Assert.Equal(compact, control.ClassList.Contains("toolbar-compact"));
        Assert.NotNull(cut.Find(".mud-input-outlined"));
        Assert.NotNull(cut.Find(".mud-input-control-margin-dense"));
    }

    /// <summary>A toolbar select with no visible label is named by its AriaLabel.</summary>
    [Fact]
    public async Task Render_AriaLabel_NamesTheInput()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SelectField<string>> cut = ctx.Render<SelectField<string>>(parameters => parameters
            .Add(static p => p.AriaLabel, "Team")
            .Add(static p => p.Compact, true)
            .Add(static p => p.ChildContent, Items));

        Assert.Equal(["Team"], cut.FindAll("[aria-label]").Select(static e => e.GetAttribute("aria-label")));
    }

    /// <summary>A select with neither a Label nor an accessible name fails its first render.</summary>
    [Fact]
    public async Task Render_NoLabelAndNoAriaLabel_Throws()
    {
        await using MudBunitContext ctx = new();

        Assert.Throws<InvalidOperationException>(() => ctx.Render<SelectField<string>>(parameters => parameters
            .Add(static p => p.ChildContent, Items)));
    }

    /// <summary>A multi-select renders the selected values it is given, so the filter selects keep their state.</summary>
    [Fact]
    public async Task Render_MultiSelection_ShowsTheSelectedValues()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SelectField<string>> cut = ctx.Render<SelectField<string>>(parameters => parameters
            .Add(static p => p.Label, "Team")
            .Add(static p => p.MultiSelection, true)
            .Add(static p => p.SelectedValues, (IReadOnlyCollection<string>)["Platform", "Docs"])
            .Add(static p => p.ChildContent, Items));

        MudSelect<string> select = cut.FindComponent<MudSelect<string>>().Instance;
        Assert.True(select.MultiSelection);
        IReadOnlyCollection<string?>? selected = select.GetState(static x => x.SelectedValues);
        Assert.NotNull(selected);
        Assert.Equal(["Docs", "Platform"], selected.Order(StringComparer.Ordinal));
    }

    private static readonly RenderFragment Items = static builder =>
    {
        foreach (string team in new[] { "Platform", "Docs" })
        {
            builder.OpenComponent<MudSelectItem<string>>(0);
            builder.AddAttribute(1, nameof(MudSelectItem<string>.Value), team);
            builder.CloseComponent();
        }
    };
}
