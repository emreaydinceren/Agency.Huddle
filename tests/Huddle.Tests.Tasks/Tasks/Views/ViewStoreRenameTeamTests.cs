using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// <see cref="ViewStore.RenameTeam"/> and <see cref="ViewStore.RenameProject"/> (Team Rename Spec 4, Views): a rename rewrites the
/// Team and Project entries of every View's filter, ignoring case, writes <c>views.json</c> and leaves other Views and entries alone.
/// </summary>
public sealed class ViewStoreRenameTeamTests
{
    private static TaskView ViewWith(string id, IReadOnlyList<string> teams, IReadOnlyList<ProjectRef> projects) => new()
    {
        Id = id,
        Name = id,
        Kind = ViewKind.List,
        Filter = new TaskFilter { Teams = teams, Projects = projects },
    };

    /// <summary>A Team entry is rewritten ignoring case, the Project entries of that Team follow it, and other entries stay.</summary>
    [Fact]
    public void RenameTeam_RewritesTeamAndProjectRefs_IgnoringCase()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(ViewWith("v1", ["growth", "Ops"], [new ProjectRef("GROWTH", "Launch"), new ProjectRef("Ops", null)]));

        store.RenameTeam("Growth", "Revenue");

        TaskView? view = store.Get("v1");
        Assert.NotNull(view);
        Assert.Equal(["Revenue", "Ops"], view.Filter.Teams);
        Assert.Equal([new ProjectRef("Revenue", "Launch"), new ProjectRef("Ops", null)], view.Filter.Projects);
    }

    /// <summary>A View whose filter names neither the old Team nor its Projects is not changed.</summary>
    [Fact]
    public void RenameTeam_OtherView_Untouched()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(ViewWith("v1", ["Growth"], []));
        _ = store.Save(ViewWith("v2", ["Ops"], [new ProjectRef("Ops", "Launch")]));

        store.RenameTeam("Growth", "Revenue");

        TaskView? other = store.Get("v2");
        Assert.NotNull(other);
        Assert.Equal(["Ops"], other.Filter.Teams);
        Assert.Equal([new ProjectRef("Ops", "Launch")], other.Filter.Projects);
    }

    /// <summary>The rewrite reaches <c>views.json</c>: a second store over the same folder reads the new Team.</summary>
    [Fact]
    public void RenameTeam_PersistsToViewsJson()
    {
        using TempDataDir dataDir = new();
        using (ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance))
        {
            _ = store.Save(ViewWith("v1", ["Growth"], []));
            store.RenameTeam("Growth", "Revenue");
        }

        using ViewStore reopened = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.Equal(["Revenue"], reopened.Get("v1")?.Filter.Teams);
        Assert.DoesNotContain("Growth", File.ReadAllText(Path.Combine(dataDir.Path, "views.json")), StringComparison.Ordinal);
    }

    /// <summary>A Project rename rewrites only that Team's reference to that Project, ignoring case.</summary>
    [Fact]
    public void RenameProject_RewritesOnlyThatTeamsProject()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(ViewWith("v1", ["Growth"], [new ProjectRef("growth", "launch"), new ProjectRef("Ops", "Launch"), new ProjectRef("Growth", "Other"), new ProjectRef("Growth", null)]));

        store.RenameProject("Growth", "Launch", "Debut");

        TaskView? view = store.Get("v1");
        Assert.NotNull(view);
        Assert.Equal(["Growth"], view.Filter.Teams);
        Assert.Equal([new ProjectRef("growth", "Debut"), new ProjectRef("Ops", "Launch"), new ProjectRef("Growth", "Other"), new ProjectRef("Growth", null)], view.Filter.Projects);
    }
}
