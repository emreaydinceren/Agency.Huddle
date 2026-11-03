using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Microsoft.Extensions.Logging.Abstractions;
using OverCapNotifier = Agency.Huddle.App.SecondBrain.OverCapNotifier;
using SecondBrainOverCapPayload = Agency.Huddle.App.Feed.SecondBrainOverCapPayload;

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

        SessionPromptSnapshot actual = SessionPromptInputs.Build(reader, warnings, teams, "nova", memoryDir, 50, ["business"], teamsRoot, 50, maxHuddleMemoryEntries: 30, maxOutputsEntries: 40);

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

    /// <summary>Task 130 (SB-R15): the snapshot lists <c>(team, path)</c> in the Persona's <c>teams</c> order for a Team whose workspace is a readable folder or repository, and leaves out an unreadable one, a missing one and a Team with none.</summary>
    [Fact]
    public void Build_Workspaces_ListsReadableOnesInTeamsOrder()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        string memoryDir = Path.Combine(this.root, "Agents", "nova", "memory");
        string repo = Path.Combine(this.root, "repo");
        string folder = Path.Combine(this.root, "folder");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(teamsRoot);
        Directory.CreateDirectory(memoryDir);
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);
        IReadOnlyList<TeamSummary> teams =
        [
            new TeamSummary("Alpha", [], ["nova"], true) { Workspace = repo },
            new TeamSummary("Beta", [], ["nova"], true) { Workspace = Path.Combine(this.root, "gone") },
            new TeamSummary("Gamma", [], ["nova"], true),
            new TeamSummary("Delta", [], ["nova"], true) { Workspace = folder },
        ];

        // The label order is Delta, Alpha, Beta, Gamma: the result follows it, minus Beta (missing path) and Gamma (no workspace).
        SessionPromptSnapshot actual = SessionPromptInputs.Build(reader, warnings, teams, "nova", memoryDir, 50, ["Delta", "Alpha", "Beta", "Gamma"], teamsRoot, 50, maxHuddleMemoryEntries: 30, maxOutputsEntries: 40);

        Assert.Equal([("Delta", folder), ("Alpha", repo)], actual.Workspaces);
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

        SessionPromptSnapshot actual = SessionPromptInputs.Build(reader, warnings, [], "nova", memoryDir, 50, [], teamsRoot, 50, maxHuddleMemoryEntries: 30, maxOutputsEntries: 40);

        Assert.Null(actual.TeamMemory);
        Assert.Empty(actual.Memory.Entries);
        Assert.Empty(actual.AgentsMd.Sections);
    }

    /// <summary>
    /// Building session inputs over a Team memory folder and a Huddle-wide memory folder that each hold more files than the cap reports both
    /// folders to the notifier (SB-R13, Spec §6.8): the one place the indexes are built is the one place overflow is noticed.
    /// </summary>
    [Fact]
    public void Build_FoldersOverCap_ReportsEachToTheNotifier()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        string memoryDir = Path.Combine(this.root, "Agents", "nova", "memory");
        Directory.CreateDirectory(memoryDir);
        foreach (string folder in new[] { Path.Combine(teamsRoot, "memory"), Path.Combine(teamsRoot, "Business", "memory") })
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "a.md"), "# A.\n\nOne.");
            File.WriteAllText(Path.Combine(folder, "b.md"), "# B.\n\nTwo.");
        }

        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);
        IReadOnlyList<TeamSummary> teams = [new TeamSummary("Business", [], ["nova"], true)];
        RecordingFeedPublisher feed = new();
        OverCapNotifier notifier = new(feed);

        _ = SessionPromptInputs.Build(reader, warnings, teams, "nova", memoryDir, 50, ["Business"], teamsRoot, 1, maxHuddleMemoryEntries: 1, maxOutputsEntries: 40, overCap: notifier);

        // Each folder holds 2 files against a cap of 1, so 1 is listed and 1 is not.
        string[] texts = [.. feed.Posts.Select(post => Assert.IsType<SecondBrainOverCapPayload>(post.Payload).Message).Order(StringComparer.Ordinal)];
        Assert.Equal(
            [
                "Teams/Business/memory has more Memory than Agents are shown (1 of 1 listed). Ask the Team's manager to tidy it.",
                "Teams/memory has more Memory than Agents are shown (1 of 1 listed). Ask the Team's manager to tidy it.",
            ],
            texts);
    }
}
