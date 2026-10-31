using System.Reflection;
using Bunit;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.NeedsYou;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;
using NeedsYouPageComponent = Agency.Huddle.App.Components.NeedsYou.NeedsYouPage;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins T12.1 for <c>NeedsYouPage</c> at <c>/needs-you</c> (Human gate Spec §6.7; UI copy rows 37-38; corrections P12):
/// Task entries first, each a decision card wrapped in its id, then the Room list; the empty state; the refresh on
/// <see cref="NeedsYouTaskSource.Changed"/>; the unsubscribe on dispose.
/// </summary>
public sealed class NeedsYouPageTests
{
    private const string Title = "Write the reply";

    /// <summary>How many handlers are subscribed to <see cref="NeedsYouTaskSource.Changed"/>.</summary>
    /// <param name="source">The source.</param>
    private static int Subscribers(NeedsYouTaskSource source)
    {
        FieldInfo field = typeof(NeedsYouTaskSource).GetField(nameof(NeedsYouTaskSource.Changed), BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The Changed event has no backing field.");
        return field.GetValue(source) is Delegate handlers ? handlers.GetInvocationList().Length : 0;
    }

    /// <summary>Each Task entry is a decision card wrapped in its id, and the Room list follows them.</summary>
    [Fact]
    public async Task Page_ListsTaskEntriesAsDecisionCards_ThenTheRoomList()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        _ = await NeedsYouTaskTestHost.WaitingRoomAsync(fixture, ct);
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0002", "Second", NeedsYouTaskTestHost.SecondEntry);
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".task-decision-card").Count));
        Assert.NotNull(cut.Find("#PLAT-0001 .task-decision-card"));
        Assert.NotNull(cut.Find("#PLAT-0002 .task-decision-card"));
        int first = cut.Markup.IndexOf("id=\"PLAT-0001\"", StringComparison.Ordinal);
        int second = cut.Markup.IndexOf("id=\"PLAT-0002\"", StringComparison.Ordinal);
        int rooms = cut.Markup.IndexOf("needs-you-list", StringComparison.Ordinal);
        Assert.True(first >= 0 && first < second && second < rooms);
        Assert.Equal("Backend", cut.Find(".needs-you-list .needs-you-name").TextContent.Trim());
    }

    /// <summary>With nothing waiting the page says so, with the title and sentence of UI copy row 38.</summary>
    [Fact]
    public async Task Page_NothingWaiting_ShowsTheEmptyState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);

        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();

        Assert.Equal("Nothing needs you", HumanGateCopy.NeedsYouEmptyTitle);
        Assert.Equal("Tasks waiting for your decision and Rooms waiting on you appear here.", HumanGateCopy.NeedsYouEmptyText);
        cut.WaitForAssertion(() => Assert.Equal("Nothing needs you", cut.Find(".empty-state-title").TextContent.Trim()));
        Assert.Equal("Tasks waiting for your decision and Rooms waiting on you appear here.", cut.Find(".empty-state-text").TextContent.Trim());
    }

    /// <summary>A Task entry that appears after the first render replaces the empty state with its decision card.</summary>
    [Fact]
    public async Task Page_TaskSourceChanged_ShowsTheNewEntry()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();
        Assert.Empty(cut.FindAll(".task-decision-card"));

        _ = NeedsYouTaskTestHost.SeedWaiting(harness, "PLAT-0001", Title, NeedsYouTaskTestHost.FirstEntry);
        harness.Events.RaiseTasksReloaded();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#PLAT-0001 .task-decision-card")));
        Assert.Empty(cut.FindAll(".empty-state-title"));
    }

    /// <summary>Disposing the page removes its handler from the source, so a singleton source does not keep it alive.</summary>
    [Fact]
    public async Task Page_Dispose_UnsubscribesFromTheTaskSource()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        using TaskToolHarness harness = new();
        using NeedsYouTaskSource source = new(harness.Store, new NeedsYouTaskTestHost.ThreeOfFourProofSource(), harness.Events);
        await using MudBunitContext ctx = NeedsYouTaskTestHost.NewContext(fixture, harness, source);
        IRenderedComponent<NeedsYouPageComponent> cut = ctx.Render<NeedsYouPageComponent>();
        Assert.True(cut.Markup.Length > 0);
        Assert.Equal(1, Subscribers(source));

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(0, Subscribers(source));
    }
}
