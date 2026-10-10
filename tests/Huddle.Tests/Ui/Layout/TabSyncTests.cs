using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
}
