using Bunit;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The browser switch as the real app composes it (#551): the Agent card in View mode shows the Browser row P6b, and the same card
/// in Edit mode does not.
/// </summary>
public sealed class AgentBrowserPageTests
{
    private const string Row = ".agent-browser-row";

    /// <summary>The View card shows the Browser row, directly after the Account connectors row.</summary>
    [Fact]
    public async Task ViewMode_ShowsTheBrowserRow_AfterTheConnectorsRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Row)));
        Assert.Single(cut.FindAll(".agent-connectors-row + .agent-browser-row"));
    }

    /// <summary>The same card in Edit mode does not show the Browser row.</summary>
    [Fact]
    public async Task EditMode_HasNoBrowserRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(Row)));
        await ClickButtonAsync(cut, "Edit");

        cut.WaitForAssertion(() => Assert.False(HasButton(cut, "Edit")));
        Assert.Empty(cut.FindAll(Row));
    }
}
