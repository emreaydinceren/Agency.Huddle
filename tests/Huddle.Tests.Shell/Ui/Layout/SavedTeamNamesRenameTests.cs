using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins R11 for the Team names the browser's localStorage remembers (<c>shell:teamShortcuts</c>, <c>huddle:tabs</c>): a name journalled as renamed loads as today's name.</summary>
public sealed class SavedTeamNamesRenameTests
{
    private static TeamRenameStore Journal(TempDataDir dir)
    {
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        return journal;
    }

    /// <summary>A shortcut the Human removed under the old Team name is still removed for the renamed Team.</summary>
    [Fact]
    public async Task TeamShortcutStore_RemovedUnderOldName_AppliesToNewName()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", TeamShortcutStore.StorageKey).SetResult("{\"Off\":false,\"Removed\":[\"Marketing\"]}");
        TeamShortcutStore store = new(ctx.JSInterop.JSRuntime, null, Journal(dir));

        await store.LoadAsync();

        Assert.False(store.Has("Growth"));
        Assert.True(store.Has("Research"));
    }

    /// <summary>A stored Tab whose address names the old Team is restored with the new Team's address.</summary>
    [Fact]
    public async Task TabPersistence_TabUnderOldTeamName_RestoresUnderNewName()
    {
        using TempDataDir dir = new();
        TabService source = new();
        Tab old = source.Open("/teams/Marketing", "Marketing");
        source.Keep(old.Id);
        await using MudBunitContext ctx = new();
        TabService tabs = new();
        ctx.Services.AddSingleton(tabs);
        ctx.Services.AddSingleton<ITeamRenames>(Journal(dir));
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle:tabs").SetResult(source.Export().ToJson());
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Growth");

        IRenderedComponent<TabPersistence> cut = ctx.Render<TabPersistence>();

        cut.WaitForAssertion(() => Assert.EndsWith("/teams/Growth", Assert.Single(tabs.Tabs).Url, StringComparison.Ordinal));
    }
}
