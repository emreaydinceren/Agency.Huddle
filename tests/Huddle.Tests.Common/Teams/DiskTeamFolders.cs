using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>An <see cref="ITeamFolders"/> that serves one Team folder on disk and keeps its <c>_team.md</c> in memory.</summary>
internal sealed class DiskTeamFolders(string folder) : ITeamFolders
{
    /// <summary>Gets or sets the Team file; <see langword="null"/> means the Team has none.</summary>
    public TeamFile? File { get; set; } = new(new VersionedRef("web", "1.2.0"), null, null, null, string.Empty);

    /// <inheritdoc />
    public LibraryResult<LibraryPath> EnsureTeam(string name)
    {
        LibraryRoot root = new("teams", "Teams", folder, LibraryRootKind.Teams);
        return LibraryResult<LibraryPath>.Ok(new LibraryPath(root, name, folder, LibraryNodeRole.TeamFolder));
    }

    /// <inheritdoc />
    public LibraryResult<LibraryPath> FindTeam(string name) => this.EnsureTeam(name);

    /// <inheritdoc />
    public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => this.EnsureTeam(team);

    /// <inheritdoc />
    public TeamCreateResult CreateTeam(string team, TeamFile file) => new(TeamCreateOutcome.AlreadyExists, null);

    /// <summary>Gets or sets a path the composed Team file is also written to on every update; <see langword="null"/> keeps it in memory only.</summary>
    public string? TeamFilePath { get; set; }

    /// <inheritdoc />
    public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change)
    {
        if (this.File is null)
        {
            return false;
        }

        this.File = change(this.File);
        if (this.TeamFilePath is not null)
        {
            System.IO.File.WriteAllText(this.TeamFilePath, this.File.Compose());
        }

        return true;
    }

    /// <inheritdoc />
    public TeamFile? ReadTeamFile(string team) => this.File;
}
