namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Pins the AGENTS.md block of <see cref="SystemPromptComposer"/> (Spec §8.1, §8.2, correction C5): where it sits,
/// the levels sentence (T8), and the heading of each section.
/// </summary>
public sealed class AgentsMdCompositionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-agentsmdcomp-" + Guid.NewGuid().ToString("N"));

    private const string LevelsSentence =
        "Each level below adds to the one before it. If two rules conflict, follow the more specific one: a Project's over its Team's, a Team's over the Huddle-wide rules.";

    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    /// <summary>Composes a prompt for Nova with the given snapshot.</summary>
    /// <param name="agentsMd">The snapshot, or null for none.</param>
    private static string Compose(AgentsMdSnapshot? agentsMd) =>
        SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            agentsMd: agentsMd);

    /// <summary>A snapshot holding Huddle-wide, Team A, Project A/Alpha and Team B sections.</summary>
    private static AgentsMdSnapshot FourSections() =>
        new(
            [
                new AgentsMdSection(AgentsMdLevel.HuddleWide, null, null, "T/AGENTS.md", "Huddle text.", false),
                new AgentsMdSection(AgentsMdLevel.Team, "A", null, "T/A/AGENTS.md", "Team A text.", false),
                new AgentsMdSection(AgentsMdLevel.Project, "A", "Alpha", "T/A/Alpha/AGENTS.md", "Alpha text.", false),
                new AgentsMdSection(AgentsMdLevel.Team, "B", null, "T/B/AGENTS.md", "Team B text.", false),
            ],
            []);

    /// <summary>The levels sentence appears exactly once, after the Persona's text and before the identity line.</summary>
    [Fact]
    public void Compose_WithSections_PutsLevelsSentenceOnceBetweenPersonaAndIdentity()
    {
        string actual = Compose(FourSections());

        int first = actual.IndexOf(LevelsSentence, StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.Equal(-1, actual.IndexOf(LevelsSentence, first + 1, StringComparison.Ordinal));
        string identity = new FakePromptSource().Render(
            "systemPrompt.identity",
            new Dictionary<string, string> { ["{{personaName}}"] = "Nova" });
        Assert.True(actual.IndexOf("You are Nova.", StringComparison.Ordinal) < first);
        Assert.True(first < actual.IndexOf(identity, StringComparison.Ordinal));
    }

    /// <summary>Each section has the heading <c>## level (path)</c> followed by its text, in snapshot order.</summary>
    [Fact]
    public void Compose_WithSections_HeadsEachSectionWithLevelAndPathInOrder()
    {
        string actual = Compose(FourSections());

        string[] expected =
        [
            "## Huddle-wide (T/AGENTS.md)\nHuddle text.",
            "## Team A (T/A/AGENTS.md)\nTeam A text.",
            "## Project A/Alpha (T/A/Alpha/AGENTS.md)\nAlpha text.",
            "## Team B (T/B/AGENTS.md)\nTeam B text.",
        ];
        string normalised = actual.Replace("\r\n", "\n", StringComparison.Ordinal);
        int previous = -1;
        foreach (string section in expected)
        {
            int at = normalised.IndexOf(section, StringComparison.Ordinal);
            Assert.True(at > previous, section);
            previous = at;
        }
    }

    /// <summary>With an empty snapshot neither the levels sentence nor any heading appears, and the prompt equals one composed with none.</summary>
    [Fact]
    public void Compose_EmptySnapshot_AddsNeitherSentenceNorHeading()
    {
        string actual = Compose(AgentsMdSnapshot.Empty);

        Assert.DoesNotContain(LevelsSentence, actual, StringComparison.Ordinal);
        Assert.DoesNotContain("## Huddle-wide", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("## Team ", actual, StringComparison.Ordinal);
        Assert.Equal(Compose(null), actual);
    }

    /// <summary>
    /// SB-R10 (Spec §6.2, D-9): an Agent's <c>work/Instructions.md</c> is no level any more, so its text never reaches the composed prompt,
    /// while the Human's AGENTS.md beside it still does. The session-level twin is <c>SessionStart_ReadsTheThreeAgentsMdFilesInOrder</c>.
    /// </summary>
    [Fact]
    public void Compose_AgentWorkInstructionsFile_NeverReachesThePrompt()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        string workDir = Path.Combine(this.root, "Agents", "Nova", "work");
        Directory.CreateDirectory(Path.Combine(teamsRoot, "A"));
        Directory.CreateDirectory(workDir);
        File.WriteAllText(Path.Combine(workDir, "Instructions.md"), "ZEBRA");
        File.WriteAllText(Path.Combine(teamsRoot, "AGENTS.md"), "Huddle text.");
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);

        string actual = Compose(reader.Read(teamsRoot, ["A"]));

        Assert.DoesNotContain("ZEBRA", actual, StringComparison.Ordinal);
        Assert.Contains("Huddle text.", actual, StringComparison.Ordinal);
    }

    /// <summary>Removes the temporary data folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>The levels sentence (T8) is the default of <c>systemPrompt.agentsMdLevels</c>.</summary>
    [Fact]
    public void AgentsMdLevels_DefaultIsTheLevelsSentence()
    {
        Assert.Equal(LevelsSentence, PromptCatalog.Get("systemPrompt.agentsMdLevels").Default);
    }
}
