using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Team shortcuts at the top of the Activity bar (<c>S5</c>): every Team has one by default, in the catalog's
/// order and above a hairline; a click selects the Team and a click on the selected one collapses the Sidebar; the
/// right-click menu removes one or turns them all off, and the choice is remembered in <c>localStorage</c>; the Team
/// page offers "Add shortcut" only while its Team has none.
/// </summary>
public sealed class TeamShortcutTests
{
    private static readonly IReadOnlyList<TeamSummary> ThreeTeams = [Team("DevTeam"), Team("Ops"), Team("Data")];

    /// <summary>Nothing is shown before the stored choice is read, so a removed shortcut never flashes; then every Team has one.</summary>
    [Fact]
    public async Task Store_BeforeAndAfterLoad_ShowsNothingThenEveryTeam()
    {
        await using MudBunitContext ctx = NewContext(stored: null);
        TeamShortcutStore store = ctx.Services.GetRequiredService<TeamShortcutStore>();

        Assert.Empty(store.Visible(ThreeTeams));
        Assert.True(store.Has("Ops"));

        await store.LoadAsync();

        Assert.Equal(["DevTeam", "Ops", "Data"], Names(store.Visible(ThreeTeams)));
    }

    /// <summary>A stored removal is honoured on load, ignoring case, and a Team built later still gets its shortcut.</summary>
    [Fact]
    public async Task Store_StoredRemoval_HidesOnlyThatTeam()
    {
        await using MudBunitContext ctx = NewContext(stored: """{"Off":false,"Removed":["ops"]}""");
        TeamShortcutStore store = ctx.Services.GetRequiredService<TeamShortcutStore>();

        await store.LoadAsync();

        Assert.Equal(["DevTeam", "Data", "New"], Names(store.Visible([.. ThreeTeams, Team("New")])));
        Assert.False(store.Has("Ops"));
    }

    /// <summary>Text that is not the stored shape leaves every Team with its default shortcut.</summary>
    [Fact]
    public async Task Store_MalformedStoredText_KeepsTheDefault()
    {
        await using MudBunitContext ctx = NewContext(stored: "not json");
        TeamShortcutStore store = ctx.Services.GetRequiredService<TeamShortcutStore>();

        await store.LoadAsync();

        Assert.Equal(3, store.Visible(ThreeTeams).Count);
    }

    /// <summary>Adding a Team back after turning shortcuts off shows that one Team alone, and the choice is written to storage.</summary>
    [Fact]
    public async Task Store_AddAfterTurnOff_ShowsOnlyTheAddedTeam()
    {
        await using MudBunitContext ctx = NewContext(stored: null);
        TeamShortcutStore store = ctx.Services.GetRequiredService<TeamShortcutStore>();
        await store.LoadAsync();

        await store.TurnOffAsync();
        Assert.Empty(store.Visible(ThreeTeams));

        await store.AddAsync("Data", ThreeTeams);

        Assert.Equal(["Data"], Names(store.Visible(ThreeTeams)));
        object? written = ctx.JSInterop.Invocations
            .Last(invocation => string.Equals(invocation.Identifier, "huddleStorage.set", StringComparison.Ordinal))
            .Arguments[1];
        Assert.Equal("""{"Off":false,"Removed":["DevTeam","Ops"]}""", written);
    }

    /// <summary>The strip draws one tile per Team in catalog order, each pressed only when selected, followed by the hairline.</summary>
    [Fact]
    public async Task Bar_DrawsATilePerTeam_AboveTheHairline()
    {
        await using MudBunitContext ctx = NewContext(stored: null);
        IRenderedComponent<TeamShortcutBar> cut = ctx.Render<TeamShortcutBar>(p => p.Add(c => c.Selected, "ops"));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("button.activity-bar-shortcut").Count));
        IReadOnlyList<IElement> tiles = cut.FindAll("button.activity-bar-shortcut");
        Assert.Equal(["DevTeam", "Ops", "Data"], tiles.Select(static tile => tile.GetAttribute("aria-label")));
        IElement pressed = Assert.Single(tiles, static tile => tile.GetAttribute("aria-pressed") == "true");
        Assert.Equal("Ops", pressed.GetAttribute("data-team"));
        Assert.Single(cut.FindAll(".activity-bar-shortcuts + hr.activity-bar-divider"));
    }

    /// <summary>A click on a tile reports its Team; a click on the selected tile reports none, collapsing the Sidebar.</summary>
    [Fact]
    public async Task Bar_Click_ReportsTheTeamOrCollapses()
    {
        await using MudBunitContext ctx = NewContext(stored: null);
        List<string?> reported = [];
        IRenderedComponent<TeamShortcutBar> cut = ctx.Render<TeamShortcutBar>(p => p
            .Add(c => c.Selected, "Ops")
            .Add(c => c.SelectedChanged, (string? team) => reported.Add(team)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.activity-bar-shortcut")));

        await cut.Find("button[data-team='Ops']").ClickAsync(new());
        await cut.Find("button[data-team='Data']").ClickAsync(new());

        Assert.Equal([null, "Data"], reported);
    }

    /// <summary>The right-click menu's "Remove shortcut" takes that tile away and collapses the Sidebar that was showing it.</summary>
    [Fact]
    public async Task Bar_RemoveShortcut_TakesTheTileAway()
    {
        await using MudBunitContext ctx = NewContext(stored: null);
        List<string?> reported = [];
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamShortcutBar>(0);
            builder.AddComponentParameter(1, nameof(TeamShortcutBar.Selected), "Ops");
            builder.AddComponentParameter(2, nameof(TeamShortcutBar.SelectedChanged),
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<string?>(this, reported.Add));
            builder.CloseComponent();
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.activity-bar-shortcut")));

        await cut.InvokeAsync(() => cut.Find("button[data-team='Ops']").ContextMenuAsync(new()));
        string[] items = [.. cut.FindAll("div.mud-menu-item").Select(static item => item.TextContent.Trim())];
        Assert.Equal(["Remove shortcut", "Turn off Team shortcuts"], items);
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item")[0].ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Equal(["DevTeam", "Data"], cut.FindAll("button.activity-bar-shortcut").Select(static tile => tile.GetAttribute("data-team"))));
        Assert.Equal([null], reported);
    }

    /// <summary>With every shortcut turned off the strip draws nothing, not even the hairline.</summary>
    [Fact]
    public async Task Bar_TurnedOff_DrawsNothing()
    {
        await using MudBunitContext ctx = NewContext(stored: """{"Off":true,"Removed":[]}""");
        IRenderedComponent<TeamShortcutBar> cut = ctx.Render<TeamShortcutBar>();

        cut.WaitForAssertion(() => Assert.True(ctx.Services.GetRequiredService<TeamShortcutStore>().IsLoaded));
        Assert.Empty(cut.FindAll("button.activity-bar-shortcut"));
        Assert.Empty(cut.FindAll("hr.activity-bar-divider"));
    }

    /// <summary>The Team page's "Add shortcut" is absent while the Team has a shortcut, and adding one makes it go.</summary>
    [Fact]
    public async Task Button_ShownOnlyWithoutAShortcut_AndAddingHidesIt()
    {
        await using MudBunitContext ctx = NewContext(stored: """{"Off":false,"Removed":["Ops"]}""");
        IRenderedComponent<TeamShortcutButton> withShortcut = ctx.Render<TeamShortcutButton>(p => p.Add(c => c.Team, "Data"));
        IRenderedComponent<TeamShortcutButton> without = ctx.Render<TeamShortcutButton>(p => p.Add(c => c.Team, "Ops"));

        without.WaitForAssertion(() => Assert.Single(without.FindAll("button.team-shortcut-add")));
        Assert.Empty(withShortcut.FindAll("button.team-shortcut-add"));

        await without.Find("button.team-shortcut-add").ClickAsync(new());

        without.WaitForAssertion(() => Assert.Empty(without.FindAll("button.team-shortcut-add")));
        Assert.True(ctx.Services.GetRequiredService<TeamShortcutStore>().Has("Ops"));
    }

    /// <summary>A context whose catalog lists <see cref="ThreeTeams"/> and whose <c>huddleStorage.get</c> answers <paramref name="stored"/>.</summary>
    private static MudBunitContext NewContext(string? stored)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = ThreeTeams });
        ctx.JSInterop.Setup<string?>("huddleStorage.get", TeamShortcutStore.StorageKey).SetResult(stored);
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        return ctx;
    }

    private static TeamSummary Team(string name) => new(name, [], ["Nova"], true);

    private static string[] Names(IReadOnlyList<TeamSummary> teams) => [.. teams.Select(static team => team.Name)];
}
