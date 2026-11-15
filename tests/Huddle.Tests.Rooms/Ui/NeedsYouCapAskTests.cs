using Bunit;
using Agency.Huddle.App.Components.NeedsYou;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.NeedsYou;
using Agency.Huddle.Tests.Ui.Teams;
using NeedsYouPageComponent = Agency.Huddle.App.Components.NeedsYou.NeedsYouPage;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins that a spend cap question (ADR-0087) is listed on the Needs You page and the sidebar card, answered with Yes or No, and gone once answered.</summary>
public sealed class NeedsYouCapAskTests
{
    /// <summary>The page shows the open question as a card with its two buttons and no empty state.</summary>
    [Fact]
    public async Task Page_PendingCapAsk_ShowsTheQuestionWithBothButtons()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();

        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();

        Assert.Equal(
            "Dev has spent 5 USD running inside the Team. Let it spend another 5 USD? Until you answer it asks you for each step.",
            cut.Find(".needs-you-cap-ask-text").TextContent.Trim());
        Assert.Equal("Let it spend another 5 USD", cut.Find(".needs-you-cap-grant").TextContent.Trim());
        Assert.Equal("No, ask me for each step", cut.Find(".needs-you-cap-decline").TextContent.Trim());
        Assert.Empty(cut.FindAll(".empty-state-title"));
    }

    /// <summary>Yes on the page calls Grant, and the card disappears.</summary>
    [Fact]
    public async Task Page_Yes_GrantsAndTheCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();
        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();

        await cut.InvokeAsync(() => cut.Find(".needs-you-cap-grant").ClickAsync(new()));

        Assert.Empty(rig.Caps.Pending);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-cap-ask")));
        Assert.False(rig.Levels.For("Dev", null).CapReached);
    }

    /// <summary>No on the page calls Decline, the card disappears and the Team stays at Ask me.</summary>
    [Fact]
    public async Task Page_No_DeclinesAndTheCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();
        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();

        await cut.InvokeAsync(() => cut.Find(".needs-you-cap-decline").ClickAsync(new()));

        Assert.Empty(rig.Caps.Pending);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-cap-ask")));
        Assert.True(rig.Levels.For("Dev", null).CapReached);
    }

    /// <summary>The sidebar card lists the open question as a row, counts it, and drops the row once the question is answered elsewhere.</summary>
    [Fact]
    public async Task Card_PendingCapAsk_ListsARowAndDropsItWhenAnswered()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        using NeedsYouLevelUiRig rig = new(ctx);
        rig.OpenTheTeamQuestion();

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        Assert.Equal("Dev", cut.Find(".needs-you-cap-row .needs-you-name").TextContent.Trim());
        Assert.Equal("Spend cap reached", cut.Find(".needs-you-cap-row .needs-you-detail").TextContent.Trim());
        Assert.Equal("1", cut.Find(".needs-you-count").TextContent.Trim());

        Assert.True(rig.Caps.Decline("team:Dev"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-cap-row")));
    }
}
