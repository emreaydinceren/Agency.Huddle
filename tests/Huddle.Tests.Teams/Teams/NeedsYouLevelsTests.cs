using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="NeedsYouLevels"/> (ADR-0087): the Project's level beats the Team's, which beats the global one; an unset scope inherits;
/// the saved global value beats the configuration; and the scope that supplies the level also supplies the cap.
/// </summary>
public sealed class NeedsYouLevelsTests
{
    private static readonly VersionedRef Type = new("development", "1.0.0");

    private static TeamFile Team(NeedsYouLevel? level = null, decimal? cap = null) =>
        new(Type, null, null, null, string.Empty) { NeedsYouLevel = level, SpendCap = cap };

    private static NeedsYouLevels Build(TempDataDir dir, MemoryFolders folders, Action<TeamOptions>? configure = null)
    {
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Dev", ["Site"], [], true)] };
        return new NeedsYouLevels(catalog, folders, new NeedsYouLevelStore(dir.Options()), dir.Options(configure));
    }

    /// <summary>With nothing set anywhere the level is Ask me from the global scope, with no cap.</summary>
    [Fact]
    public void For_NothingSet_IsAskMeFromTheGlobalScope()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders { Team = Team() });

        EffectiveLevel level = levels.For("Dev", "Site");

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, null), level);
    }

    /// <summary>The configured global level and cap apply when no other scope sets a level.</summary>
    [Fact]
    public void For_OnlyTheConfigurationSet_UsesTheConfiguredLevelAndCap()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders { Team = Team() }, options =>
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
        NeedsYouLevels levels = Build(dir, new MemoryFolders(), options => options.NeedsYouLevel.Level = "whenever");

        Assert.Equal(NeedsYouLevel.AskMe, levels.For("Dev", null).Level);
    }

    /// <summary>The level Settings saved in <c>team.db</c> wins over the configuration, with its own cap.</summary>
    [Fact]
    public void For_SavedGlobalValue_BeatsTheConfiguration()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders(), options =>
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
        NeedsYouLevels levels = Build(dir, new MemoryFolders { Team = Team(NeedsYouLevel.RunInsideTheTeam, 3m) }, options => options.NeedsYouLevel.SpendCap = 9m);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, "Dev", 3m), levels.For("Dev", null));
    }

    /// <summary>A Project's level beats its Team's, and the Team's cap is not borrowed when the Project sets none.</summary>
    [Fact]
    public void For_ProjectLevelSet_BeatsTheTeamAndTakesNoCapFromIt()
    {
        using TempDataDir dir = new();
        MemoryFolders folders = new()
        {
            Team = Team(NeedsYouLevel.RunInsideTheTeam, 3m),
            Projects = { ["Site"] = ProjectFile.Empty with { NeedsYouLevel = NeedsYouLevel.AskMe } },
        };
        NeedsYouLevels levels = Build(dir, folders);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Project, "Site", null), levels.For("Dev", "Site"));
    }

    /// <summary>A Project that sets no level inherits its Team's, even when its file holds a cap: the cap travels with a level only.</summary>
    [Fact]
    public void For_ProjectUnset_InheritsTheTeam()
    {
        using TempDataDir dir = new();
        MemoryFolders folders = new()
        {
            Team = Team(NeedsYouLevel.RunInsideTheTeam, 3m),
            Projects = { ["Site"] = ProjectFile.Empty with { SpendCap = 99m } },
        };
        NeedsYouLevels levels = Build(dir, folders);

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, "Dev", 3m), levels.For("Dev", "Site"));
    }

    /// <summary>A Team with no readable file, and a Team the catalog does not know, fall through to the global level.</summary>
    [Fact]
    public void For_MissingTeamFileOrTeam_FallsThroughToGlobal()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders(), options => options.NeedsYouLevel.Level = "run-inside-team");

        Assert.Equal(NeedsYouScope.Global, levels.For("Dev", "Site").Scope);
        Assert.Equal(NeedsYouScope.Global, levels.For("Nobody", null).Scope);
        Assert.Equal(NeedsYouScope.Global, levels.For(string.Empty, null).Scope);
    }

    /// <summary>A Task under a Project resolves through the Project; a Task directly under the Team resolves through the Team.</summary>
    [Fact]
    public void ForTask_UsesTheTasksFolderAsItsScope()
    {
        using TempDataDir dir = new();
        MemoryFolders folders = new()
        {
            Team = Team(NeedsYouLevel.RunInsideTheTeam),
            Projects = { ["Site"] = ProjectFile.Empty with { NeedsYouLevel = NeedsYouLevel.AskMe } },
        };
        NeedsYouLevels levels = Build(dir, folders);
        TaskItem inProject = TestTasks.Make(location: new TaskLocation("Dev", "Site", false));
        TaskItem inTeam = TestTasks.Make(location: new TaskLocation("Dev", null, false));

        Assert.Equal((NeedsYouLevel.AskMe, NeedsYouScope.Project), (levels.ForTask(inProject).Level, levels.ForTask(inProject).Scope));
        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team), (levels.ForTask(inTeam).Level, levels.ForTask(inTeam).Scope));
    }

    /// <summary>Setting a Team level writes it and the cap, and keeps every other key and the notes; unsetting it removes both lines.</summary>
    [Fact]
    public void SetTeam_WritesLevelAndCap_KeepsOtherKeysAndNotes_AndClears()
    {
        using TempDataDir dir = new();
        MemoryFolders folders = new() { Team = Team() with { Manager = "Ada", Notes = "Keep me.\n", UnknownLines = ["custom: 'x'"] } };
        NeedsYouLevels levels = Build(dir, folders);

        Assert.True(levels.SetTeam("Dev", NeedsYouLevel.RunInsideTheTeam, 4m));
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, folders.Team?.NeedsYouLevel);
        Assert.Equal(4m, folders.Team?.SpendCap);
        Assert.Equal("Ada", folders.Team?.Manager);
        Assert.Equal("Keep me.\n", folders.Team?.Notes);
        Assert.Equal(["custom: 'x'"], folders.Team?.UnknownLines);

        Assert.True(levels.SetTeam("Dev", null, 4m));
        Assert.Null(folders.Team?.NeedsYouLevel);
        Assert.Null(folders.Team?.SpendCap);
    }

    /// <summary>Setting a Project level writes it and keeps the avatar, other keys and notes; a cap of zero is stored as none.</summary>
    [Fact]
    public void SetProject_WritesLevel_KeepsTheRest_AndStoresNoZeroCap()
    {
        using TempDataDir dir = new();
        MemoryFolders folders = new() { Team = Team(), Projects = { ["Site"] = ProjectFile.Parse("---\navatarLabel: 'SI'\nowner: 'Ada'\n---\nNotes\n") } };
        NeedsYouLevels levels = Build(dir, folders);

        Assert.True(levels.SetProject("Dev", "Site", NeedsYouLevel.RunInsideTheTeam, 0m));

        ProjectFile written = folders.Projects["Site"];
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, written.NeedsYouLevel);
        Assert.Null(written.SpendCap);
        Assert.Equal("SI", written.Avatar.Label);
        Assert.Equal(["owner: 'Ada'"], written.UnknownLines);
        Assert.Equal("Notes\n", written.Notes);
        Assert.Equal(NeedsYouScope.Project, levels.For("Dev", "Site").Scope);
    }

    /// <summary>A setter returns <see langword="false"/> and raises nothing when there is no file to write.</summary>
    [Fact]
    public void Setters_NoFile_ReturnFalseAndRaiseNothing()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders());
        int raised = 0;
        levels.Changed += () => raised++;

        Assert.False(levels.SetTeam("Dev", NeedsYouLevel.AskMe, null));
        Assert.False(levels.SetProject("Dev", "Site", NeedsYouLevel.AskMe, null));
        Assert.Equal(0, raised);
    }

    /// <summary>Every successful setter raises <see cref="NeedsYouLevels.Changed"/>, and a subscriber that throws does not stop the others or the setter.</summary>
    [Fact]
    public void Setters_RaiseChanged_EvenWhenASubscriberThrows()
    {
        using TempDataDir dir = new();
        NeedsYouLevels levels = Build(dir, new MemoryFolders { Team = Team(), Projects = { ["Site"] = ProjectFile.Empty } });
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
        Assert.Null(first.Get());

        first.Set(NeedsYouLevel.RunInsideTheTeam, 7.25m);
        NeedsYouLevelStore second = new(dir.Options());
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.RunInsideTheTeam, 7.25m), second.Get());

        second.Set(NeedsYouLevel.AskMe, null);
        Assert.Equal(new SavedNeedsYouLevel(NeedsYouLevel.AskMe, null), first.Get());
    }

    /// <summary>An in-memory <see cref="ITeamFolders"/> holding one Team file and Project files by name.</summary>
    private sealed class MemoryFolders : ITeamFolders
    {
        public TeamFile? Team { get; set; }

        public Dictionary<string, ProjectFile> Projects { get; } = new(StringComparer.OrdinalIgnoreCase);

        public LibraryResult<LibraryPath> EnsureTeam(string name) => throw new NotSupportedException();

        public LibraryResult<LibraryPath> FindTeam(string name) => throw new NotSupportedException();

        public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => throw new NotSupportedException();

        public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false) => throw new NotSupportedException();

        public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change)
        {
            if (this.Team is null)
            {
                return false;
            }

            this.Team = change(this.Team);
            return true;
        }

        public TeamFile? ReadTeamFile(string team) => this.Team;

        public ProjectFile? ReadProjectFile(string team, string project) => this.Projects.GetValueOrDefault(project);

        public bool UpdateProjectFile(string team, string project, Func<ProjectFile, ProjectFile> change)
        {
            if (!this.Projects.TryGetValue(project, out ProjectFile? current))
            {
                return false;
            }

            this.Projects[project] = change(current);
            return true;
        }
    }
}
