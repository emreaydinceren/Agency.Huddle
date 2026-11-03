using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins Spec §6.7's collision rules (Task 114, corrections-12 items 10 and 4): a Team name or a Project name that
/// an archived one already holds is refused, matched ignoring case, and archiving a Team whose archived twin exists
/// is refused. "Archived Team" means <c>Archive/&lt;T&gt;/_team.md</c> exists.
/// </summary>
public sealed class ArchiveNameCollisionTests
{
    private static TeamSummary Home(params string[] projects) => new("Home", [.. projects], [], HasFolder: true);

    /// <summary>A name that differs only by case from an archived Team is refused with the archived spelling in the text.</summary>
    [Fact]
    public void ValidateTeamName_ArchivedNameDifferingByCase_RefusedWithArchivedSpelling()
    {
        string? result = TeamNames.ValidateTeamName("home", [], ["Home"]);

        // The typed "home" matches the archived "Home" ignoring case; the text names the archived spelling.
        Assert.Equal("A Team named Home is archived. Unarchive it or pick another name.", result);
    }

    /// <summary>A name no archived Team holds is accepted.</summary>
    [Fact]
    public void ValidateTeamName_NameNotArchived_ReturnsNull()
    {
        string? result = TeamNames.ValidateTeamName("Work", [], ["Home"]);

        Assert.Null(result);
    }

    /// <summary>A Project name that an archived Project of the Team holds, ignoring case, is refused.</summary>
    [Fact]
    public void ValidateProjectName_ArchivedProjectDifferingByCase_Refused()
    {
        string? result = TeamNames.ValidateProjectName("old", Home(), ["Old"]);

        Assert.Equal("A Project named Old is archived in Home. Unarchive it or pick another name.", result);
    }

    /// <summary>A Project name no archived Project holds is accepted.</summary>
    [Fact]
    public void ValidateProjectName_NameNotArchived_ReturnsNull()
    {
        string? result = TeamNames.ValidateProjectName("Fresh", Home(), ["Old"]);

        Assert.Null(result);
    }

    /// <summary>Archiving <c>Home</c> while <c>Archive/HOME/_team.md</c> exists is refused and the live folder is untouched.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_ArchivedTwinDifferingByCase_RefusedUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        string twin = Path.Combine(f.ArchiveRoot, "HOME");
        Directory.CreateDirectory(twin);
        File.WriteAllText(Path.Combine(twin, TeamPaths.TeamFileName), "---\n---\n");
        Dictionary<string, string> before = ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder);

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync("Home", ct);

        Assert.False(outcome.Done);
        Assert.Contains("already archived", outcome.Refusal, StringComparison.Ordinal);
        Assert.Equal(before, ArchiveTeamTests.Fixture.Snapshot(f.TeamFolder));
        Assert.Empty(f.Membership.Calls);
    }

    /// <summary>A Project archived on its own (<c>Archive/Home/Old/</c>, no <c>_team.md</c>) does not make the Team archived: archiving the live Team merges into it.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_OnlyAProjectArchived_StillArchivesTheTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        Directory.CreateDirectory(Path.Combine(f.ArchiveRoot, "Home", "Old"));

        ArchiveOutcome outcome = await f.Service.ArchiveTeamAsync("Home", ct);

        Assert.True(outcome.Done);
        Assert.True(File.Exists(Path.Combine(f.ArchiveRoot, "Home", TeamPaths.TeamFileName)));
    }
}
