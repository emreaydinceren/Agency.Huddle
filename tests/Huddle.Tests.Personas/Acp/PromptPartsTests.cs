using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Task 68 (8.1.t, SB-R16, Spec §6.4): <c>SystemPromptComposer.ComposeParts</c> returns the labelled
/// parts and <c>SystemPromptComposer.Compose</c> is exactly those parts joined with a blank line.
/// The three setups are rebuilt here from the cases in <c>PromptGoldenTests</c> (Tests.Hosted), whose
/// private inputs are not reachable from this project.
/// </summary>
public sealed class PromptPartsTests
{
    /// <summary>The prefixed tool names the golden tests offer, trimmed to what the three setups need.</summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__ask_human",
        "mcp__team__memory",
    ];

    /// <summary>A plain Persona: the joined parts equal <c>Compose</c> and none is empty.</summary>
    [Fact]
    public void ComposeParts_PlainPersona_JoinEqualsCompose()
    {
        Persona persona = new("Nova", "You are Nova.");
        FakePromptSource prompts = new();

        var parts = SystemPromptComposer.ComposeParts(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);
        var composed = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        AssertJoinsAndNonEmpty(parts, composed);
    }

    /// <summary>A Persona with an AGENTS.md snapshot (golden case <c>AgentsMd_TwoTeamsWithProjects</c>): the AGENTS.md block is one of several parts.</summary>
    [Fact]
    public void ComposeParts_WithAgentsMd_JoinEqualsCompose()
    {
        AgentsMdSnapshot agentsMd = new(
            [
                new AgentsMdSection(AgentsMdLevel.HuddleWide, null, null, "Teams/AGENTS.md", "Be brief everywhere.", false),
                new AgentsMdSection(AgentsMdLevel.Team, "A", null, "Teams/A/AGENTS.md", "Team A writes in British English.", false),
                new AgentsMdSection(AgentsMdLevel.Project, "A", "Alpha", "Teams/A/Alpha/AGENTS.md", "Alpha ships on Fridays.", false),
            ],
            []);
        Persona persona = new("Nova", "You are Nova.");
        FakePromptSource prompts = new();

        var parts = SystemPromptComposer.ComposeParts(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, agentsMd: agentsMd);
        var composed = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, agentsMd: agentsMd);

        AssertJoinsAndNonEmpty(parts, composed);
        Assert.True(parts.Count > 5, "The AGENTS.md block adds a part to the five fixed ones.");
    }

    /// <summary>A hired member with a role block, a Memory index and per-Room sessions (golden cases <c>SystemPrompt_HiredMember</c> and <c>PerRoomWithMemory</c>); skills are left out because the shipped Skill catalogue is a Hosted helper.</summary>
    [Fact]
    public void ComposeParts_HiredMemberWithMemory_JoinEqualsCompose()
    {
        Persona persona = new("Ada", "You are Ada.");
        RoleBlockInput roleBlock = new(
            "Ada",
            "Developer",
            "Apollo",
            [new ConsultLine("Grace", "Lead", "Ask about scope and priorities.")],
            [new SkillLine("team-building", "Use when you are asked to propose or change a Team.")]);
        MemorySnapshot memory = new(
            @"E:\Huddle\App_Data\work\Ada\memory",
            [new MemoryEntry("The Human prefers C# for all code.", @"E:\Huddle\App_Data\work\Ada\memory\code-language.md")],
            NotListed: 0);
        FakePromptSource prompts = new();

        var parts = SystemPromptComposer.ComposeParts(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory, SessionScope.PerRoom, askHumanToolName: "mcp__team__ask_human", roleBlock: roleBlock);
        var composed = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory, SessionScope.PerRoom, askHumanToolName: "mcp__team__ask_human", roleBlock: roleBlock);

        AssertJoinsAndNonEmpty(parts, composed);
        Assert.True(parts.Count > 8, "Role, memory and closing parts follow the five fixed ones.");
    }

    /// <summary>Task 70 (8.2.t): the orientation part names the catalogue prompt and reports it as not overridden by default.</summary>
    [Fact]
    public void ComposeParts_Orientation_SourceIsPromptNotOverridden()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.orientation", false), parts.Single(part => part.Label == "Orientation").Source);
    }

    /// <summary>Task 70 (8.2.t): an override of the orientation key flips <c>Overridden</c> to true.</summary>
    [Fact]
    public void ComposeParts_OrientationOverridden_SourceIsOverridden()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.orientation", "Custom orientation, ask {{helpTool}}.");

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.orientation", true), parts.Single(part => part.Label == "Orientation").Source);
    }

    /// <summary>Task 70 (8.2.t): the Persona text part names the Persona; tools part carries the offered count (3 names in <c>ToolNames</c>).</summary>
    [Fact]
    public void ComposeParts_PersonaAndTools_SourcesNamePersonaAndToolCount()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        Assert.Equal(new PromptPartSource.Persona("Nova"), parts.Single(part => part.Label == "Persona text").Source);
        Assert.Equal(new PromptPartSource.Tools(3), parts.Single(part => part.Label == "Tools").Source);
    }

    /// <summary>Task 70 (8.2.t): the AGENTS.md block is one part per section (three sections), each sourced by the section's path.</summary>
    [Fact]
    public void ComposeParts_AgentsMd_OneFilePartPerSection()
    {
        AgentsMdSnapshot agentsMd = new(
            [
                new AgentsMdSection(AgentsMdLevel.HuddleWide, null, null, "Teams/AGENTS.md", "Be brief everywhere.", false),
                new AgentsMdSection(AgentsMdLevel.Team, "A", null, "Teams/A/AGENTS.md", "Team A writes in British English.", false),
                new AgentsMdSection(AgentsMdLevel.Project, "A", "Alpha", "Teams/A/Alpha/AGENTS.md", "Alpha ships on Fridays.", false),
            ],
            []);

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, agentsMd: agentsMd);

        Assert.Equal(
            [
                new PromptPartSource.File("Teams/AGENTS.md"),
                new PromptPartSource.File("Teams/A/AGENTS.md"),
                new PromptPartSource.File("Teams/A/Alpha/AGENTS.md"),
            ],
            parts.Where(part => part.Label == "AGENTS.md block" && part.Source is PromptPartSource.File).Select(part => part.Source).ToList());
        // The block opens with the levels sentence, a catalogue prompt rather than a file.
        Assert.Equal(
            new PromptPartSource.Prompt("systemPrompt.agentsMdLevels", false),
            parts.First(part => part.Label == "AGENTS.md block").Source);
    }

    /// <summary>Task 70 (8.2.t): the Memory part is an index of "memory" with the one entry listed in the snapshot.</summary>
    [Fact]
    public void ComposeParts_Memory_SourceIsIndexWithEntryCount()
    {
        MemorySnapshot memory = new(
            @"E:\Huddle\App_Data\work\Ada\memory",
            [new MemoryEntry("The Human prefers C# for all code.", @"E:\Huddle\App_Data\work\Ada\memory\code-language.md")],
            NotListed: 0);

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Ada", "You are Ada."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory, SessionScope.PerRoom);

        Assert.Equal(new PromptPartSource.Index("memory", 1), parts.Single(part => part.Label == "Memory").Source);
    }

    /// <summary>The prefixed names of every tool the Second Brain summary names, as a full session offers them (<c>JobsTool.Name</c>, <c>CreateTaskTool.Name</c>, <c>LogChangeTool.Name</c>, <c>ProposeTeamMemoryTool.ToolName</c>, <c>MemoryTool.ToolName</c>).</summary>
    private static readonly IReadOnlyList<string> SummaryToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__ask_human",
        "mcp__team__memory",
        "mcp__team__propose_team_memory",
        "mcp__team__create_task",
        "mcp__team__jobs",
        "mcp__team__log_change",
    ];

    /// <summary>Task 87 (SB-R14, Spec 8.1 slot 6): the summary part is the Prompt <c>systemPrompt.secondBrain</c>, not overridden by default, labelled "Second Brain".</summary>
    [Fact]
    public void ComposeParts_SecondBrain_SourceIsPromptNotOverridden()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        PromptPart part = Assert.Single(parts, p => p.Label == "Second Brain");
        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.secondBrain", false), part.Source);
    }

    /// <summary>Task 87: an override of the key flips <c>Overridden</c>, and the override text is what the part holds.</summary>
    [Fact]
    public void ComposeParts_SecondBrainOverridden_SourceIsOverriddenAndTextIsOverride()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.secondBrain", "Custom summary: {{memoryTool}}.");

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        PromptPart part = Assert.Single(parts, p => p.Label == "Second Brain");
        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.secondBrain", true), part.Source);
        Assert.Equal("Custom summary: mcp__team__memory.", part.Text);
    }

    /// <summary>Task 87: every placeholder is filled with the offered prefixed name (one each in the shipped text, so each name appears once) and none is left open.</summary>
    [Fact]
    public void ComposeParts_SecondBrain_FillsPlaceholdersWithOfferedPrefixedNames()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        string text = Assert.Single(parts, p => p.Label == "Second Brain").Text;

        Assert.Contains("with mcp__team__memory;", text, StringComparison.Ordinal);
        Assert.Contains("goes to mcp__team__propose_team_memory;", text, StringComparison.Ordinal);
        Assert.Contains("Due by a date: mcp__team__create_task.", text, StringComparison.Ordinal);
        Assert.Contains("Recurs: mcp__team__jobs.", text, StringComparison.Ordinal);
        Assert.Contains("then mcp__team__log_change.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rule names the tool that proposes it (Spec U4): with <c>propose_retro_changes</c> offered, the summary tells the
    /// Agent to say the rule belongs in <c>AGENTS.md</c> and to propose it with that tool. Without a tool named, a Haiku Agent
    /// in the manual run answered "I'll lead every summary with a number" and proposed nothing.
    /// </summary>
    [Fact]
    public void ComposeParts_SecondBrain_RuleNamesTheRetroTool()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", [.. SummaryToolNames, "mcp__team__propose_retro_changes"], [], string.Empty, memory: null, SessionScope.Shared);

        string text = Assert.Single(parts, p => p.Label == "Second Brain").Text;

        Assert.Contains(
            "Then it is a rule: it belongs in the AGENTS.md of its level. Tell the Human so and propose the line with mcp__team__propose_retro_changes; never edit an AGENTS.md yourself.",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>Whether a part is rendered from the workspace Prompt.</summary>
    /// <param name="part">The part.</param>
    private static bool IsWorkspacePart(PromptPart part) =>
        part.Source is PromptPartSource.Prompt { Key: "systemPrompt.secondBrainWorkspace" };

    /// <summary>Task 130 (SB-R15, Spec 8.1 slot 7): a Team with a readable workspace adds one part from the Prompt, not overridden, with both placeholders filled and none left open.</summary>
    [Fact]
    public void ComposeParts_OneWorkspace_AddsOnePartWithTeamAndPathFilled()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared,
            workspaces: [("Alpha", "/work/alpha")]);

        PromptPart part = Assert.Single(parts, IsWorkspacePart);
        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.secondBrainWorkspace", false), part.Source);
        Assert.StartsWith("Alpha works in /work/alpha.", part.Text, StringComparison.Ordinal); // Default text opens "{{team}} works in {{workspacePath}}."
        Assert.DoesNotContain("{{", part.Text, StringComparison.Ordinal);
    }

    /// <summary>Task 130: the workspace part comes straight after the Second Brain summary (slot 7 follows slot 6).</summary>
    [Fact]
    public void ComposeParts_Workspace_FollowsTheSecondBrainSummary()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared,
            workspaces: [("Alpha", "/work/alpha")]);

        int summary = parts.ToList().FindIndex(p => p.Label == "Second Brain");
        Assert.True(summary >= 0);
        Assert.True(IsWorkspacePart(parts[summary + 1]), "The part after the summary is the workspace part.");
    }

    /// <summary>Task 130: two Teams add two parts, in the order given (the Persona's <c>teams</c> order), each with its own Team and path.</summary>
    [Fact]
    public void ComposeParts_TwoWorkspaces_AddsTwoPartsInGivenOrder()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared,
            workspaces: [("Zed", "/work/zed"), ("Alpha", "/work/alpha")]);

        List<PromptPart> found = [.. parts.Where(IsWorkspacePart)];
        Assert.Equal(2, found.Count);
        Assert.StartsWith("Zed works in /work/zed.", found[0].Text, StringComparison.Ordinal); // Given first, so first, not sorted.
        Assert.StartsWith("Alpha works in /work/alpha.", found[1].Text, StringComparison.Ordinal);
    }

    /// <summary>Task 130: no workspaces, or none given, adds no part, and the prompt equals the one composed without the argument.</summary>
    [Fact]
    public void ComposeParts_NoWorkspaces_AddsNoPartAndLeavesOutputUnchanged()
    {
        Persona persona = new("Nova", "You are Nova.");
        FakePromptSource prompts = new();

        string without = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared);
        string empty = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared, workspaces: []);
        var parts = SystemPromptComposer.ComposeParts(persona, prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared, workspaces: []);

        Assert.Equal(without, empty);
        Assert.DoesNotContain(parts, IsWorkspacePart);
    }

    /// <summary>The Needs You scopes given are listed one line each under the prompt's heading, with the guidance sentences (including that a level may change while the Agent works and the gate checks it at every merge), and no placeholder is left open.</summary>
    [Fact]
    public void ComposeParts_NeedsYouScopes_ListsEachScopeWithItsLevel()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared,
            needsYou: [("Dev Team", "Ask me"), ("Dev Team › Lib", "Run inside the Team")]);

        PromptPart part = Assert.Single(parts, static p => p.Source is PromptPartSource.Prompt { Key: "systemPrompt.needsYou" });
        string expected = string.Join(
            '\n',
            "Needs You level (what waits for the Human):",
            "- Dev Team: Ask me",
            "- Dev Team › Lib: Run inside the Team",
            "Run inside the Team: do not set a merge Action on that scope's Tasks; Huddle releases a Task when its Proof is complete in Review, and its assignee then merges its branch into the local main. Name every branch after its Task id, for example feat/DEV-0012-login: a merge of any other branch still waits for the Human. Every push waits for the Human at every level, so a push, and any step that leaves the Team, goes in the Task's Action. The Human can change a level at any time, so the list above may be out of date; Huddle checks the level again at every merge.");
        Assert.Equal(expected, part.Text);
    }

    /// <summary>No Needs You scopes, or none given, adds no part, and the prompt equals the one composed without the argument.</summary>
    [Fact]
    public void ComposeParts_NoNeedsYouScopes_AddsNoPartAndLeavesOutputUnchanged()
    {
        Persona persona = new("Nova", "You are Nova.");
        FakePromptSource prompts = new();

        string without = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared);
        string empty = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared, needsYou: []);

        Assert.Equal(without, empty);
        Assert.DoesNotContain("Needs You level", without, StringComparison.Ordinal);
    }

    /// <summary>Task 130: an override of the Prompt flips <c>Overridden</c> and the override text is filled in.</summary>
    [Fact]
    public void ComposeParts_WorkspaceOverridden_SourceIsOverriddenAndTextIsOverride()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.secondBrainWorkspace", "{{team}} lives at {{workspacePath}}.");

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), prompts, "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared,
            workspaces: [("Alpha", "/work/alpha")]);

        PromptPart part = Assert.Single(parts, IsWorkspacePart);
        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.secondBrainWorkspace", true), part.Source);
        Assert.Equal("Alpha lives at /work/alpha.", part.Text);
    }

    /// <summary>
    /// Task 87: Spec 8.1 slot 6 is present "Always", so a session that offers none of the five tools still gets the part,
    /// with each placeholder filled by "(not available here; tell the Human)" and no bare tool name the model might try to call.
    /// </summary>
    [Fact]
    public void ComposeParts_SecondBrainToolsNotOffered_RendersUnavailablePhrase()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ["mcp__team__get_help"], [], string.Empty, memory: null, SessionScope.Shared);

        string text = Assert.Single(parts, p => p.Label == "Second Brain").Text;

        const string Unavailable = "(not available here; tell the Human)";
        Assert.Contains($"with {Unavailable};", text, StringComparison.Ordinal);
        Assert.Contains($"goes to {Unavailable};", text, StringComparison.Ordinal);
        Assert.Contains($"Due by a date: {Unavailable}.", text, StringComparison.Ordinal);
        Assert.Contains($"Recurs: {Unavailable}.", text, StringComparison.Ordinal);
        Assert.Contains($"then {Unavailable}.", text, StringComparison.Ordinal);
        Assert.Contains($"propose the line with {Unavailable};", text, StringComparison.Ordinal); // contains-ok: one sentence of a long prompt; the whole text is pinned by systemPrompt.secondBrain.txt.
        Assert.DoesNotContain("create_task", text, StringComparison.Ordinal);
        Assert.DoesNotContain("propose_team_memory", text, StringComparison.Ordinal);
        Assert.DoesNotContain("log_change", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>Task 87: with the tool paragraphs (ask_human and log_change offered, so two) and a role block, the part sits straight after the last paragraph and straight before the role block.</summary>
    [Fact]
    public void ComposeParts_SecondBrain_FollowsToolParagraphsAndPrecedesRoleBlock()
    {
        RoleBlockInput roleBlock = new("Ada", "Developer", "Apollo", [], []);

        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Ada", "You are Ada."), new FakePromptSource(), "mcp__team__get_help", SummaryToolNames, [], string.Empty, memory: null, SessionScope.Shared, askHumanToolName: "mcp__team__ask_human", roleBlock: roleBlock);

        List<string> labels = parts.Select(p => p.Label).ToList();
        int summary = labels.IndexOf("Second Brain");
        Assert.Equal(2, labels.Count(label => label == "Tool paragraphs"));
        Assert.Equal(labels.LastIndexOf("Tool paragraphs") + 1, summary);
        Assert.Equal("Role block", labels[summary + 1]);
    }

    /// <summary>Task 87: with no tool paragraph at all the part follows the Tools part of slot 4, and with no role block the next part is not a role block.</summary>
    [Fact]
    public void ComposeParts_SecondBrainNoParagraphsNoRole_FollowsToolsPart()
    {
        var parts = SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ["mcp__team__get_help"], [], string.Empty, memory: null, SessionScope.Shared);

        List<string> labels = parts.Select(p => p.Label).ToList();
        Assert.Equal(labels.IndexOf("Tools") + 1, labels.IndexOf("Second Brain"));
        Assert.DoesNotContain("Role block", labels);
    }

    /// <summary>Task 95 (11.3.t, Spec §8.1 slot 13): the outputs index follows Team Memory and precedes the closing part, with the source <c>Index("outputs", n)</c> for n entries.</summary>
    [Fact]
    public void ComposeParts_WithOutputs_IndexFollowsTeamMemoryAndPrecedesClosing()
    {
        OutputsSnapshot outputs = new(
            [new OutputsEntry("A/outputs/sub/y.md", null), new OutputsEntry("A/outputs/z.md", "Read when Z")],
            0);

        IReadOnlyList<PromptPart> parts = ComposeWithOutputs(outputs, withMemories: true);

        List<string> labels = [.. parts.Select(static part => part.Label)];
        int at = labels.IndexOf("Outputs index");
        Assert.True(at > 0, "The outputs part is present.");
        Assert.Equal(labels.IndexOf("Team Memory") + 1, at); // Slot 13 directly follows slot 12.
        Assert.Equal("Closing", labels[at + 1]); // Slot 14 follows.
        Assert.Equal(new PromptPartSource.Index("outputs", 2), parts[at].Source); // Two entries in the snapshot.
    }

    /// <summary>The part carries the shipped Prompt's heading and one line per entry: <c>- path - description</c>, or the bare <c>- path</c> without a description.</summary>
    [Fact]
    public void ComposeParts_WithOutputs_RendersOneLinePerEntry()
    {
        OutputsSnapshot outputs = new(
            [new OutputsEntry("A/outputs/sub/y.md", null), new OutputsEntry("A/outputs/z.md", "Read when Z")],
            0);

        PromptPart part = ComposeWithOutputs(outputs, withMemories: false).Single(static part => part.Label == "Outputs index");

        Assert.Contains("## Outputs", part.Text, StringComparison.Ordinal); // The heading of systemPrompt.outputsIndex.
        Assert.EndsWith("- A/outputs/sub/y.md\n- A/outputs/z.md - Read when Z", part.Text.ReplaceLineEndings("\n").TrimEnd(), StringComparison.Ordinal); // systemPrompt.memoryEntry: "- {{file}} - {{summary}}".
    }

    /// <summary>Over the cap each overflow ends the list with the existing "…and N more in {folder}." line, naming the Team's outputs folder.</summary>
    [Fact]
    public void ComposeParts_OutputsOverflow_EndsWithAMoreLinePerFolder()
    {
        OutputsSnapshot outputs = new(
            [new OutputsEntry("A/outputs/z.md", "Read when Z")],
            3,
            [new OutputsOverflow("A/outputs", 1), new OutputsOverflow("B/outputs", 2)]);

        PromptPart part = ComposeWithOutputs(outputs, withMemories: false).Single(static part => part.Label == "Outputs index");

        // systemPrompt.memoryMore: "…and {{count}} more in {{memoryPath}}."
        Assert.EndsWith("- A/outputs/z.md - Read when Z\n…and 1 more in A/outputs.\n…and 2 more in B/outputs.", part.Text.ReplaceLineEndings("\n").TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>No snapshot and an empty one add no outputs part, and the composed prompt is the same as without the argument.</summary>
    [Fact]
    public void ComposeParts_NullOrEmptyOutputs_AddsNoPart()
    {
        IReadOnlyList<PromptPart> withNull = ComposeWithOutputs(null, withMemories: true);
        IReadOnlyList<PromptPart> withEmpty = ComposeWithOutputs(new OutputsSnapshot([], 0), withMemories: true);

        Assert.DoesNotContain(withNull, static part => part.Source is PromptPartSource.Index { Kind: "outputs" });
        Assert.Equal(withNull.Select(static part => part.Text), withEmpty.Select(static part => part.Text));
    }

    /// <summary>The joined parts still equal <c>Compose</c> when an outputs index is present.</summary>
    [Fact]
    public void ComposeParts_WithOutputs_JoinEqualsCompose()
    {
        OutputsSnapshot outputs = new([new OutputsEntry("A/outputs/z.md", "Read when Z")], 0);
        Persona persona = new("Nova", "You are Nova.");
        FakePromptSource prompts = new();

        var parts = SystemPromptComposer.ComposeParts(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, outputs: outputs);
        var composed = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, outputs: outputs);

        AssertJoinsAndNonEmpty(parts, composed);
        Assert.Contains(parts, static part => part.Source is PromptPartSource.Index { Kind: "outputs" });
    }

    /// <summary>Composes Nova's parts with an outputs snapshot, and optionally the three Memory snapshots that precede it.</summary>
    /// <param name="outputs">The outputs index, or null.</param>
    /// <param name="withMemories">Whether Memory, Huddle-wide Memory and Team Memory are present too.</param>
    private static IReadOnlyList<PromptPart> ComposeWithOutputs(OutputsSnapshot? outputs, bool withMemories)
    {
        MemorySnapshot? memory = withMemories ? new("own", [new MemoryEntry("My fact.", "own/mine.md")], 0) : null;
        MemorySnapshot? huddle = withMemories ? new("huddle", [new MemoryEntry("Alpha rule.", "huddle/a.md")], 0) : null;
        TeamMemorySnapshot? team = withMemories
            ? new([new TeamMemoryGroup("A", "A/memory", new MemorySnapshot("A/memory", [new MemoryEntry("Team fact.", "A/memory/t.md")], 0), [])], 0)
            : null;

        return SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory,
            SessionScope.Shared,
            teamMemory: team,
            huddleMemory: huddle,
            outputs: outputs);
    }

    /// <summary>Asserts the invariant of SB-R16: the parts joined with a blank line are the composed prompt, and no part is empty.</summary>
    /// <param name="parts">The parts from <c>ComposeParts</c>.</param>
    /// <param name="composed">The text from <c>Compose</c> with the same arguments.</param>
    private static void AssertJoinsAndNonEmpty(IReadOnlyList<PromptPart> parts, string composed)
    {
        Assert.Equal(composed, string.Join("\n\n", parts.Select(part => part.Text)));
        Assert.All(parts, part => Assert.False(string.IsNullOrEmpty(part.Text), $"Part '{part.Label}' is empty."));
    }
}
