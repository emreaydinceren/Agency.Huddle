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
/// Pins the re-run of a case-only <see cref="TeamMembership.RenameLabel"/>: when every label already says the new name
/// spelled exactly, the call edits nothing and raises nothing (a recovery pass must not churn the Persona files).
/// </summary>
public sealed class TeamMembershipCaseOnlyRerunTests
{
    /// <summary>A second Growth-to-growth rename, after the labels already say growth, leaves the file and raises no PersonasChanged.</summary>
    [Fact]
    public void RenameLabel_LabelsAlreadyNewSpelling_NoEditAndNoEvent()
    {
        string text = string.Join('\n', "---", "Name: Ada", "Title: Ada", "Alias: ada", "teams: ['growth']", "---", "You are Ada.");
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        AgentPaths paths = new(options);
        TestPersonaFiles.Write(paths, "ada", text);
        using PersonaStore personas = new(paths, new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
        TeamMembership membership = new(personas, new FakeTeamCatalog(), new UnusedFolders(), RoleFixtures.Types(dir));
        int raised = 0;
        void OnChanged() => Interlocked.Increment(ref raised);
        personas.PersonasChanged += OnChanged;
        try
        {
            membership.RenameLabel("Growth", "growth");
        }
        finally
        {
            personas.PersonasChanged -= OnChanged;
        }

        Assert.Equal(0, raised);
        Assert.Equal(text, File.ReadAllText(paths.DefinitionFile("ada")));
    }

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
}
