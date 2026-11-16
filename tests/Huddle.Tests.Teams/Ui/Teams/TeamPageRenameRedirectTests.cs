using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins R11 for the Team route: an old Team or Project name in the address is replaced by today's name; a never-journalled unknown name keeps the not-found notice.</summary>
public sealed class TeamPageRenameRedirectTests
{
    private const string AlertSelector = ".team-page-notice[role='alert']";

    /// <summary>/teams/Marketing, after Marketing became Growth, replaces the history entry with /teams/Growth.</summary>
    [Fact]
    public async Task OldTeamName_NavigatesToTheNewRouteWithReplace()
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        await using MudBunitContext ctx = NewContext(journal);

        _ = RenderPage(ctx, "Marketing", null);

        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        NavigationHistory entry = Assert.Single(navigation.History);
        Assert.Equal("/teams/Growth", navigation.ToAbsoluteUri(entry.Uri).AbsolutePath);
        Assert.True(entry.Options.ReplaceHistoryEntry);
    }

    /// <summary>A Team name that was never journalled and is not in the catalog keeps the not-found notice and does not navigate.</summary>
    [Fact]
    public async Task UnknownTeam_NeverJournalled_KeepsNotFoundView()
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        await using MudBunitContext ctx = NewContext(journal);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Research", null);

        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Empty(navigation.History);
        Assert.Single(cut.FindAll(AlertSelector));
    }

    /// <summary>A Project route naming a renamed Project replaces the entry with the Project's new route.</summary>
    [Fact]
    public async Task OldProjectName_NavigatesToTheNewProjectRouteWithReplace()
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Project, "Growth", "Launch", "Relaunch"));
        await using MudBunitContext ctx = NewContext(journal);

        _ = RenderPage(ctx, "Growth", "Launch");

        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        NavigationHistory entry = Assert.Single(navigation.History);
        Assert.Equal("/teams/Growth/projects/Relaunch", navigation.ToAbsoluteUri(entry.Uri).AbsolutePath);
        Assert.True(entry.Options.ReplaceHistoryEntry);
    }

    /// <summary>A never-journalled unknown Project keeps the Project not-found notice and does not navigate.</summary>
    [Fact]
    public async Task UnknownProject_NeverJournalled_KeepsNotFoundView()
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Project, "Growth", "Launch", "Relaunch"));
        await using MudBunitContext ctx = NewContext(journal);

        IRenderedComponent<TeamPage> cut = RenderPage(ctx, "Growth", "Other");

        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Empty(navigation.History);
        Assert.Single(cut.FindAll(AlertSelector));
    }

    private static MudBunitContext NewContext(ITeamRenames journal)
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Growth", ["Relaunch"], ["Nova"], true)] };
        MudBunitContext ctx = UiTestHost.Create(services =>
        {
            services.AddSingleton<ITeamCatalog>(catalog);
            services.AddSingleton(journal);
        });
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamDetails>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();
        ctx.ComponentFactories.AddStub<ProjectSpend>();
        return ctx;
    }

    private static IRenderedComponent<TeamPage> RenderPage(MudBunitContext ctx, string team, string? project) =>
        ctx.Render<TeamPage>(p => p.Add(x => x.Team, team).Add(x => x.Project, project));
}
