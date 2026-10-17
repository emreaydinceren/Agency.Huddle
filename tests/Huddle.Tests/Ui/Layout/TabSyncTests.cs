using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Circuits;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins <see cref="TabSync"/> against a real Team Directory: titles follow Room names and archived Rooms lose their Tab.</summary>
public sealed class TabSyncTests
{
    private static async Task<(MudBunitContext Ctx, SqliteTeamDirectory Directory, RoomEvents Events, TabService Tabs, TempDataDir Dir)> SetUpAsync(CancellationToken ct)
    {
        TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        TabService tabs = new();
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamDirectory>(directory);
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton(tabs);
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog());
        ctx.Services.AddFakeCircuitConnection();
        return (ctx, directory, events, tabs, dir);
    }

    /// <summary>A Tab opened by URL starts as "Chat" and is retitled to the Room's name on the first pass.</summary>
    [Fact]
    public async Task FirstPass_RetitlesARoomTab()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, SqliteTeamDirectory directory, _, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            Room room = await directory.CreateRoomAsync("Design", [], ct);
            tabs.Open($"/rooms/{room.Id}");

            ctx.Render<TabSync>();

            Assert.Equal("Design", Assert.Single(tabs.Tabs).Title);
        }
    }

    /// <summary>Archiving a Room and announcing it closes the Room's Tab.</summary>
    [Fact]
    public async Task RoomsChanged_AfterArchive_ClosesTheTab()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, SqliteTeamDirectory directory, RoomEvents events, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            Room room = await directory.CreateRoomAsync("Design", [], ct);
            tabs.Open($"/rooms/{room.Id}");
            var cut = ctx.Render<TabSync>();

            await directory.SetRoomArchivedAsync(room.Id, archived: true, ct);
            events.PublishRoomsChanged();

            cut.WaitForAssertion(() => Assert.Empty(tabs.Tabs));
        }
    }

    /// <summary>A Team that drops out of the catalog loses its Tab when the catalog announces the change.</summary>
    [Fact]
    public async Task CatalogChanged_TeamGone_ClosesItsTab()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, _, _, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            FakeTeamCatalog catalog = (FakeTeamCatalog)ctx.Services.GetRequiredService<ITeamCatalog>();
            catalog.Teams = [new TeamSummary("Core", [], [], true), new TeamSummary("Other", [], [], true)];
            tabs.Open("/teams/Core");
            var cut = ctx.Render<TabSync>();

            catalog.Teams = [new TeamSummary("Other", [], [], true)];
            catalog.Raise();

            cut.WaitForAssertion(() => Assert.Empty(tabs.Tabs));
        }
    }

    /// <summary>Archiving the active Tab's Room moves the active Tab, and while the client is connected the router follows it to the other Tab's URL.</summary>
    [Fact]
    public async Task RoomsChanged_ArchivingTheActiveTabsRoom_WhenConnected_FollowsTheNewActiveTab()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, SqliteTeamDirectory directory, RoomEvents events, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            (Room kept, Room archived) = await OpenTwoRoomTabsAsync(directory, tabs, ct);
            var cut = ctx.Render<TabSync>();
            NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();

            await directory.SetRoomArchivedAsync(archived.Id, archived: true, ct);
            events.PublishRoomsChanged();

            cut.WaitForAssertion(() => Assert.EndsWith($"/rooms/{kept.Id}", navigation.Uri, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// The same archive after the client is gone (a closed or reloaded browser whose circuit is still retained) still
    /// closes the Tab, because that is the app's own state, but issues no navigation, which the framework would fail
    /// and log as two errors.
    /// </summary>
    [Fact]
    public async Task RoomsChanged_ArchivingTheActiveTabsRoom_WhenTheClientIsGone_ClosesTheTabWithoutNavigating()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, SqliteTeamDirectory directory, RoomEvents events, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            (Room kept, Room archived) = await OpenTwoRoomTabsAsync(directory, tabs, ct);
            var cut = ctx.Render<TabSync>();
            NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
            string addressBefore = navigation.Uri;
            ((FakeCircuitConnection)ctx.Services.GetRequiredService<ICircuitConnection>()).IsConnected = false;

            await directory.SetRoomArchivedAsync(archived.Id, archived: true, ct);
            events.PublishRoomsChanged();

            cut.WaitForAssertion(() => Assert.Equal($"/rooms/{kept.Id}", Assert.Single(tabs.Tabs).Url));

            // The pass that closed the Tab and the navigation that would follow it are one dispatcher turn; waiting
            // for the dispatcher to come round again means that turn is over, so the address can be checked.
            await cut.InvokeAsync(static () => { });
            Assert.Equal(addressBefore, navigation.Uri);
        }
    }

    /// <summary>A Team leaving the catalog closes its active Tab, and while the client is connected the router follows to the other Tab's URL.</summary>
    [Fact]
    public async Task CatalogChanged_TeamGone_WhenConnected_FollowsTheNewActiveTab()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, _, _, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            FakeTeamCatalog catalog = OpenTwoTeamTabs(ctx, tabs);
            var cut = ctx.Render<TabSync>();
            NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();

            catalog.Teams = [new TeamSummary("Core", [], [], true)];
            catalog.Raise();

            cut.WaitForAssertion(() => Assert.EndsWith("/teams/Core", navigation.Uri, StringComparison.Ordinal));
        }
    }

    /// <summary>The same catalog change after the client is gone still closes the Tab but issues no navigation.</summary>
    [Fact]
    public async Task CatalogChanged_TeamGone_WhenTheClientIsGone_ClosesTheTabWithoutNavigating()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        (MudBunitContext ctx, _, _, TabService tabs, TempDataDir dir) = await SetUpAsync(ct);
        using (dir)
        await using (ctx)
        {
            FakeTeamCatalog catalog = OpenTwoTeamTabs(ctx, tabs);
            var cut = ctx.Render<TabSync>();
            NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
            string addressBefore = navigation.Uri;
            ((FakeCircuitConnection)ctx.Services.GetRequiredService<ICircuitConnection>()).IsConnected = false;

            catalog.Teams = [new TeamSummary("Core", [], [], true)];
            catalog.Raise();

            cut.WaitForAssertion(() => Assert.Equal("/teams/Core", Assert.Single(tabs.Tabs).Url));
            await cut.InvokeAsync(static () => { });
            Assert.Equal(addressBefore, navigation.Uri);
        }
    }

    /// <summary>Opens a kept Tab for one Room, then a second Tab for another that becomes the active one.</summary>
    /// <param name="directory">The directory to create the two Rooms in.</param>
    /// <param name="tabs">The Tab service to open them in.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The Room whose Tab is kept, and the Room whose Tab is active.</returns>
    private static async Task<(Room Kept, Room Active)> OpenTwoRoomTabsAsync(SqliteTeamDirectory directory, TabService tabs, CancellationToken ct)
    {
        Room kept = await directory.CreateRoomAsync("Kept", [], ct);
        Room active = await directory.CreateRoomAsync("Archived", [], ct);
        tabs.Keep(tabs.Open($"/rooms/{kept.Id}").Id);
        tabs.Open($"/rooms/{active.Id}");
        return (kept, active);
    }

    /// <summary>Lists two Teams in the catalog and opens a kept Tab for the first, then a second Tab for the other that becomes the active one.</summary>
    /// <param name="ctx">The context whose catalog to fill.</param>
    /// <param name="tabs">The Tab service to open the Tabs in.</param>
    /// <returns>The catalog, for the test to change.</returns>
    private static FakeTeamCatalog OpenTwoTeamTabs(MudBunitContext ctx, TabService tabs)
    {
        FakeTeamCatalog catalog = (FakeTeamCatalog)ctx.Services.GetRequiredService<ITeamCatalog>();
        catalog.Teams = [new TeamSummary("Core", [], [], true), new TeamSummary("Other", [], [], true)];
        tabs.Keep(tabs.Open("/teams/Core").Id);
        tabs.Open("/teams/Other");
        return catalog;
    }
}
