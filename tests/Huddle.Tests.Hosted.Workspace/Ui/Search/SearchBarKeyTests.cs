using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Search;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Search;

/// <summary>
/// Pins how the search box is wired to the global key script <c>search.js</c> (Global Search spec
/// §5.2, §6.5, §11): when it attaches and detaches, what the keys the script reports do to the chips, and the
/// focus restore on Esc. The module handler bUnit sets up for <c>search.js</c> records the calls.
/// </summary>
public sealed class SearchBarKeyTests(TeamWebApplicationFactory factory) : IClassFixture<TeamWebApplicationFactory>
{
    /// <summary>The number of recorded calls of a script function.</summary>
    /// <param name="h">The harness whose context records the calls.</param>
    /// <param name="identifier">The function, such as <c>attach</c> in <c>search.js</c>.</param>
    /// <returns>The count.</returns>
    private static int Calls(SearchBarHarness h, string identifier)
    {
        return h.Ctx.Module(JsModule.Search).Invocations.Count(invocation => string.Equals(invocation.Identifier, identifier, StringComparison.Ordinal));
    }

    /// <summary>The text of the chip that is checked.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <returns>The chip's text.</returns>
    private static string Checked(IRenderedComponent<ContainerFragment> cut)
    {
        return cut.Find("[role='radio'][aria-checked='true']").TextContent.Trim();
    }

    /// <summary>The search box component inside the rendered fragment.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <returns>The component instance, whose <c>[JSInvokable]</c> methods the script calls.</returns>
    private static SearchBar Bar(IRenderedComponent<ContainerFragment> cut) => cut.FindComponent<SearchBar>().Instance;

    /// <summary>Alt+3 lights Agents, the third chip, while the dropdown is open.</summary>
    [Fact]
    public async Task SelectChip_3_LightsAgents()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        await cut.InvokeAsync(() => SearchBarKeyTests.Bar(cut).SelectChipAsync(3));

        Assert.Equal("Agents", SearchBarKeyTests.Checked(cut));
    }

    /// <summary>Ctrl+Right moves from All to the next chip, Teams.</summary>
    [Fact]
    public async Task MoveChip_Right_FromAll_LightsTeams()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        await cut.InvokeAsync(() => SearchBarKeyTests.Bar(cut).MoveChipAsync(1));

        Assert.Equal("Teams", SearchBarKeyTests.Checked(cut));
    }

    /// <summary>Ctrl+Left from All stays on All: the chips do not wrap.</summary>
    [Fact]
    public async Task MoveChip_Left_FromAll_StaysOnAll()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        await cut.InvokeAsync(() => SearchBarKeyTests.Bar(cut).MoveChipAsync(-1));

        Assert.Equal("All", SearchBarKeyTests.Checked(cut));
    }

    /// <summary>A chip key that arrives while the dropdown is closed changes nothing.</summary>
    [Fact]
    public async Task SelectChip_WhileClosed_ChangesNothing()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await cut.InvokeAsync(() => SearchBarKeyTests.Bar(cut).SelectChipAsync(3));
        await SearchBarHarness.OpenAsync(cut);

        Assert.Equal("All", SearchBarKeyTests.Checked(cut));
    }

    /// <summary>The script attaches once, on the first render, and later renders do not attach it again.</summary>
    [Fact]
    public async Task Attach_IsCalledOnFirstRenderOnly()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        int afterFirst = SearchBarKeyTests.Calls(h, "attach");

        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "abc");

        Assert.Equal(1, afterFirst);
        Assert.Equal(1, SearchBarKeyTests.Calls(h, "attach"));
    }

    /// <summary>The script is told when the dropdown opens and closes, so the chip keys exist only while it is open.</summary>
    [Fact]
    public async Task SetOpen_FollowsTheDropdown()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.PressAsync(cut, "Escape");

        object?[] flags =
        [
            .. h.Ctx.Module(JsModule.Search).Invocations
                .Where(static invocation => string.Equals(invocation.Identifier, "setOpen", StringComparison.Ordinal))
                .Select(static invocation => invocation.Arguments[0]),
        ];
        Assert.Equal(new object?[] { true, false }, flags);
    }

    /// <summary>Esc closes the dropdown and asks the script to put focus back where it was.</summary>
    [Fact]
    public async Task Escape_RestoresFocus()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        await SearchBarHarness.PressAsync(cut, "Escape");

        Assert.Equal(1, SearchBarKeyTests.Calls(h, "restoreFocus"));
    }

    /// <summary>Disposing the box removes the script's listener.</summary>
    [Fact]
    public async Task Dispose_Detaches()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await h.Ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(1, SearchBarKeyTests.Calls(h, "detach"));
        Assert.NotNull(cut);
    }
}
