using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Seeder.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>The software-co seed splits a template the way <see cref="ProcessProvisioner"/> does: prose to AGENTS.md, settings to <c>_team.md</c>.</summary>
public sealed class SeedSettingsTests
{
    /// <summary>A typed seeded Team's _team.md carries the template's id and gates, and its AGENTS.md is the template body with no frontmatter.</summary>
    [Fact]
    public async Task Seed_TypedTeam_SplitsTheTemplateIntoAgentsMdAndTeamFile()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedTeam team = seed.Summary.Plan.Teams.First(static t => t.TeamType is not null);
        string folder = Path.Combine(seed.DataDir, "Teams", team.Name);
        TeamTypeCatalog types = new(seed.Options(), NullLogger<TeamTypeCatalog>.Instance);
        Assert.True(VersionedRef.TryParse(team.TeamType, out VersionedRef? type));
        TeamTypeManifest manifest = types.Find(type.Id)!;
        ProcessRef chosen = manifest.Processes.FirstOrDefault(static p => p.Default) ?? manifest.Processes[0];
        ProcessSettings expected = ProcessSettings.Split(types.ReadProcess(type.Id, chosen.Id)!, out string body);

        Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(folder, TeamPaths.TeamFileName)), out TeamFile? file));
        string agents = File.ReadAllText(Path.Combine(folder, TeamPaths.AgentsMdFileName));

        Assert.NotNull(expected.Template);
        Assert.Equal(expected.Template, file.Process.Template);
        Assert.Equal(expected.Gates, file.Process.Gates);
        Assert.False(agents.StartsWith("---", StringComparison.Ordinal));
        Assert.StartsWith(body.Split('\n')[0], agents, StringComparison.Ordinal);
    }
}
