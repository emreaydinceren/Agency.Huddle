using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="RenamedTeamPaths.Map"/>: the first segment under the Teams root, and a Project second segment, follow the rename journal; the rest of the text is kept.</summary>
public sealed class RenamedTeamPathsTests : IDisposable
{
    private readonly TempDataDir dir = new();

    public void Dispose() => this.dir.Dispose();

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private TeamRenameStore Journal(params (TeamRenameKind Kind, string Team, string Old, string New)[] rows)
    {
        TeamRenameStore store = new(this.dir.Options());
        foreach ((TeamRenameKind kind, string team, string oldName, string newName) in rows)
        {
            store.Complete(store.Begin(kind, team, oldName, newName));
        }

        return store;
    }

    /// <summary>A path under an old Team name maps to the same path under today's name.</summary>
    [Fact]
    public void Map_OldTeamName_BecomesToday()
    {
        TeamRenameStore store = this.Journal((TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        string mapped = RenamedTeamPaths.Map(this.TeamsRoot, this.TeamsRoot + "/Marketing/docs/x.md", store);

        Assert.Equal(this.TeamsRoot + "/Growth/docs/x.md", mapped);
    }

    /// <summary>A chain of renames maps the oldest name to the newest.</summary>
    [Fact]
    public void Map_Chain_FollowsToLatest()
    {
        TeamRenameStore store = this.Journal((TeamRenameKind.Team, "A", "A", "B"), (TeamRenameKind.Team, "B", "B", "C"));

        string mapped = RenamedTeamPaths.Map(this.TeamsRoot, this.TeamsRoot + "/A/x.md", store);

        Assert.Equal(this.TeamsRoot + "/C/x.md", mapped);
    }

    /// <summary>A renamed Project in the second segment maps through ResolveProject, for the old Team name too.</summary>
    [Fact]
    public void Map_ProjectSegment_UsesResolveProject()
    {
        TeamRenameStore store = this.Journal(
            (TeamRenameKind.Team, "Marketing", "Marketing", "Growth"),
            (TeamRenameKind.Project, "Growth", "Launch", "Rollout"));

        string mapped = RenamedTeamPaths.Map(this.TeamsRoot, this.TeamsRoot + "/Marketing/Launch/plan.md", store);

        Assert.Equal(this.TeamsRoot + "/Growth/Rollout/plan.md", mapped);
    }

    /// <summary>Either separator is understood and the caller's separators are kept.</summary>
    [Fact]
    public void Map_BothSeparators_AreKept()
    {
        TeamRenameStore store = this.Journal((TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.Equal(this.TeamsRoot + "\\Growth\\x.md", RenamedTeamPaths.Map(this.TeamsRoot, this.TeamsRoot + "\\Marketing\\x.md", store));
        Assert.Equal(this.TeamsRoot + "/Growth/x.md", RenamedTeamPaths.Map(this.TeamsRoot, this.TeamsRoot + "/Marketing/x.md", store));
    }

    /// <summary>A sibling whose name only starts with an old name, and a path outside the Teams root, are unchanged.</summary>
    [Fact]
    public void Map_OtherNamesAndOutsideRoot_AreUnchanged()
    {
        TeamRenameStore store = this.Journal((TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        string sibling = this.TeamsRoot + "/Marketing2/x.md";
        string outside = Path.Combine(this.dir.Path, "Other", "Marketing", "x.md");

        Assert.Equal(sibling, RenamedTeamPaths.Map(this.TeamsRoot, sibling, store));
        Assert.Equal(outside, RenamedTeamPaths.Map(this.TeamsRoot, outside, store));
    }
}
