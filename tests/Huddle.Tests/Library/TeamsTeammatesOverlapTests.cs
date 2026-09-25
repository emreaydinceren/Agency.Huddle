using Agency.Huddle.App;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins the start-up guard that rejects a <c>Team:Teams:Dir</c> and <c>Team:Acp:TeammatesDir</c>
/// that overlap (Spec §6.3, fourth bullet; ADR-0030 Consequences).
/// </summary>
public sealed class TeamsTeammatesOverlapTests
{
    /// <summary>Teams nested inside Teammates throws.</summary>
    [Fact]
    public void Validate_TeamsInsideTeammates_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Acp.TeammatesDir = "Teammates";
        options.Teams.Dir = Path.Combine("Teammates", "Teams");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options));

        Assert.Contains("Team:Teams:Dir", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Team:Acp:TeammatesDir", ex.Message, StringComparison.Ordinal);
        Assert.Contains("must not overlap", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Teammates nested inside Teams throws.</summary>
    [Fact]
    public void Validate_TeammatesInsideTeams_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams";
        options.Acp.TeammatesDir = Path.Combine("Teams", "Teammates");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options));

        Assert.Contains("Team:Teams:Dir", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Team:Acp:TeammatesDir", ex.Message, StringComparison.Ordinal);
        Assert.Contains("must not overlap", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The same folder for both throws.</summary>
    [Fact]
    public void Validate_Same_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Shared";
        options.Acp.TeammatesDir = "Shared";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options));

        Assert.Contains("must not overlap", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Sibling folders under DataDir pass without throwing.</summary>
    [Fact]
    public void Validate_Siblings_Passes()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams";
        options.Acp.TeammatesDir = "Teammates";

        Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options);
    }

    /// <summary>A trailing separator on one side must not defeat the equality check.</summary>
    [Fact]
    public void Validate_SameWithTrailingSeparator_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams/";
        options.Acp.TeammatesDir = "Teams";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options));

        Assert.Contains("must not overlap", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A `..` segment that lands inside Teammates must not escape the nesting check.</summary>
    [Fact]
    public void Validate_DotDotIntoTeammates_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsTeammatesOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teammates/../Teammates/sub";
        options.Acp.TeammatesDir = "Teammates";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates(options));

        Assert.Contains("must not overlap", ex.Message, StringComparison.Ordinal);
    }
}
