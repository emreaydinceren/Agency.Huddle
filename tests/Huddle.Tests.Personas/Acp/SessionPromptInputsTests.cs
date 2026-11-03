using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins that a session and the "What this Agent reads" preview build their prompt inputs in one place (Spec §6.5, D-18):
/// <see cref="SessionPromptInputs.Build"/> returns the Memory, Team Memory and AGENTS.md snapshots that
/// <c>DotAcpPersonaHost</c> has always composed from.
/// </summary>
public sealed class SessionPromptInputsTests : IDisposable
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-promptinputs-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary data folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Composes Nova's prompt from the three snapshots the way the host passes them.</summary>
    /// <param name="memory">Nova's Memory.</param>
    /// <param name="teamMemory">Nova's Team Memory, or null.</param>
    /// <param name="agentsMd">The AGENTS.md files.</param>
    private static string Compose(MemorySnapshot memory, TeamMemorySnapshot? teamMemory, AgentsMdSnapshot agentsMd) =>
        SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory,
            SessionScope.Shared,
            teamMemory: teamMemory,
            agentsMd: agentsMd);

    /// <summary>
    /// A Persona in Team Business with a Huddle-wide and a Team AGENTS.md, one Memory file and one Team Memory file gets all three
    /// snapshots back, and the prompt composed from them equals the one composed from the builders the host called before the extraction.
    /// </summary>
    [Fact]
    public void Build_PersonaInTeam_ReturnsTheSnapshotsTheHostComposesFrom()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        string memoryDir = Path.Combine(this.root, "Agents", "nova", "memory");
        string business = Path.Combine(teamsRoot, "Business");
        Directory.CreateDirectory(Path.Combine(business, "memory"));
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(teamsRoot, "AGENTS.md"), "Huddle rule.");
        File.WriteAllText(Path.Combine(business, "AGENTS.md"), "Business rule.");
        File.WriteAllText(Path.Combine(memoryDir, "language.md"), "# The Human prefers C#.\n\nSaid in Room 'Alpha'.");
        File.WriteAllText(Path.Combine(business, "memory", "norms.md"), "# Ship small.\n\nTeam norm.");
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);
        IReadOnlyList<TeamSummary> teams = [new TeamSummary("Business", [], ["nova"], true)];

        SessionPromptSnapshot actual = SessionPromptInputs.Build(reader, warnings, teams, "nova", memoryDir, 50, ["business"], teamsRoot, 50, maxHuddleMemoryEntries: 30);

        // The reference is what ComposeSystemPrompt built inline: MemoryIndex, TeamMemoryIndex and the reader, with the label resolved to the catalog's spelling.
        var (entries, notListed) = MemoryIndex.Build(memoryDir, 50, warnings);
        MemorySnapshot expectedMemory = new(memoryDir, entries, notListed);
        TeamMemorySnapshot expectedTeam = TeamMemoryIndex.Build(teamsRoot, ["business"], teams, 50, "nova", warnings);
        AgentsMdSnapshot expectedAgents = reader.Read(teamsRoot, ["Business"]);

        Assert.Single(actual.Memory.Entries);
        Assert.NotNull(actual.TeamMemory);
        Assert.Single(actual.TeamMemory.Groups);
        Assert.Equal(2, actual.AgentsMd.Sections.Count); // Huddle-wide and Team Business; no Project folders exist.
        Assert.Equal(
            Compose(expectedMemory, expectedTeam, expectedAgents),
            Compose(actual.Memory, actual.TeamMemory, actual.AgentsMd));
    }

    /// <summary>A Persona in no Team gets no Team Memory snapshot and no Team AGENTS.md section.</summary>
    [Fact]
    public void Build_PersonaInNoTeam_HasNoTeamMemory()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        string memoryDir = Path.Combine(this.root, "Agents", "nova", "memory");
        Directory.CreateDirectory(teamsRoot);
        Directory.CreateDirectory(memoryDir);
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);

        SessionPromptSnapshot actual = SessionPromptInputs.Build(reader, warnings, [], "nova", memoryDir, 50, [], teamsRoot, 50, maxHuddleMemoryEntries: 30);

        Assert.Null(actual.TeamMemory);
        Assert.Empty(actual.Memory.Entries);
        Assert.Empty(actual.AgentsMd.Sections);
    }
}
