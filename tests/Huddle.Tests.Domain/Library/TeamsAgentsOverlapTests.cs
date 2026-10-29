using Agency.Huddle.App;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins the start-up guard that rejects a <c>Team:Teams:Dir</c> and <c>Team:Acp:AgentsDir</c>
/// that overlap (Spec §6.3, fourth bullet; ADR-0030 Consequences).
/// </summary>
public sealed class TeamsAgentsOverlapTests
{
    /// <summary>Teams nested inside Agents throws.</summary>
    [Fact]
    public void Validate_TeamsInsideAgents_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Acp.AgentsDir = "Agents";
        options.Teams.Dir = Path.Combine("Agents", "Teams");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options));

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.AgentsDir)));
        Assert.Equal($"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }

    /// <summary>Agents nested inside Teams throws.</summary>
    [Fact]
    public void Validate_AgentsInsideTeams_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams";
        options.Acp.AgentsDir = Path.Combine("Teams", "Agents");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options));

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.AgentsDir)));
        Assert.Equal($"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }

    /// <summary>The same folder for both throws.</summary>
    [Fact]
    public void Validate_Same_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Shared";
        options.Acp.AgentsDir = "Shared";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options));

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.AgentsDir)));
        Assert.Equal($"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }

    /// <summary>Sibling folders under DataDir pass without throwing.</summary>
    [Fact]
    public void Validate_Siblings_Passes()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams";
        options.Acp.AgentsDir = "Agents";

        Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options);
    }

    /// <summary>A trailing separator on one side must not defeat the equality check.</summary>
    [Fact]
    public void Validate_SameWithTrailingSeparator_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Teams/";
        options.Acp.AgentsDir = "Teams";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options));

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.AgentsDir)));
        Assert.Equal($"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }

    /// <summary>A `..` segment that lands inside Agents must not escape the nesting check.</summary>
    [Fact]
    public void Validate_DotDotIntoAgents_Throws()
    {
        TeamOptions options = new()
        {
            DataDir = Path.Combine(Path.GetTempPath(), "TeamsAgentsOverlapTests", Guid.NewGuid().ToString("N")),
        };
        options.Teams.Dir = "Agents/../Agents/sub";
        options.Acp.AgentsDir = "Agents";

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents(options));

        string teamsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Teams.Dir)));
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(options.DataDir, options.Acp.AgentsDir)));
        Assert.Equal($"'Team:Teams:Dir' ({teamsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }
}
