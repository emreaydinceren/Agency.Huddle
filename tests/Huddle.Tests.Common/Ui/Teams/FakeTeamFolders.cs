using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// A hand-driven <see cref="ITeamFolders"/> for UI tests: it records every call and answers with a
/// result the test sets. The default result is a success carrying no path
/// (<c>new LibraryResult&lt;LibraryPath&gt;(null, null)</c> counts as success because
/// <c>Succeeded</c> is <c>Error is null</c>), so a caller must branch on <c>Succeeded</c> and never read
/// <c>Value</c>.
/// </summary>
internal sealed class FakeTeamFolders : ITeamFolders
{
    private readonly Lock gate = new();
    private readonly List<string> calls = [];

    /// <summary>What <see cref="EnsureTeam"/> returns. Set it before the code under test calls.</summary>
    public LibraryResult<LibraryPath> TeamResult { get; set; } = new(null, null);

    /// <summary>What <see cref="EnsureProjectIn"/> returns. Set it before the code under test calls.</summary>
    public LibraryResult<LibraryPath> ProjectResult { get; set; } = new(null, null);

    /// <summary>A snapshot of the calls so far, in order: <c>EnsureTeam:{name}</c> or <c>EnsureProjectIn:{team}/{project}</c>.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc />
    public LibraryResult<LibraryPath> EnsureTeam(string name)
    {
        lock (this.gate)
        {
            this.calls.Add($"EnsureTeam:{name}");
        }

        return this.TeamResult;
    }

    /// <inheritdoc />
    public LibraryResult<LibraryPath> FindTeam(string name) => this.TeamResult;

    /// <inheritdoc />
    public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project)
    {
        lock (this.gate)
        {
            this.calls.Add($"EnsureProjectIn:{team}/{project}");
        }

        return this.ProjectResult;
    }

    /// <summary>What <see cref="CreateTeam"/> returns. Set it before the code under test calls.</summary>
    public TeamCreateResult CreateTeamResult { get; set; } = new(TeamCreateOutcome.Created, null);

    /// <summary>What <see cref="ArchivedTeamNames"/> returns. Set it before the code under test calls.</summary>
    public IReadOnlyList<string> ArchivedNames { get; set; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> ArchivedTeamNames() => this.ArchivedNames;

    /// <summary>What <see cref="UpdateTeamFile"/> returns. Set it before the code under test calls.</summary>
    public bool UpdateResult { get; set; }

    /// <summary>What <see cref="ReadTeamFile"/> returns. Set it before the code under test calls.</summary>
    public TeamFile? ReadResult { get; set; }

    /// <inheritdoc />
    public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false)
    {
        lock (this.gate)
        {
            this.calls.Add($"CreateTeam:{team}");
        }

        return this.CreateTeamResult;
    }

    /// <inheritdoc />
    /// <remarks>A successful update applies <paramref name="change"/> to <see cref="ReadResult"/> when one is set, so a test can read back what was written.</remarks>
    public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change)
    {
        lock (this.gate)
        {
            this.calls.Add($"UpdateTeamFile:{team}");
            if (this.UpdateResult && this.ReadResult is { } current)
            {
                this.ReadResult = change(current);
            }
        }

        return this.UpdateResult;
    }

    /// <summary>Every avatar <see cref="UpdateProjectAvatar"/> was given, with its Team and Project, in call order.</summary>
    public List<(string Team, string Project, Avatar Avatar)> ProjectAvatarWrites { get; } = [];

    /// <inheritdoc />
    /// <remarks>Records the write and returns <see cref="UpdateResult"/>.</remarks>
    public bool UpdateProjectAvatar(string team, string project, Avatar avatar)
    {
        lock (this.gate)
        {
            this.calls.Add($"UpdateProjectAvatar:{team}/{project}");
            this.ProjectAvatarWrites.Add((team, project, avatar));
        }

        return this.UpdateResult;
    }

    /// <inheritdoc />
    public TeamFile? ReadTeamFile(string team)
    {
        lock (this.gate)
        {
            this.calls.Add($"ReadTeamFile:{team}");
        }

        return this.ReadResult;
    }
}
