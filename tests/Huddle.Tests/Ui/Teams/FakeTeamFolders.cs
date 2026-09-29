using Agency.Huddle.App.Library;

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
    public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project)
    {
        lock (this.gate)
        {
            this.calls.Add($"EnsureProjectIn:{team}/{project}");
        }

        return this.ProjectResult;
    }
}
