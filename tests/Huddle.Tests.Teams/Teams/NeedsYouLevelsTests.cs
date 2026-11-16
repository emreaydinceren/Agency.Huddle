using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="NeedsYouLevels"/> (ADR-0088): the Project's level beats the Team's, which beats the global one; an unset scope inherits;
/// the saved global value beats the configuration; the scope that supplies the level also supplies the cap; and every value lives in
/// <c>team.db</c>, under the scope's id, so a file an Agent can write holds none of it and a rename keeps it.
/// </summary>
public sealed class NeedsYouLevelsTests
{
    private static NeedsYouLevels Build(TempDataDir dir, Action<TeamOptions>? configure = null) => new Rig(dir, configure).Levels;

    /// <summary>With nothing set anywhere the level is Ask me from the global scope, with no cap.</summary>
    [Fact]
    public void For_NothingSet_IsAskMeFromTheGlobalScope()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir);

        EffectiveLevel level = levels.For("Dev", "Site");

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, null), level);
    }

    /// <summary>The configured global level and cap apply when no other scope sets a level.</summary>
    [Fact]
    public void For_OnlyTheConfigurationSet_UsesTheConfiguredLevelAndCap()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, options =>
        {
            options.NeedsYouLevel.Level = "run-inside-team";
            options.NeedsYouLevel.SpendCap = 9m;
        });

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Global, null, 9m), levels.For("Dev", null));
    }

    /// <summary>An unknown configured level reads as Ask me.</summary>
    [Fact]
    public void For_UnknownConfiguredLevel_IsAskMe()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, options => options.NeedsYouLevel.Level = "whenever");

        Assert.Equal(NeedsYouLevel.AskMe, levels.For("Dev", null).Level);
    }

    /// <summary>The level Settings saved in <c>team.db</c> wins over the configuration, with its own cap.</summary>
    [Fact]
    public void For_SavedGlobalValue_BeatsTheConfiguration()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, options =>
        {
            options.NeedsYouLevel.Level = "run-inside-team";
            options.NeedsYouLevel.SpendCap = 9m;
        });

        levels.SetGlobal(NeedsYouLevel.AskMe, null);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, null), levels.For("Dev", null));
    }

    /// <summary>A Team's level beats the global one and brings its own cap, not the global cap.</summary>
    [Fact]
    public void For_TeamLevelSet_BeatsGlobalAndBringsItsOwnCap()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, options => options.NeedsYouLevel.SpendCap = 9m);

        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 3m));

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, "Dev", 3m), levels.For("Dev", null));
    }

    /// <summary>A Project's level beats its Team's, and the Team's cap is not borrowed when the Project sets none.</summary>
    [Fact]
    public void For_ProjectLevelSet_BeatsTheTeamAndTakesNoCapFromIt()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir);
        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 3m));
        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Project, "Site", null), levels.For("Dev", "Site"));
    }

    /// <summary>A Project that sets no level inherits its Team's: the cap travels with a level only, so clearing the level drops the cap.</summary>
    [Fact]
    public void For_ProjectUnset_InheritsTheTeam()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir);
        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 3m));
        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));
        Assert.True(levels.SetProject("Dev", "Site", null, 99m));

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, "Dev", 3m), levels.For("Dev", "Site"));
        Assert.Equal((null, null), levels.Own("Dev", "Site"));
    }

    /// <summary>A Team that never set a level, and a Team the catalog does not know, fall through to the global level.</summary>
    [Fact]
    public void For_UnsetOrUnknownTeam_FallsThroughToGlobal()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, options => options.NeedsYouLevel.Level = "run-inside-team");

        Assert.Equal(NeedsYouScope.Global, levels.For("Dev", "Site").Scope);
        Assert.Equal(NeedsYouScope.Global, levels.For("Nobody", null).Scope);
        Assert.Equal(NeedsYouScope.Global, levels.For(string.Empty, null).Scope);
    }

    /// <summary>A Task under a Project resolves through the Project; a Task directly under the Team resolves through the Team.</summary>
    [Fact]
    public void ForTask_UsesTheTasksFolderAsItsScope()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir);
        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, null));
        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));
        TaskItem inProject = TestTasks.Make(location: new TaskLocation("Dev", "Site", false));
        TaskItem inTeam = TestTasks.Make(location: new TaskLocation("Dev", null, false));
        levels.Place(inTeam);

        Assert.Equal((NeedsYouLevel.AskMe, NeedsYouScope.Project), (levels.ForTask(inProject).Level, levels.ForTask(inProject).Scope));
        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team), (levels.ForTask(inTeam).Level, levels.ForTask(inTeam).Scope));
    }

    /// <summary>A Team's level and cap are stored in <c>team.db</c> and read back by a new store over the same file; clearing the level clears the cap too.</summary>
    [Fact]
    public void SetTeam_StoresLevelAndCapInTheDatabase_AndClears()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);

        Assert.True(rig.Levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 4m));

        string key = NeedsYouCaps.TeamKey(rig.Ids.TeamId("Dev"));
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.RunInsideTheTeam, 4m), new NeedsYouLevelStore(dir.Options()).Get(key));
        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, 4m), rig.Levels.Own("Dev", null));

        Assert.True(rig.Levels.SetTeam("Dev", null, 4m));
        Assert.Null(new NeedsYouLevelStore(dir.Options()).Get(key));
        Assert.Equal((null, null), rig.Levels.Own("Dev", null));
    }

    /// <summary>Setting a Project level stores it under the Project's id; a cap of zero is stored as none.</summary>
    [Fact]
    public void SetProject_StoresLevel_AndStoresNoZeroCap()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);

        Assert.True(rig.Levels.SetProject("Dev", "Site", NeedsYouLevel.RunInsideTheTeam, 0m));

        string key = NeedsYouCaps.ProjectKey(rig.Ids.ProjectId("Dev", "Site"));
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.RunInsideTheTeam, null), new NeedsYouLevelStore(dir.Options()).Get(key));
        Assert.Equal(NeedsYouScope.Project, rig.Levels.For("Dev", "Site").Scope);
    }

    /// <summary>
    /// The values come from <c>team.db</c> only: a <c>_team.md</c> and a <c>_project.md</c> that name a level and a cap change nothing, and
    /// saving a level through the setters leaves both files byte for byte as they were.
    /// </summary>
    [Fact]
    public void Levels_FilesThatNameALevel_AreIgnoredAndNeverRewritten()
    {
        using TempDataDir dir = new();
        string teamFile = Path.Combine(dir.Path, "Teams", "Dev", "_team.md");
        string projectFile = Path.Combine(dir.Path, "Teams", "Dev", "Site", "_project.md");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile) ?? dir.Path);
        File.WriteAllText(teamFile, "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5'\n---\n");
        File.WriteAllText(projectFile, "---\nneedsYouLevel: 'run-inside-team'\nspendCap: '5'\n---\n");
        byte[] teamBefore = File.ReadAllBytes(teamFile);
        byte[] projectBefore = File.ReadAllBytes(projectFile);
        NeedsYouLevels levels = Build(dir);

        EffectiveLevel before = levels.For("Dev", "Site");
        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.AskMe, null));
        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.RunInsideTheTeam, 7m));

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, null), before);
        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Project, "Site", 7m), levels.For("Dev", "Site"));
        Assert.Equal(teamBefore, File.ReadAllBytes(teamFile));
        Assert.Equal(projectBefore, File.ReadAllBytes(projectFile));
    }

    /// <summary>A Team rename keeps the Team's level and cap, and the Projects' under it, because the scope is stored under an id that outlives the name.</summary>
    [Fact]
    public async Task TeamRename_KeepsTheTeamAndProjectLevelsAndCaps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir);
        Assert.True(rig.Levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 4m));
        Assert.True(rig.Levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));
        rig.Catalog.Teams = [new TeamSummary("Build", ["Site"], [], true)];

        await rig.Rewrite.RunAsync(Rename(TeamRenameKind.Team, "Dev", "Dev", "Build"), ct);
        await rig.Rewrite.RunAsync(Rename(TeamRenameKind.Team, "Dev", "Dev", "Build"), ct);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, "Build", 4m), rig.Levels.For("Build", null));
        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Project, "Site", null), rig.Levels.For("Build", "Site"));
        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, 4m), rig.Levels.Own("Build", null));
    }

    /// <summary>A Project rename keeps the Project's level and cap, and leaves the Team's alone.</summary>
    [Fact]
    public async Task ProjectRename_KeepsTheProjectLevelAndCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Rig rig = new(dir);
        Assert.True(rig.Levels.SetTeam("Dev", NeedsYouLevel.AskMe, null));
        Assert.True(rig.Levels.SetProject("Dev", "Site", NeedsYouLevel.RunInsideTheTeam, 6m));
        rig.Catalog.Teams = [new TeamSummary("Dev", ["Web"], [], true)];

        await rig.Rewrite.RunAsync(Rename(TeamRenameKind.Project, "Dev", "Site", "Web"), ct);
        await rig.Rewrite.RunAsync(Rename(TeamRenameKind.Project, "Dev", "Site", "Web"), ct);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Project, "Web", 6m), rig.Levels.For("Dev", "Web"));
        Assert.Equal((NeedsYouLevel.AskMe, (decimal?)null), rig.Levels.Own("Dev", null));
    }

    /// <summary>A setter returns <see langword="false"/>, stores nothing and raises nothing for a Team the catalog does not know or a Project the Team does not have.</summary>
    [Fact]
    public void Setters_UnknownTeamOrProject_ReturnFalseAndStoreNothing()
    {
        using TempDataDir dir = new();
        Rig rig = new(dir);
        int raised = 0;
        rig.Levels.Changed += () => raised++;

        Assert.False(rig.Levels.SetTeam("Nobody", NeedsYouLevel.RunInsideTheTeam, 1m));
        Assert.False(rig.Levels.SetProject("Dev", "Nope", NeedsYouLevel.RunInsideTheTeam, 1m));
        Assert.False(rig.Levels.SetProject("Nobody", "Site", NeedsYouLevel.RunInsideTheTeam, 1m));

        Assert.Equal(0, raised);
        Assert.Null(rig.Ids.FindTeamId("Nobody"));
        Assert.Null(rig.Ids.FindProjectId("Dev", "Nope"));
        Assert.Equal(NeedsYouScope.Global, rig.Levels.For("Dev", null).Scope);
    }

    /// <summary>Every setter that changes a value raises <see cref="NeedsYouLevels.Changed"/>, and a subscriber that throws does not stop the others or the setter.</summary>
    [Fact]
    public void Setters_RaiseChanged_EvenWhenASubscriberThrows()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir);
        int raised = 0;
        levels.Changed += static () => throw new InvalidOperationException("a broken page");
        levels.Changed += () => raised++;

        levels.SetGlobal(NeedsYouLevel.RunInsideTheTeam, null);
        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.AskMe, null));
        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));

        Assert.Equal(3, raised);
    }

    /// <summary>The saved global pair is stored in <c>team.db</c>, read back by a new store over the same file, and replaced by a later save; an empty store has none.</summary>
    [Fact]
    public void Store_SetAndGet_RoundTripsLevelAndCapAcrossInstances()
    {
        using TempDataDir dir = new();
        NeedsYouLevelStore first = new(dir.Options());
        Assert.Null(first.Get(NeedsYouCaps.GlobalKey));

        first.Set(NeedsYouCaps.GlobalKey, NeedsYouLevel.RunInsideTheTeam, 7.25m);
        NeedsYouLevelStore second = new(dir.Options());
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.RunInsideTheTeam, 7.25m), second.Get(NeedsYouCaps.GlobalKey));

        second.Set(NeedsYouCaps.GlobalKey, NeedsYouLevel.AskMe, null);
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.AskMe, null), first.Get(NeedsYouCaps.GlobalKey));
    }

    /// <summary>A Team id and a Project id are minted once, found without minting, looked up back to the current names, and follow a rename.</summary>
    [Fact]
    public void ScopeIdStore_MintsOnce_LooksUpBothWays_AndFollowsRenames()
    {
        using TempDataDir dir = new();
        ScopeIdStore ids = new(dir.Options());
        Assert.Null(ids.FindTeamId("Dev"));

        string team = ids.TeamId("Dev");
        string project = ids.ProjectId("dev", "Site");

        Assert.Equal(team, ids.TeamId("DEV"));
        Assert.Equal(team, ids.FindTeamId("dev"));
        Assert.Equal(project, ids.FindProjectId("Dev", "SITE"));
        Assert.Equal("Dev", ids.TeamNameOf(team));
        Assert.Equal(("Dev", "Site"), ids.ProjectNamesOf(project));

        ids.RenameProject("Dev", "Site", "Web");
        ids.RenameTeam("Dev", "Build");

        Assert.Equal(("Build", "Web"), ids.ProjectNamesOf(project));
        Assert.Null(ids.FindTeamId("Dev"));
        Assert.Equal(team, ids.FindTeamId("Build"));
    }

    private static TeamRename Rename(TeamRenameKind kind, string team, string oldName, string newName) =>
        new(1, kind, team, oldName, newName, TeamRenameState.Pending, DateTimeOffset.UnixEpoch);

    /// <summary>The levels and the stores under them, over a catalog that knows Team <c>Dev</c> and its Project <c>Site</c>.</summary>
    private sealed class Rig
    {
        public Rig(TempDataDir dir, Action<TeamOptions>? configure = null)
        {
            this.Catalog = new FakeTeamCatalog { Teams = [new TeamSummary("Dev", ["Site"], [], true)] };
            this.Ids = new ScopeIdStore(dir.Options());
            this.Levels = new NeedsYouLevels(this.Catalog, new NeedsYouLevelStore(dir.Options()), this.Ids, new TaskScopeStore(dir.Options()), dir.Options(configure));
            this.Rewrite = new NeedsYouRenameRewrite(this.Ids);
        }

        public FakeTeamCatalog Catalog { get; }

        public ScopeIdStore Ids { get; }

        public NeedsYouLevels Levels { get; }

        public NeedsYouRenameRewrite Rewrite { get; }
    }
}
