using Bunit;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Opening the Create card, through the test page that stands in for whatever surface opens it: it renders without throwing,
/// and it is the moment the model catalog is probed (never a page load).
/// </summary>
public sealed class AgentCreateCardTests
{
    /// <summary>
    /// Reproduces Gitea issue #10 (which also closes #11): opening the Create card must not crash. A fresh Create card has no
    /// Persona yet, and the status lookup must not ask <c>PersonaHealth.Get</c> about that blank name. bUnit dispatches a
    /// real click on the rendered "New agent" button, so this fails the way the reported crash did if the guard regresses.
    /// </summary>
    [Fact]
    public async Task OpeningTheCreateCard_RendersWithoutThrowing()
    {
        await using TeamWebApplicationFactory factory = new();
        Directory.CreateDirectory(factory.AgentsDirPath);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Bunit.Rendering.ContainerFragment> cut = RenderPage(ctx, factory);

        await ClickButtonAsync(cut, "New agent");

        // The dialog provider paints after the click handler returns, so wait for the card instead of reading at once.
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal("New agent", cut.Find(".mud-dialog-title h6").TextContent.Trim());
                Assert.True(HasButton(cut, "Add agent"));
            },
            TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Pins rules.md's model-catalog rule: the catalog is read by spawning an adapter even when Acp:Enabled is false, and what
    /// keeps it honest is when it runs - only when a New/Edit form opens. <see cref="TeamWebApplicationFactory"/> pins
    /// <c>Team:Acp:Enabled=false</c>, so this proves the probe fires here regardless.
    /// </summary>
    [Fact]
    public async Task OpeningTheCreateCard_ProbesForModelsEvenWithAcpDisabled()
    {
        await using TeamWebApplicationFactory factory = new();
        Directory.CreateDirectory(factory.AgentsDirPath);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Bunit.Rendering.ContainerFragment> cut = RenderPage(ctx, factory);
        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);

        await ClickButtonAsync(cut, "New agent");

        // The card probes when it initialises inside the dialog, which paints after the click handler returns.
        cut.WaitForAssertion(() => Assert.Equal(1, factory.FakeModelCatalog.ProbeCount), TimeSpan.FromSeconds(10));
    }
}
