using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Archiving records the Team's manager so unarchive can bring the manager back (Task 111).</summary>
public sealed class ArchiveManagerTests
{
    /// <summary>The archived <c>_team.md</c> carries <c>archivedManager: 'Ada'</c> and no live <c>manager</c>.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_Manager_WrittenAsArchivedManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);

        _ = await f.Service.ArchiveTeamAsync("Home", ct);

        string text = File.ReadAllText(Path.Combine(f.ArchivedFolder, TeamPaths.TeamFileName));
        // The fixture catalog gives Home the manager Ada.
        Assert.Contains("archivedManager: 'Ada'", text, StringComparison.Ordinal);
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        Assert.Null(file.Manager);
    }
}
