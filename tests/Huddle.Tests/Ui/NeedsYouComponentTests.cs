using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.NeedsYou;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="NeedsYouList"/> and <see cref="NeedsYouCard"/> over a real
/// <see cref="NeedsYouTracker"/> and Team Directory (Needs You spec §6.4, §8).
/// </summary>
public sealed class NeedsYouComponentTests
{
    /// <summary>Each waiting Room is a row with its current Name, who asked, and a link to the Room.</summary>
    [Fact]
    public async Task List_RendersARowPerEntry()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You?", ct: ct);
        await using MudBunitContext ctx = NewContext(fixture);

        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();

        Assert.Single(cut.FindAll(".needs-you-row"));
        Assert.Equal("Backend", cut.Find(".needs-you-name").TextContent);
        Assert.Equal("Nova · 09:00", cut.Find(".needs-you-detail").TextContent);
        Assert.NotNull(cut.Find($"a[href='/rooms/{room.Id}']"));
    }

    /// <summary>Pressing the dismiss button hides the row, and the Room stays dismissed in the tracker.</summary>
    [Fact]
    public async Task List_Dismiss_RemovesTheRow()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        await using MudBunitContext ctx = NewContext(fixture);
        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();

        await cut.InvokeAsync(() => cut.Find("button[aria-label^='Dismiss ']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-row")));
        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>An Archived Room is tracked but its row is hidden, and it returns when the Room is unarchived.</summary>
    [Fact]
    public async Task List_HidesAnArchivedRoom_UntilItIsUnarchived()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        await using MudBunitContext ctx = NewContext(fixture);
        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();

        _ = await fixture.Service.SetRoomArchivedAsync(room.Id, true, ct);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".needs-you-row")));
        Assert.Single(fixture.Tracker.Snapshot());

        _ = await fixture.Service.SetRoomArchivedAsync(room.Id, false, ct);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".needs-you-row")));
    }

    /// <summary>Renaming a Room updates its row, because the list keys on the id and reads the Name each time.</summary>
    [Fact]
    public async Task List_Rename_UpdatesTheRow()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        await using MudBunitContext ctx = NewContext(fixture);
        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();

        _ = await fixture.Service.RenameRoomAsync(room.Id, "API", ct);

        cut.WaitForAssertion(() => Assert.Equal("API", cut.Find(".needs-you-name").TextContent));
    }

    /// <summary>A Room that starts waiting after the list is on screen appears without a reload.</summary>
    [Fact]
    public async Task List_LiveMention_AddsARow()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        await using MudBunitContext ctx = NewContext(fixture);
        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();
        Assert.Empty(cut.FindAll(".needs-you-row"));

        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".needs-you-row")));
    }

    /// <summary>With nothing waiting the card is present but hidden, so the list inside it still hears the tracker.</summary>
    [Fact]
    public async Task Card_NothingWaiting_IsHidden()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        await using MudBunitContext ctx = NewContext(fixture);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        Assert.True(cut.Find(".needs-you-card").HasAttribute("hidden"));
    }

    /// <summary>With a Room waiting the card is shown with the count, and hides again when the Human answers.</summary>
    [Fact]
    public async Task Card_ShowsTheCount_AndHidesWhenAnswered()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        await using MudBunitContext ctx = NewContext(fixture);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        cut.WaitForAssertion(() => Assert.False(cut.Find(".needs-you-card").HasAttribute("hidden")));
        Assert.Equal("1", cut.Find(".needs-you-count").TextContent.Trim());

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "postgres", ct: ct);

        cut.WaitForAssertion(() => Assert.True(cut.Find(".needs-you-card").HasAttribute("hidden")));
    }

    /// <summary>The card's title is a sidebar header, so it is caption-sized like the other sidebar section headers, not a 16px section heading.</summary>
    [Fact]
    public async Task Card_TitleIsASidebarHeader_NotASectionHeading()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        await using MudBunitContext ctx = NewContext(fixture);

        IRenderedComponent<NeedsYouCard> cut = ctx.Render<NeedsYouCard>();

        var title = cut.Find(".needs-you-title .needs-you-heading");
        Assert.Equal("Needs you", title.TextContent);
        Assert.Contains("mud-typography-caption", title.ClassList);
        Assert.DoesNotContain("mud-typography-subtitle2", title.ClassList);
    }

    /// <summary>Builds a bUnit context holding the services both components inject.</summary>
    /// <param name="fixture">The fixture whose tracker, directory and events the components use.</param>
    /// <returns>The context; the caller disposes it.</returns>
    private static MudBunitContext NewContext(NeedsYouFixture fixture)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(fixture.Tracker);
        ctx.Services.AddSingleton<ITeamDirectory>(fixture.Team);
        ctx.Services.AddSingleton(fixture.Events);
        ctx.Services.AddSingleton<TimeProvider>(fixture.Clock);
        ctx.Services.AddSingleton<TabService>();
        return ctx;
    }
}
