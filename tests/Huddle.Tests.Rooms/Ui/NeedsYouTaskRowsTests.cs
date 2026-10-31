using Bunit;
using AngleSharp.Dom;
using Agency.Huddle.App.Components.NeedsYou;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.NeedsYou;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins T12.1 for <see cref="NeedsYouCard"/> (Human gate Spec §6.7; UI copy rows 35-36; corrections P12): Task rows
/// follow the Room rows, read <c>&lt;id&gt; &lt;title&gt;</c> over <c>Waiting on you · Proof m/n</c>, link to
/// <c>/needs-you#&lt;id&gt;</c>, share one count with the Room rows and carry no dismiss button.
/// </summary>
public sealed class NeedsYouTaskRowsTests
{
    private const string Title = "Write the reply";

    /// <summary>A Task row follows the Room rows and reads the id and title over the waiting line with the Proof count.</summary>
    [Fact]
    public async Task Card_TaskRow_FollowsTheRoomRows_AndReadsIdTitleAndProof()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = await NeedsYouTaskTestHost.WaitingRoomAsync(fixture, ct);
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        IElement taskRow = cut.WaitForElement(".needs-you-task-row");
        Assert.Equal("PLAT-0001 " + Title, taskRow.QuerySelector(".needs-you-name")?.TextContent.Trim());
        Assert.Equal("Waiting on you · Proof 3/4", taskRow.QuerySelector(".needs-you-detail")?.TextContent.Trim());
        Assert.Equal("Waiting on you · Proof 3/4", HumanGateCopy.NeedsYouTaskDetail(3, 4));
        Assert.True(cut.Markup.IndexOf("Backend", StringComparison.Ordinal) < cut.Markup.IndexOf("PLAT-0001", StringComparison.Ordinal));
    }

    /// <summary>A Task row links to the Needs You page at the Task's id.</summary>
    [Fact]
    public async Task Card_TaskRow_LinksToTheNeedsYouPageAtTheTaskId()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        Assert.NotNull(cut.Find(".needs-you-task-row a[href='/needs-you#PLAT-0001']"));
    }

    /// <summary>The count is the Room rows plus the Task rows.</summary>
    [Fact]
    public async Task Card_Count_IsRoomRowsPlusTaskRows()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = await NeedsYouTaskTestHost.WaitingRoomAsync(fixture, ct);
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0002", "Second", NeedsYouTaskTestHost.SecondEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        cut.WaitForAssertion(() => Assert.Equal("3", cut.Find(".needs-you-count").TextContent.Trim()));
    }

    /// <summary>With no Room waiting and one Task entry the card is shown, with a count of one.</summary>
    [Fact]
    public async Task Card_OnlyTaskEntries_IsShown()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        cut.WaitForAssertion(() => Assert.False(cut.Find(".needs-you-card").HasAttribute("hidden")));
        Assert.Equal("1", cut.Find(".needs-you-count").TextContent.Trim());
    }

    /// <summary>A Task entry that appears after the first render shows the card, because the card snapshots the source on Changed.</summary>
    [Fact]
    public async Task Card_TaskSourceChanged_ShowsTheNewEntry()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();
        Assert.True(cut.Find(".needs-you-card").HasAttribute("hidden"));

        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        harness.Events.RaiseTasksReloaded();

        cut.WaitForAssertion(() => Assert.False(cut.Find(".needs-you-card").HasAttribute("hidden")));
        Assert.Equal("1", cut.Find(".needs-you-count").TextContent.Trim());
    }

    /// <summary>A Task row has no dismiss button: it leaves the list only by a decision or a status change.</summary>
    [Fact]
    public async Task Card_TaskRow_HasNoDismissButton()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        IElement taskRow = cut.WaitForElement(".needs-you-task-row");
        Assert.Empty(taskRow.QuerySelectorAll("button"));
        Assert.Empty(cut.FindAll("button[aria-label^='Dismiss ']"));
    }

    /// <summary>The card's heading is a link to the Needs You page.</summary>
    [Fact]
    public async Task Card_Heading_LinksToTheNeedsYouPage()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        Assert.Equal("Needs you", cut.Find(".needs-you-title a[href='/needs-you']").TextContent.Trim());
    }
}
