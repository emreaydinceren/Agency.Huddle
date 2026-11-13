using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>Pins R11 for the Tasks routes: an old Team or Project name in the address is replaced by today's name; a never-journalled unknown name is left alone.</summary>
public sealed class TasksPageRenameRedirectTests
{
    /// <summary>/teams/Marketing/tasks/all-tasks, after Marketing became Growth, replaces the history entry with the Growth route.</summary>
    [Fact]
    public async Task OldTeamName_NavigatesToTheNewTasksRouteWithReplace()
    {
        using TaskToolHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        await using MudBunitContext ctx = NewContext(harness, journal);

        _ = Render(ctx, "Marketing", null);

        NavigationHistory entry = Assert.Single(History(ctx), item => PathOf(ctx, item) == "/teams/Growth/tasks/all-tasks");
        Assert.True(entry.Options.ReplaceHistoryEntry);
    }

    /// <summary>A Project route naming a renamed Project replaces the entry with the new Project route.</summary>
    [Fact]
    public async Task OldProjectName_NavigatesToTheNewTasksRouteWithReplace()
    {
        using TaskToolHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Project, "Growth", "Launch", "Relaunch"));
        await using MudBunitContext ctx = NewContext(harness, journal);

        _ = Render(ctx, "Growth", "Launch");

        NavigationHistory entry = Assert.Single(History(ctx), item => PathOf(ctx, item) == "/teams/Growth/projects/Relaunch/tasks/all-tasks");
        Assert.True(entry.Options.ReplaceHistoryEntry);
    }

    /// <summary>A never-journalled unknown Team is not redirected anywhere.</summary>
    [Fact]
    public async Task UnknownTeam_NeverJournalled_DoesNotRedirect()
    {
        using TaskToolHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        await using MudBunitContext ctx = NewContext(harness, journal);

        _ = Render(ctx, "Research", null);

        Assert.DoesNotContain(History(ctx), item => PathOf(ctx, item).StartsWith("/teams/", StringComparison.Ordinal));
    }

    /// <summary>A never-journalled unknown Project is not redirected anywhere.</summary>
    [Fact]
    public async Task UnknownProject_NeverJournalled_DoesNotRedirect()
    {
        using TaskToolHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Project, "Growth", "Launch", "Relaunch"));
        await using MudBunitContext ctx = NewContext(harness, journal);

        _ = Render(ctx, "Growth", "Other");

        Assert.DoesNotContain(History(ctx), item => PathOf(ctx, item).StartsWith("/teams/", StringComparison.Ordinal));
    }

    private static MudBunitContext NewContext(TaskToolHarness harness, ITeamRenames journal)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = [new TeamSummary("Growth", ["Relaunch"], ["Nova"], true)] });
        ctx.Services.AddSingleton(journal);
        return ctx;
    }

    private static IRenderedComponent<IComponent> Render(MudBunitContext ctx, string team, string? project) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.Team), team);
            builder.AddAttribute(2, nameof(TasksPage.Project), project);
            builder.AddAttribute(3, nameof(TasksPage.ViewId), "all-tasks");
            builder.CloseComponent();
        });

    private static IReadOnlyList<NavigationHistory> History(MudBunitContext ctx) =>
        [.. ((BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>()).History];

    private static string PathOf(MudBunitContext ctx, NavigationHistory item) =>
        ctx.Services.GetRequiredService<NavigationManager>().ToAbsoluteUri(item.Uri).AbsolutePath;
}
