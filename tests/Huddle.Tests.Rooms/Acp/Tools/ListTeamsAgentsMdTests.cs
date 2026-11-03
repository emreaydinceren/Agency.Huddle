using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins Spec §6.2 rule 3 for <see cref="ListTeamsTool"/> (Task 62): a Team's AGENTS.md is prose the Human wrote for that Team,
/// so <c>list_teams</c> shows the card's <c>owns</c> line and never the text of the file.
/// </summary>
public sealed class ListTeamsAgentsMdTests
{
    /// <summary>list_teams returns a Team's card fields and never the text of its AGENTS.md.</summary>
    [Fact]
    public async Task ListTeams_TeamHasAgentsMd_OmitsItsText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string teamFolder = Path.Combine(harness.Store.RootDirectory, "Home");
        _ = Directory.CreateDirectory(teamFolder);
        await File.WriteAllTextAsync(Path.Combine(teamFolder, TeamPaths.AgentsMdFileName), "SECRET RULE", ct);
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Home", [], ["Nova"], true) { Manager = "Nova", Owns = "The house" }],
        };
        ListTeamsTool tool = new(catalog, harness.Store, harness.Personas, harness.Directory, harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."), new FakePromptSource());

        string result = await tool.InvokeAsync([], ct);

        Assert.Contains("The house", result, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET RULE", result, StringComparison.Ordinal);
    }
}
