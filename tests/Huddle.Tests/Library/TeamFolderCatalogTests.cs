using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="TeamFolderCatalog.List"/> (Spec §6.2 first paragraph, Task 7.1,
/// corrections-B4 item 32): it is built on <see cref="TaskStore.Teams"/>, so the Library and the
/// board can never disagree about orphans.
/// </summary>
public sealed class TeamFolderCatalogTests
{
    /// <summary>A folder whose name matches a Persona's Team label only by case is not an orphan,
    /// and the listed name keeps the folder's own spelling.</summary>
    [Fact]
    public void List_MatchesLabelsCaseInsensitively()
    {
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "marketing"));
        fixture.WritePersonaWithTeam("coo", "Marketing");

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        LibraryTeamFolder folder = Assert.Single(folders);
        Assert.Equal("marketing", folder.Name, StringComparer.Ordinal);
        Assert.False(folder.IsOrphan);
    }

    /// <summary>A Team folder with no Persona naming it in <c>teams</c> is listed and marked as an orphan.</summary>
    [Fact]
    public void List_FolderWithoutLabel_IsOrphan()
    {
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "Ops"));

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        LibraryTeamFolder folder = Assert.Single(folders);
        Assert.Equal("Ops", folder.Name, StringComparer.Ordinal);
        Assert.True(folder.IsOrphan);
    }

    /// <summary>Only non-underscore sub-folders of a Team folder are listed as Projects: <c>_tasks</c>
    /// is excluded, an ordinary sub-folder is included.</summary>
    [Fact]
    public void List_Projects_AreNonUnderscoreSubfolders()
    {
        using Fixture fixture = Fixture.Build();
        string team = Path.Combine(fixture.TeamsPath, "Marketing");
        Directory.CreateDirectory(Path.Combine(team, "Launch Q4"));
        Directory.CreateDirectory(Path.Combine(team, "_tasks"));

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        LibraryTeamFolder folder = Assert.Single(folders);
        Assert.Equal(["Launch Q4"], folder.Projects);
    }

    /// <summary>Both reserved prefixes are skipped as Team folders (<c>_</c> and <c>.</c>), while a
    /// name that merely contains an underscore after its first character is an allowed neighbour.</summary>
    [Fact]
    public void List_ReservedPrefixes_AreSkipped_UnderscoreInsideNameIsAllowed()
    {
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "_hidden"));
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, ".hidden"));
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "x_y"));

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        LibraryTeamFolder folder = Assert.Single(folders);
        Assert.Equal("x_y", folder.Name, StringComparer.Ordinal);
    }

    /// <summary>The listing is ordered ordinal-ignore-case by folder name, not disk or creation order.</summary>
    [Fact]
    public void List_OrderIsOrdinalIgnoreCase()
    {
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "bravo"));
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "Alpha"));
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "charlie"));

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        Assert.Equal(["Alpha", "bravo", "charlie"], [.. folders.Select(f => f.Name)]);
    }

    /// <summary>A missing Teams root produces an empty listing, not a thrown exception.</summary>
    [Fact]
    public void List_MissingRoot_IsEmpty()
    {
        using Fixture fixture = Fixture.Build();
        Directory.Delete(fixture.TeamsPath);

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        Assert.Empty(folders);
    }

    /// <summary>A Persona naming a Team with no matching on-disk folder is not listed: creating the
    /// folder is the provisioner's job, not the catalog's (Task 7.1.t).</summary>
    [Fact]
    public void List_LabelWithoutFolder_IsNotListed()
    {
        using Fixture fixture = Fixture.Build();
        fixture.WritePersonaWithTeam("coo", "Marketing");

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

        Assert.Empty(folders);
    }

    /// <summary>A Team folder that is a junction pointing outside the Teams root is skipped: the
    /// resolver refuses it (corrections-B4 item 32), and the catalog does not surface it.</summary>
    [Fact]
    public void List_JunctionOutsideRoot_IsSkipped()
    {
        using Fixture fixture = Fixture.Build();
        string outside = Path.Combine(Path.GetTempPath(), $"team-folder-catalog-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        string link = Path.Combine(fixture.TeamsPath, "Escaped");

        try
        {
            if (!TestLinks.TryCreateLink(link, outside))
            {
                Assert.Skip("Could not create a directory link on this machine.");
                return;
            }

            IReadOnlyList<LibraryTeamFolder> folders = fixture.List();

            Assert.Empty(folders);
        }
        finally
        {
            TestLinks.RemoveLink(link);
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>The catalog's result agrees with <see cref="TaskStore.Teams"/> for the same folder
    /// set: same names, same orphan flags, same Projects (corrections-B4 item 32).</summary>
    [Fact]
    public void List_AgreesWithTaskStoreTeams()
    {
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "Marketing", "Launch Q4"));
        Directory.CreateDirectory(Path.Combine(fixture.TeamsPath, "Ops"));
        fixture.WritePersonaWithTeam("coo", "Marketing");

        IReadOnlyList<LibraryTeamFolder> folders = fixture.List();
        IReadOnlyList<TeamFolder> boardTeams = fixture.TaskStore.Teams;

        Assert.Equal(boardTeams.Count, folders.Count);
        foreach (TeamFolder team in boardTeams)
        {
            LibraryTeamFolder match = Assert.Single(folders, f => string.Equals(f.Name, team.Name, StringComparison.Ordinal));
            Assert.Equal(team.IsOrphan, match.IsOrphan);
            Assert.Equal(team.Projects, match.Projects);
        }
    }

    /// <summary>An isolated fixture over a temp <c>DataDir</c>: a real <see cref="TaskStore"/> and
    /// <see cref="PersonaStore"/> for the board's view, plus a real <see cref="LibraryPathResolver"/>
    /// for the catalog under test.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;
        private readonly TeammatePaths paths;

        private Fixture(TempDataDir dir, PersonaStore personas, TeammatePaths paths, LibraryPathResolver resolver, LibraryPath teamsRoot)
        {
            this.dir = dir;
            this.personas = personas;
            this.paths = paths;
            this.Resolver = resolver;
            this.TeamsRoot = teamsRoot;
            this.TaskStore = TestTaskStore.CreateTaskStore(dir, personas);
        }

        /// <summary>The Task store, rebuilt by <see cref="List"/> so it scans the folders arranged
        /// on disk after this fixture was built (its own constructor scans once, at construction).</summary>
        public TaskStore TaskStore { get; private set; }

        /// <summary>The resolver under test.</summary>
        public LibraryPathResolver Resolver { get; }

        /// <summary>The resolved Teams root, as an already-resolved <see cref="LibraryPath"/>.</summary>
        public LibraryPath TeamsRoot { get; }

        /// <summary>The Teams scan root's absolute path on disk.</summary>
        public string TeamsPath => this.TeamsRoot.FullPath;

        /// <summary>Builds a fixture with an empty <c>Teams/</c> and <c>Teammates/</c> layout under a fresh temp <c>DataDir</c>.</summary>
        public static Fixture Build()
        {
            TempDataDir dir = new();
            Directory.CreateDirectory(Path.Combine(dir.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(dir.Path, "Teammates"));

            IOptions<TeamOptions> options = dir.Options();
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

            TeammatePaths teammatePaths = new(options);
            LibraryRootStore rootStore = new(options, teammatePaths, NullLogger<LibraryRootStore>.Instance);
            LibraryPathResolver resolver = new(rootStore, options, NullLogger<LibraryPathResolver>.Instance);
            Assert.True(resolver.TryResolve("teams", string.Empty, out LibraryPath? teamsRoot, out _));
            Assert.NotNull(teamsRoot);

            return new Fixture(dir, personas, teammatePaths, resolver, teamsRoot);
        }

        /// <summary>Writes a minimal Persona definition naming <paramref name="team"/> in its <c>teams</c> frontmatter field.</summary>
        /// <param name="stem">The Persona's file stem (its Alias).</param>
        /// <param name="team">The Team label to write.</param>
        public void WritePersonaWithTeam(string stem, string team)
        {
            string text = $"---\nName: {stem}\nTitle: {stem}\nAlias: {stem}\nteams: [{team}]\n---\nbody";
            TestPersonaFiles.Write(this.paths, stem, text);
            this.personas.RescanNow();
        }

        /// <summary>Rescans <see cref="TaskStore"/> (so it reflects any folders arranged on disk since
        /// this fixture was built), then calls <see cref="TeamFolderCatalog.List"/> with it and this
        /// fixture's resolver.</summary>
        public IReadOnlyList<LibraryTeamFolder> List()
        {
            this.TaskStore = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            return TeamFolderCatalog.List(this.Resolver, this.TeamsRoot, this.TaskStore.Teams);
        }

        /// <summary>Disposes the temp <c>DataDir</c>.</summary>
        public void Dispose() => this.dir.Dispose();
    }
}
