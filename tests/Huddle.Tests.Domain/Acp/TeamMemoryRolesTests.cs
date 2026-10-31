namespace Agency.Huddle.Tests.Acp;

using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins the role paragraphs of the Team Memory block (Instructions spec §6.2): one frame with one heading, a paragraph for the
/// Teams a Persona manages first and one for the Teams it only belongs to, and the old frame still rendered for an override that
/// predates the roles.
/// </summary>
public sealed class TeamMemoryRolesTests
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help", "mcp__team__memory"];

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-roles-root");

    private static readonly string Sep = Path.DirectorySeparatorChar.ToString();

    /// <summary>A group with an empty Team-wide Memory.</summary>
    /// <param name="team">The Team's folder name.</param>
    /// <param name="isManager">Whether the Persona manages it.</param>
    private static TeamMemoryGroup Group(string team, bool isManager)
    {
        string memory = Path.Combine(Root, team, "memory");
        return new TeamMemoryGroup(team, memory, new MemorySnapshot(memory, [], 0), [], isManager);
    }

    /// <summary>The two folder lines the paths Prompt renders for a Team.</summary>
    /// <param name="team">The Team's folder name.</param>
    private static string Paths(string team) =>
        $"- {team}, whole Team: {Path.Combine(Root, team, "memory")}{Sep}\n"
        + $"- {team}, one Project: {Path.Combine(Root, team, "<Project>", "memory")}{Sep}";

    /// <summary>Composes Nova's whole prompt with the groups.</summary>
    /// <param name="prompts">The Prompt source.</param>
    /// <param name="logger">Receives the composer's log entries, or null.</param>
    /// <param name="toolNames">The offered tools.</param>
    /// <param name="groups">The Persona's Team Memory groups.</param>
    private static string Compose(FakePromptSource prompts, ILogger? logger, IReadOnlyList<string> toolNames, params TeamMemoryGroup[] groups) =>
        SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            prompts,
            toolNames[0],
            toolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            teamMemory: new TeamMemorySnapshot(groups, 0),
            logger: logger);

    /// <summary>Composes Nova's prompt with the groups and returns the Team Memory block alone.</summary>
    /// <param name="prompts">The Prompt source.</param>
    /// <param name="logger">Receives the composer's log entries, or null.</param>
    /// <param name="toolNames">The offered tools.</param>
    /// <param name="groups">The Persona's Team Memory groups.</param>
    private static string TeamBlock(FakePromptSource prompts, ILogger? logger, IReadOnlyList<string> toolNames, params TeamMemoryGroup[] groups)
    {
        string prompt = Compose(prompts, logger, toolNames, groups);
        string closing = "\n\n" + prompts.Render("systemPrompt.sharedSession", new Dictionary<string, string>());
        int start = prompt.IndexOf("## Team Memory", StringComparison.Ordinal);
        int end = prompt.LastIndexOf(closing, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The Team Memory block is missing.");
        return prompt[start..end];
    }

    /// <summary>A manager reads the manager paragraph alone: it owns the Memory, names the tool, and has no member paragraph.</summary>
    [Fact]
    public void Manager_OfEveryTeam_GetsOnlyTheManagerParagraph()
    {
        string actual = TeamBlock(new FakePromptSource(), null, ToolNames, Group("Dev", isManager: true));

        string expected = $"""
            ## Team Memory
            You own the shared Memory of Dev: other members read it, and only you write it,
            with the mcp__team__memory tool.
            {Paths("Dev")}
            You see the Team's chat, Tasks and decisions as they happen, so decide for yourself what is worth
            filing: goals, decisions and the reasons behind them, agreed ways of working, who owns what, and
            lessons learned. Do it as you go, at natural points such as the end of a Task or after a decision.
            Only what the whole Team benefits from belongs here. When a decision is reversed, replace it and
            note briefly that it changed. Descriptions follow the same rules as your own Memory.

            Dev:
            Nothing yet.
            """.ReplaceLineEndings("\n");
        Assert.Equal(expected, actual);
        Assert.DoesNotContain("written by each Team's manager", actual, StringComparison.Ordinal);
    }

    /// <summary>A Persona that manages one Team and belongs to another gets one heading, the manager paragraph first, then the member paragraph, each with only its own Teams.</summary>
    [Fact]
    public void MixedRoles_GetOneHeading_ManagerParagraphFirst()
    {
        string actual = TeamBlock(new FakePromptSource(), null, ToolNames, Group("Dev", isManager: false), Group("Ops", isManager: true));

        Assert.Single(actual.Split("## Team Memory"), static part => part.Length > 0);
        int managed = actual.IndexOf("You own the shared Memory of Ops:", StringComparison.Ordinal);
        int member = actual.IndexOf("Dev keep shared Memory, written by each Team's manager:", StringComparison.Ordinal);
        Assert.True(managed > 0 && member > managed, "The manager paragraph comes first, then the member paragraph.");
        Assert.Contains(Paths("Ops"), actual[managed..member], StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain(Paths("Dev"), actual[managed..member], StringComparison.Ordinal);
        Assert.Contains(Paths("Dev"), actual[member..], StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>Several managed Teams are named as a sentence would name them.</summary>
    [Fact]
    public void Manager_OfThreeTeams_NamesThemInASentence()
    {
        string actual = TeamBlock(new FakePromptSource(), null, ToolNames, Group("A", true), Group("B", true), Group("C", true));

        Assert.Contains("You own the shared Memory of A, B and C:", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>An Adapter without the tool prefix is told the bare tool name.</summary>
    [Fact]
    public void ToolName_FollowsTheAdaptersPrefix()
    {
        string actual = TeamBlock(new FakePromptSource(), null, ["get_help", "memory"], Group("Dev", isManager: true));

        Assert.Contains("with the memory tool.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("mcp__", actual, StringComparison.Ordinal);
    }

    /// <summary>An override of the old frame, which names the paths and not the roles, is rendered the old way with every Team's paths, and a Warning names the new placeholder.</summary>
    [Fact]
    public void OldFrameOverride_IsRenderedTheOldWay_WithAWarning()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.teamMemory", "OLD FRAME\n{{teamMemoryPaths}}\n{{teamMemoryIndex}}");
        RecordingLogger logger = new();

        string actual = Compose(prompts, logger, ToolNames, Group("Dev", isManager: true), Group("Ops", isManager: false));

        Assert.Contains($"OLD FRAME\n{Paths("Dev")}\n{Paths("Ops")}\nDev:\nNothing yet.\nOps:\nNothing yet.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("You own the shared Memory", actual, StringComparison.Ordinal);
        LogEntry warning = Assert.Single(logger.AtExactly(LogLevel.Warning));
        Assert.Equal("The 'systemPrompt.teamMemory' override still uses {{teamMemoryPaths}}; it is rendered the old way. Use {{teamMemoryRoles}} to say who writes Team Memory.", warning.Message);
    }
}
