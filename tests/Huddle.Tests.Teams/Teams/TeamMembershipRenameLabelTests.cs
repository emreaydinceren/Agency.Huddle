using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Agents;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamMembership.RenameLabel"/> (Team Rename Spec §5.3 step 7): every Persona whose
/// <c>teams</c> line holds the old label gets the new one in a single <c>PersonasChanged</c>, and no
/// other Persona or label is touched. Definitions are seeded before the store is built, and the event
/// count unsubscribes the instant the call returns because the file watcher raises a later rescan.
/// </summary>
public sealed class TeamMembershipRenameLabelTests
{
    /// <summary>Three members of Marketing (one also in Research, one spelling it in lower case) all say Growth, Research stays, and one event is raised.</summary>
    [Fact]
    public void RenameLabel_Members_AllSayTheNewLabelAndRaiseOnce()
    {
        using Fixture f = new([
            ("ada", Definition("Ada", "teams: ['Marketing']")),
            ("bo", Definition("Bo", "teams: ['Marketing', 'Research']")),
            ("cy", Definition("Cy", "teams: ['marketing']"))]);

        int raised = f.Observe(() => f.Membership.RenameLabel("Marketing", "Growth"));

        Assert.Equal(1, raised);
        Assert.Equal(Definition("Ada", "teams: ['Growth']"), f.Read("ada"));
        Assert.Equal(Definition("Bo", "teams: ['Growth', 'Research']"), f.Read("bo"));
        Assert.Equal(Definition("Cy", "teams: ['Growth']"), f.Read("cy"));
    }

    /// <summary>A Persona that is not in the Team keeps its file byte for byte.</summary>
    [Fact]
    public void RenameLabel_NonMember_IsUntouched()
    {
        string ops = Definition("Dee", "teams: ['Ops']");
        using Fixture f = new([
            ("ada", Definition("Ada", "teams: ['Marketing']")),
            ("dee", ops)]);

        f.Membership.RenameLabel("Marketing", "Growth");

        Assert.Equal(ops, f.Read("dee"));
        Assert.Equal(Definition("Ada", "teams: ['Growth']"), f.Read("ada"));
    }

    /// <summary>Builds a Persona definition with one extra frontmatter line and a one-line body, LF line endings.</summary>
    /// <param name="name">The Persona's Name.</param>
    /// <param name="line">The extra frontmatter line.</param>
    private static string Definition(string name, string line) =>
        string.Join('\n', "---", $"Name: {name}", $"Title: {name}", $"Alias: {name.ToLowerInvariant()}", line, "---", $"You are {name}.");

    /// <summary>An <see cref="ITeamFolders"/> that no membership rename touches.</summary>
    private sealed class UnusedFolders : ITeamFolders
    {
        /// <inheritdoc />
        public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change) => throw new NotSupportedException();

        /// <inheritdoc />
        public TeamFile? ReadTeamFile(string team) => throw new NotSupportedException();

        /// <inheritdoc />
        public LibraryResult<LibraryPath> EnsureTeam(string name) => throw new NotSupportedException();

        /// <inheritdoc />
        public LibraryResult<LibraryPath> FindTeam(string name) => throw new NotSupportedException();

        /// <inheritdoc />
        public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => throw new NotSupportedException();

        /// <inheritdoc />
        public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false) => throw new NotSupportedException();
    }

    /// <summary>A temporary data directory with a real <see cref="PersonaStore"/> seeded before it starts, and the service under test.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly AgentPaths paths;
        private readonly PersonaStore personas;

        /// <summary>Seeds every definition, THEN constructs the store.</summary>
        /// <param name="files">The definition files as (stem, text) pairs.</param>
        public Fixture(IReadOnlyList<(string Stem, string Text)> files)
        {
            IOptions<TeamOptions> options = this.dir.Options();
            this.paths = new AgentPaths(options);
            foreach ((string stem, string text) in files)
            {
                TestPersonaFiles.Write(this.paths, stem, text);
            }

            this.personas = new PersonaStore(this.paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            this.Membership = new TeamMembership(this.personas, new FakeTeamCatalog(), new UnusedFolders(), RoleFixtures.Types(this.dir));
        }

        /// <summary>The service under test.</summary>
        public TeamMembership Membership { get; }

        /// <summary>Reads a definition file's text as it is on disk.</summary>
        /// <param name="stem">The Persona's file stem.</param>
        public string Read(string stem) => File.ReadAllText(this.paths.DefinitionFile(stem));

        /// <summary>Runs <paramref name="call"/> and counts <c>PersonasChanged</c> raised until it returns, then unsubscribes at once.</summary>
        /// <param name="call">The call to observe.</param>
        public int Observe(Action call)
        {
            int raised = 0;
            void OnChanged() => Interlocked.Increment(ref raised);
            this.personas.PersonasChanged += OnChanged;
            try
            {
                call();
            }
            finally
            {
                this.personas.PersonasChanged -= OnChanged;
            }

            return raised;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }
}
