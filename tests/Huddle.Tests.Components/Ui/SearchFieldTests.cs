using Bunit;
using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="SearchField"/> on its own. It is the one search field every surface uses (design-system
/// "House defaults"; UX-055): Outlined and Dense, a start search adornment, Clearable, Immediate, one debounce
/// constant and an accessible name.
/// </summary>
public sealed class SearchFieldTests
{
    /// <summary>The input carries the placeholder and the accessible name the caller passes.</summary>
    [Fact]
    public async Task Input_CarriesPlaceholderAndAriaLabel()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<SearchField>(parameters => parameters
            .Add(p => p.Placeholder, "Search members")
            .Add(p => p.AriaLabel, "Search members"));

        var input = cut.Find("input");
        Assert.Equal("Search members", input.GetAttribute("placeholder"));
        Assert.Equal("Search members", input.GetAttribute("aria-label"));
    }

    /// <summary>The field is Outlined and shows a search icon in its start adornment.</summary>
    [Fact]
    public async Task Renders_OutlinedWithStartSearchIcon()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<SearchField>(parameters => parameters.Add(p => p.Placeholder, "Search members"));

        Assert.NotNull(cut.Find(".mud-input-outlined"));
        Assert.NotNull(cut.Find(".mud-input-adornment-start svg"));
    }

    /// <summary>The field is Clearable: once it holds text, a clear button is offered.</summary>
    [Fact]
    public async Task Value_ShowsClearButton()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<SearchField>(parameters => parameters
            .Add(p => p.Placeholder, "Search members")
            .Add(p => p.Value, "ada"));

        Assert.NotNull(cut.Find(".mud-input-clear-button"));
    }

    /// <summary>Typing raises ValueChanged with the typed text.</summary>
    [Fact]
    public async Task Typing_RaisesValueChangedWithTheText()
    {
        await using MudBunitContext ctx = new();
        string? received = null;

        var cut = ctx.Render<SearchField>(parameters => parameters
            .Add(p => p.Placeholder, "Search members")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => received = value)));

        cut.Find("input").Input("ada");

        cut.WaitForAssertion(() => Assert.Equal("ada", received));
    }

    /// <summary>The debounce is one shared constant of 250 ms, so every search field waits the same time (UX-055).</summary>
    [Fact]
    public void DebounceMs_IsTwoHundredFifty()
    {
        Assert.Equal(250, SearchField.DebounceMs);
    }
}
