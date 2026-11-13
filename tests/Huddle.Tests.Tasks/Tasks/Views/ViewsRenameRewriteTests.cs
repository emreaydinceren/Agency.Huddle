using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// <see cref="ViewsRenameRewrite"/> (Team Rename Spec 4, Views): step 6 rewrites the Team and Project entries of every View's filter
/// through <see cref="ViewStore.RenameTeam"/>, touches no other Team, and a second run changes nothing.
/// </summary>
public sealed class ViewsRenameRewriteTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static TeamRename Rename() => new(1, TeamRenameKind.Team, "Growth", "Growth", "Revenue", TeamRenameState.Pending, Stamp);

    private static TaskView ViewWith(string id, IReadOnlyList<string> teams, IReadOnlyList<ProjectRef> projects) => new()
    {
        Id = id,
        Name = id,
        Kind = ViewKind.List,
        Filter = new TaskFilter { Teams = teams, Projects = projects },
    };

    /// <summary>The rewrite renames the Team and its Project references in a saved View and names no other Team to pause.</summary>
    [Fact]
    public async Task RunAsync_RewritesTeamAndProjectRefs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(ViewWith("v1", ["Growth", "Ops"], [new ProjectRef("Growth", "Launch")]));
        ViewsRenameRewrite rewrite = new(store);

        await rewrite.RunAsync(Rename(), ct);

        TaskView? view = store.Get("v1");
        Assert.NotNull(view);
        Assert.Equal(["Revenue", "Ops"], view.Filter.Teams);
        Assert.Equal([new ProjectRef("Revenue", "Launch")], view.Filter.Projects);
        Assert.Empty(rewrite.TeamsTouched(Rename()));
    }

    /// <summary>Running the rewrite a second time, as recovery may, leaves the View as the first run left it.</summary>
    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(ViewWith("v1", ["Growth"], [new ProjectRef("Growth", null)]));
        ViewsRenameRewrite rewrite = new(store);

        await rewrite.RunAsync(Rename(), ct);
        await rewrite.RunAsync(Rename(), ct);

        TaskView? view = store.Get("v1");
        Assert.NotNull(view);
        Assert.Equal(["Revenue"], view.Filter.Teams);
        Assert.Equal([new ProjectRef("Revenue", null)], view.Filter.Projects);
    }
}
