namespace Agency.Huddle.Tests.Ui.Teams;

using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Components.Teams;

/// <summary>
/// Pins Spec §6.6 (Layout) for <c>TeamTabToolbar</c>, the action button and search field every Team
/// page tab shares: the action text and its single click, the search field's placeholder, accessible
/// name, icon, clear button and debounce, and that a blank value is reported as <c>null</c>.
/// </summary>
public sealed class TeamTabToolbarTests
{
    private const string ActionText = "Add member";
    private const string Placeholder = "Search members";
    private const string ActionClass = ".team-tab-toolbar-action";
    private const string SearchClass = "team-tab-toolbar-search";

    /// <summary>The action button reads the <c>ActionText</c> parameter, is the primary text button with the Add icon, and a click raises <c>OnAction</c> exactly once.</summary>
    [Fact]
    public async Task Renders_ActionButtonWithText()
    {
        await using MudBunitContext ctx = new();
        int actions = 0;
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx, onAction: EventCallback.Factory.Create(this, () => actions++));

        Assert.Equal(ActionText, cut.Find(ActionClass).TextContent.Trim());
        MudButton button = cut.FindComponent<MudButton>().Instance;
        Assert.Equal(Variant.Text, button.Variant);
        Assert.Equal(Color.Primary, button.Color);
        Assert.Equal(Icons.Material.Filled.Add, button.StartIcon);

        await cut.InvokeAsync(() => cut.Find(ActionClass).Click());

        Assert.Equal(1, actions);
    }

    /// <summary>The search field carries the placeholder on the input, the same text as its accessible name, a leading search icon, a clear button and immediate updates.</summary>
    [Fact]
    public async Task Renders_SearchField_WithPlaceholderAndAriaLabel()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx);

        MudTextField<string> field = SearchField(cut).Instance;
        Assert.Equal(Adornment.Start, field.Adornment);
        Assert.Equal(Icons.Material.Filled.Search, field.AdornmentIcon);
        Assert.True(field.Clearable);
        Assert.True(field.Immediate);
        Assert.Equal(Placeholder, cut.Find("." + SearchClass + " input").GetAttribute("placeholder"));
        Assert.Equal(Placeholder, cut.Find("." + SearchClass + " input").GetAttribute("aria-label"));
    }

    /// <summary>The field debounces at 250 ms and reports what was typed to <c>SearchChanged</c> once, without a timer wait in the test.</summary>
    [Fact]
    public async Task Search_IsDebounced_AndReported()
    {
        await using MudBunitContext ctx = new();
        List<string?> searched = [];
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx, searchChanged: EventCallback.Factory.Create<string?>(this, v => searched.Add(v)));

        IRenderedComponent<MudTextField<string>> field = SearchField(cut);
        Assert.Equal(250, field.Instance.DebounceInterval);

        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("no"));

        Assert.Equal(["no"], searched);
    }

    /// <summary>Whitespace-only text is blank, so it is reported as <c>null</c>, not as the text or an empty string.</summary>
    [Fact]
    public async Task Search_BlankValue_IsReportedAsNull()
    {
        await using MudBunitContext ctx = new();
        List<string?> searched = [];
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx, searchChanged: EventCallback.Factory.Create<string?>(this, v => searched.Add(v)));

        IRenderedComponent<MudTextField<string>> field = SearchField(cut);
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("  "));

        Assert.Equal<string?>([null], searched);
    }

    /// <summary>With text in the field its clear button shows, and clicking it reports <c>null</c>.</summary>
    [Fact]
    public async Task Clearing_InvokesSearchChangedWithNull()
    {
        await using MudBunitContext ctx = new();
        List<string?> searched = [];
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx, search: "no", searchChanged: EventCallback.Factory.Create<string?>(this, v => searched.Add(v)));

        Assert.Single(cut.FindAll(".mud-input-clear-button"));
        await cut.InvokeAsync(() => cut.Find(".mud-input-clear-button").Click());

        cut.WaitForAssertion(() => Assert.Equal<string?>([null], searched));
    }

    /// <summary>The <c>Search</c> parameter is the text shown in the field.</summary>
    [Fact]
    public async Task SearchParameter_IsShownInTheField()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<TeamTabToolbar> cut = Render(ctx, search: "no");

        Assert.Equal("no", cut.Find("." + SearchClass + " input").GetAttribute("value"));
    }

    private static IRenderedComponent<MudTextField<string>> SearchField(IRenderedComponent<TeamTabToolbar> cut)
    {
        return cut.FindComponents<MudTextField<string>>().Single(static m => string.Equals(m.Instance.Class, SearchClass, StringComparison.Ordinal));
    }

    private static IRenderedComponent<TeamTabToolbar> Render(
        MudBunitContext ctx,
        string? search = null,
        EventCallback? onAction = null,
        EventCallback<string?>? searchChanged = null)
    {
        return ctx.Render<TeamTabToolbar>(parameters =>
        {
            parameters.Add(t => t.ActionText, ActionText);
            parameters.Add(t => t.SearchPlaceholder, Placeholder);
            parameters.Add(t => t.Search, search);
            if (onAction is { } action)
            {
                parameters.Add(t => t.OnAction, action);
            }

            if (searchChanged is { } changed)
            {
                parameters.Add(t => t.SearchChanged, changed);
            }
        });
    }
}
