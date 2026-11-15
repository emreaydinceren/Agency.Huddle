namespace Agency.Huddle.Tests.Prompts;

using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Self-consistency checks on <see cref="PromptCatalog"/>: that its 29 entries are well-formed on their
/// own terms, independent of any config file or renderer that will later consume them.
/// </summary>
public sealed partial class PromptCatalogTests
{
    /// <summary>
    /// The catalog carries exactly the 37 prompts the task specifies, no more and no fewer: the original
    /// 24, plus <c>systemPrompt.skills</c> and <c>tool.readSkill.description</c> added for Spec §6.4, plus
    /// <c>tool.validateAgent.description</c> added for Spec §6.8 (Task 9.3), plus
    /// <c>tool.proposeAgents.description</c> added for Spec §6.9 (Task 10.2), plus
    /// <c>turn.greeting</c> added for Spec §6.14 (Task 16.3), plus the six File Changes Turn prompts
    /// (<c>turn.fileChangesHeader</c>, <c>turn.fileAdded</c>, <c>turn.fileChanged</c>,
    /// <c>turn.fileDeleted</c>, <c>turn.fileChangesMore</c>, <c>turn.folderUnchecked</c>) added for
    /// FC §6.13 (Task 7.1), plus <c>tool.watchFolder.description</c> and
    /// <c>tool.unwatchFolder.description</c> added for FC §6.9/§6.13 (Task 10.2), plus
    /// <c>systemPrompt.sharedSession</c> added for RS §6.9/§8.1 (Task 16.2), plus
    /// <c>task.wake.message</c> added for Spec §10.5/§11.9 (Task 9.2), plus the six
    /// <c>tool.*Task.description</c> prompts and <c>getHelp.tasks</c> added for Spec §11.9 (Task 10.7;
    /// corrections-B4 D10 item 7 - method name kept, count updated to 57), plus the four Team Memory
    /// prompts (<c>systemPrompt.teamMemory</c>, <c>systemPrompt.teamMemoryPaths</c>,
    /// <c>systemPrompt.teamMemoryHeading</c>, <c>systemPrompt.teamMemoryMore</c>) added for Team Pages
    /// Spec §7.4 (Task 5.4 - method name kept), plus <c>tool.askHuman.description</c> added for Questions
    /// spec §6.6, plus <c>systemPrompt.askHuman</c> added for the same spec's D-11 amendment (count now 69),
    /// plus <c>turn.libraryDocIncluded</c> and <c>turn.libraryImageUnavailable</c> added for the Prompt
    /// blocks design §6.5 (count now 71), plus <c>tool.fileRequest.description</c>, <c>tool.confirmRequest.description</c> and
    /// <c>tool.triageRequest.description</c> added for the tool descriptions batch 2 (count now 84), plus
    /// <c>tool.proposeSprint.description</c>, <c>tool.markRetroRow.description</c> and <c>tool.proposeRetroChanges.description</c>
    /// added for the tool descriptions batch 3 (count now 87), plus the eight Referenced items prompts (count now 95), plus the seven Skill Packs prompts (count now 102), plus <c>tool.logChange.description</c> added for DocHistory spec §6.3 (count now 103), plus <c>systemPrompt.docHistory</c> added for the same spec section 10 (count now 104), plus <c>helper.needsHuman.system</c> and <c>helper.needsHuman.task</c> added for Helpers spec §6.10 (count now 106), plus <c>tool.search.description</c> added for the Agent search tool (count now 107), plus the twenty-three Instructions and Memory prompts (four for Instructions, <c>systemPrompt.memoryChanges</c>, <c>tool.memory.description</c>, the two Team Memory role paragraphs, fourteen memory refusals and the path-alias refusal; count now 130), plus the nine Team Memory Helper prompts (<c>helper.teamMemory.system</c>, <c>helper.teamMemory.proposal</c>, <c>tool.proposeTeamMemory.description</c> and its six replies; count now 139), plus <c>systemPrompt.memoryReadFailed</c>, the memory tool's own read failure (count now 140), plus <c>systemPrompt.memoryOnlyFrontmatter</c> for a write whose text is only a header (count now 141), plus <c>turn.transcriptOmittedOwn</c> added for Long-Running Rooms LR-R12 (Task 3.3.t; count now 142), plus <c>tool.jobs.description</c> for scheduled Jobs (count now 143), plus <c>systemPrompt.rawRefusal</c> for the Raw guard (Task 3.1.i; count now 144), plus <c>systemPrompt.agentsMdLevels</c> and minus <c>systemPrompt.instructions</c> and <c>systemPrompt.instructionsSection</c> for the AGENTS.md block (Task 5.5.i; <c>instructionsCut</c> is renamed <c>agentsMdCut</c>; count now 143), minus <c>systemPrompt.processBlockManager</c> and <c>systemPrompt.processBlockMember</c> for the retired process blocks (Task 51c, C5; count now 141), plus the five teaching Prompts shipped as overlay files (Task 10.2; count now 148), plus <c>tool.proposeTeamMemory.noTeam</c>, the refusal for a missing Team (count now 150), plus the four Team Rename prompts <c>turn.teamRenamed</c>, <c>turn.projectRenamed</c>, <c>room.teamRenamed</c> and <c>room.projectRenamed</c> (count now 184), plus <c>tool.addComment.description</c> for the Task timeline (#586; count now 186).
    /// </summary>
    [Fact]
    public void All_HasExpectedPromptCount()
    {
        Assert.Equal(186,PromptCatalog.All.Count);
    }

    /// <summary>create_task tells an Agent that a Task whose only step left is the Human's must be moved to Review, because Huddle moves only a Task in Review and never one in Backlog (#393).</summary>
    [Fact]
    public void CreateTaskDescription_TellsAgentToMoveHumanOnlyTaskToReview()
    {
        string create = PromptCatalog.All.Single(p => p.Key == "tool.createTask.description").Default;

        Assert.Contains("only step left is the Human's", create, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("move it to Review", create, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("never from Backlog", create, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
    }

    /// <summary>The Task prompts tell an Agent that Huddle moves a ready gated Task to Needs You, never to use the Action for hand-offs, and not to ask for approval in chat; the status list names Needs You.</summary>
    [Fact]
    public void TaskPrompts_NameNeedsYouAndWhoMovesIt()
    {
        string update = PromptCatalog.All.Single(p => p.Key == "tool.updateTask.description").Default;
        string create = PromptCatalog.All.Single(p => p.Key == "tool.createTask.description").Default;
        string help = PromptCatalog.All.Single(p => p.Key == "getHelp.tasks").Default;

        Assert.Contains("Review to Needs You; Huddle moves it there", update, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("Never use it for hand-offs or status", update, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("don't ask for approval in chat", create, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("Huddle moves a Task in Review to Needs You", help, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("Huddle does not move the Task then, so move it yourself", help.ReplaceLineEndings(" "), StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("only when its status is Needs You", help.ReplaceLineEndings(" "), StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
    }

    /// <summary>3.3.t (LR-R12, correction C10): <c>turn.transcriptOmittedOwn</c> is in the catalog directly after <c>turn.transcriptOmitted</c>.</summary>
    [Fact]
    public void TranscriptOmittedOwn_FollowsTranscriptOmitted()
    {
        List<string> keys = PromptCatalog.All.Select(prompt => prompt.Key).ToList();

        Assert.Contains("turn.transcriptOmittedOwn", keys);
        Assert.Equal(keys.IndexOf("turn.transcriptOmitted") + 1, keys.IndexOf("turn.transcriptOmittedOwn"));
    }

    /// <summary>3.3.t (LR-R12, text T1): <c>turn.transcriptOmittedOwn</c> defaults to T1, takes and requires only <c>{{count}}</c>, and is Live.</summary>
    [Fact]
    public void TranscriptOmittedOwn_CarriesT1AndTakesCount()
    {
        var prompt = PromptCatalog.Get("turn.transcriptOmittedOwn");

        Assert.Equal("…{{count}} of those are your own.", prompt.Default);
        Assert.Equal(["{{count}}"], prompt.Placeholders);
        Assert.Equal(["{{count}}"], prompt.RequiredPlaceholders);
        Assert.Equal(PromptTiming.Live, prompt.Timing);
    }

    /// <summary>Task 58 (PR-4, PR-5): the onboarding turn takes and require their one placeholder and carry no tool prefix.</summary>
    [Theory]
    [InlineData("turn.onboarding", "{{steps}}", PromptTiming.Live)]
    public void ProcessPrompts_TakeTheirPlaceholder(string key, string placeholder, PromptTiming timing)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(timing, prompt.Timing);
        Assert.Equal([placeholder], prompt.Placeholders);
        Assert.Equal([placeholder], prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>Task 64 (OR-3, RT-2): the digest and retro prompts are Live, take and require the team and since placeholders, and carry no tool prefix.</summary>
    [Theory]
    [InlineData("prompt.digest")]
    [InlineData("prompt.retro")]
    public void CadencePrompts_TakeTeamAndSince(string key)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Equal(["{{team}}", "{{since}}"], prompt.Placeholders);
        Assert.Equal(["{{team}}", "{{since}}"], prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>Task 64 (OR-3): the digest asks for links to Tasks and evidence, a decisions and risks list, at most 15 lines, and names the Chief of Staff roll-up.</summary>
    [Fact]
    public void DigestPrompt_LinksEvidenceAndNamesTheChiefOfStaffRollUp()
    {
        string text = PromptCatalog.Get("prompt.digest").Default;

        Assert.True(text.Contains("link", StringComparison.Ordinal));
        Assert.True(text.Contains("decisions", StringComparison.Ordinal));
        Assert.True(text.Contains("risks", StringComparison.Ordinal));
        Assert.True(text.Contains("15 lines", StringComparison.Ordinal));
        Assert.True(text.Contains("Chief of Staff", StringComparison.Ordinal));
    }

    /// <summary>Task 79 (OR-3): the roll-up prompt is Live, takes no placeholders, carries no tool prefix and asks for decisions and risks with links.</summary>
    [Fact]
    public void RollupPrompt_IsLiveWithNoPlaceholders()
    {
        var prompt = PromptCatalog.Get("prompt.rollup");
        string text = prompt.Default;

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
        Assert.False(text.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.True(text.Contains("Decisions:", StringComparison.Ordinal));
        Assert.True(text.Contains("Risks:", StringComparison.Ordinal));
        Assert.True(text.Contains("open Requests per Team", StringComparison.Ordinal));
    }

    /// <summary>Task 64 (RT-3, Spec §Retro): the retro prompt starts with read_records and then follows the gather, ask, report, propose, register order.</summary>
    [Fact]
    public void RetroPrompt_StartsWithReadRecordsThenFollowsTheSpecOrder()
    {
        string text = PromptCatalog.Get("prompt.retro").Default;
        int readRecords = text.IndexOf("read_records", StringComparison.Ordinal);
        int gather = text.IndexOf("1. Gather", StringComparison.Ordinal);
        int ask = text.IndexOf("2. Ask", StringComparison.Ordinal);
        int report = text.IndexOf("3. Report", StringComparison.Ordinal);
        int propose = text.IndexOf("4. Propose", StringComparison.Ordinal);
        int register = text.IndexOf("5. Register", StringComparison.Ordinal);

        Assert.True(gather >= 0 && readRecords > gather);
        Assert.True(gather < ask && ask < report && report < propose && propose < register);
    }

    /// <summary>
    /// Task 29 (MS-7): <c>systemPrompt.roleBlock</c> is <see cref="PromptTiming.NextSession"/>, takes and requires the five
    /// role placeholders, reads in the order identity, colleagues, skills, and never contains the tool prefix.
    /// </summary>
    [Fact]
    public void RoleBlockSystemPrompt_TakesTheFivePlaceholdersInOrder()
    {
        var prompt = PromptCatalog.Get("systemPrompt.roleBlock");
        string[] placeholders = ["{{name}}", "{{role}}", "{{team}}", "{{consult}}", "{{skills}}"];

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Equal(placeholders, prompt.Placeholders);
        Assert.Equal(placeholders, prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.True(prompt.Default.IndexOf("{{name}}", StringComparison.Ordinal) < prompt.Default.IndexOf("Consult these colleagues when", StringComparison.Ordinal));
        Assert.True(prompt.Default.IndexOf("{{consult}}", StringComparison.Ordinal) < prompt.Default.IndexOf("Your skills, read one with read_skill when its trigger applies:", StringComparison.Ordinal));
        Assert.True(prompt.Default.IndexOf("Your skills, read one with read_skill when its trigger applies:", StringComparison.Ordinal) < prompt.Default.IndexOf("{{skills}}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Questions spec §6.6: <c>tool.askHuman.description</c> exists, is <see cref="PromptTiming.NextSession"/>
    /// like every other tool description, takes no placeholders because it names no tool, and so
    /// cannot break rules.md's rule that a Prompt never contains <c>mcp__team__</c>. It carries the
    /// whole of when to ask and when not to (D-11), so those clauses are pinned.
    /// </summary>
    [Fact]
    public void AskHumanDescription_IsNextSessionNamesNoToolAndCarriesTheGuidance()
    {
        var prompt = PromptCatalog.Get("tool.askHuman.description");

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
        Assert.False(string.IsNullOrWhiteSpace(prompt.HelperText));
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.False(prompt.HelperText.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.StartsWith("Asks the Human one to three multiple-choice questions", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("Do not use it when", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("It asks only the Human", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("end your Turn", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("rank_priorities", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>
    /// Questions spec D-11: <c>systemPrompt.askHuman</c> is <see cref="PromptTiming.NextSession"/>, takes and
    /// requires exactly <c>{{askHumanTool}}</c>, and never contains <c>mcp__team__</c> itself: rules.md says
    /// the prefix is built in code and substituted, so a hand-edited Prompt cannot misspell the tool.
    /// </summary>
    [Fact]
    public void AskHumanSystemPrompt_NamesTheToolOnlyThroughItsPlaceholder()
    {
        var prompt = PromptCatalog.Get("systemPrompt.askHuman");

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Equal(["{{askHumanTool}}"], prompt.Placeholders);
        Assert.Equal(["{{askHumanTool}}"], prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.False(prompt.HelperText.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.Contains("{{askHumanTool}}", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("end your Turn", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>
    /// Elicitation bridge E-8: now that Claude's built-in <c>AskUserQuestion</c> is bridged and works, the
    /// paragraph says so but steers a Persona to <c>ask_human</c> for tappable choices, because the built-in
    /// holds the Turn open until the Human answers. The model-facing text names the tool only through its
    /// placeholder, in both places it mentions it, and carries no citation of a spec or a decision.
    /// </summary>
    [Fact]
    public void AskHumanSystemPrompt_PrefersAskHumanOverTheBuiltInQuestionTool()
    {
        var prompt = PromptCatalog.Get("systemPrompt.askHuman");

        const string Passage = "The built-in AskUserQuestion tool also works for tappable choices, but it holds this Turn open until the Human answers, so prefer {{askHumanTool}}: ending your Turn lets the answer arrive as the Human's next Message and keeps the conversation moving.";
        Assert.Equal(1, prompt.Default.Split(Passage).Length - 1);
        Assert.Equal(2, prompt.Default.Split("{{askHumanTool}}").Length - 1);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
        Assert.False(prompt.Default.Contains("D-", StringComparison.Ordinal));
        Assert.False(prompt.Default.Contains("spec", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The four Team Memory prompts Team Pages Spec §7.4 defines all exist, are <see cref="PromptTiming.NextSession"/>, have their exact one-line defaults, the whole placeholder list, the required placeholders, and a helper text free of <c>mcp__team__</c>.</summary>
    [Theory]
    [InlineData(
        "systemPrompt.teamMemory",
        "## Team Memory\n{{teamMemoryRoles}}\n\n{{teamMemoryIndex}}",
        new object[] { "{{teamMemoryRoles}}", "{{teamMemoryIndex}}", "{{teamMemoryPaths}}" },
        new object[] { "{{teamMemoryRoles}}", "{{teamMemoryIndex}}" })]
    [InlineData(
        "systemPrompt.teamMemoryPaths",
        "- {{team}}, whole Team: {{teamMemoryPath}}\n- {{team}}, one Project: {{projectMemoryPattern}}",
        new object[] { "{{team}}", "{{teamMemoryPath}}", "{{projectMemoryPattern}}" },
        new object[] { "{{teamMemoryPath}}", "{{projectMemoryPattern}}" })]
    [InlineData(
        "systemPrompt.teamMemoryHeading",
        "{{scope}} (folder: {{folder}}):",
        new object[] { "{{scope}}", "{{folder}}" },
        new object[] { "{{scope}}" })]
    [InlineData(
        "systemPrompt.teamMemoryMore",
        "…and {{count}} more in the Teams' memory folders.",
        new object[] { "{{count}}" },
        new object[] { "{{count}}" })]
    public void TeamMemoryPrompts_HaveSpecDefaultsAndPlaceholders(string key, string expectedDefault, object?[] expectedPlaceholders, object?[] expectedRequired)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Equal(expectedDefault, prompt.Default);
        Assert.Equal(expectedPlaceholders.Cast<string>().ToList(), prompt.Placeholders);
        Assert.Equal(expectedRequired.Cast<string>().ToList(), prompt.RequiredPlaceholders);
        Assert.False(string.IsNullOrWhiteSpace(prompt.HelperText));
        Assert.False(prompt.HelperText.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>
    /// D28, RS §6.9: <c>systemPrompt.roomSessions</c> and <c>systemPrompt.roomSessionsCarry</c> are
    /// both <see cref="PromptTiming.NextSession"/>, take no placeholders, and carry their exact wording.
    /// </summary>
    [Fact]
    public void Catalog_HasRoomSessionsPrompts()
    {
        var roomSessions = PromptCatalog.Get("systemPrompt.roomSessions");
        Assert.Equal(PromptTiming.NextSession, roomSessions.Timing);
        Assert.Empty(roomSessions.Placeholders);
        var expectedRoomSessions =
            """
            Each Room you are in is a separate conversation, and this session holds exactly one of them.
            Every Message you receive here comes from the Room its label names, and you answer into that
            Room. Your other Rooms have sessions of their own, which you cannot see from here. Treat each
            Room as its own audience: do not assume the people here know what was said in another Room, and
            do not bring it up here. If a Message seems to continue something you cannot see, say so and ask
            rather than guess. Describe your own memory truthfully: you remember this Room's conversation,
            and you do not remember your other Rooms' conversations.
            """;
        Assert.Equal(
            expectedRoomSessions.Replace("\r\n", "\n", StringComparison.Ordinal),
            roomSessions.Default.Replace("\r\n", "\n", StringComparison.Ordinal));

        var carry = PromptCatalog.Get("systemPrompt.roomSessionsCarry");
        Assert.Equal(PromptTiming.NextSession, carry.Timing);
        Assert.Empty(carry.Placeholders);
        var expectedCarry =
            """
            Two things do cross between your Rooms: the files in your memory folder, and the file changes
            listed at the start of a Turn. If something should hold in every Room, write it to your memory.
            """;
        Assert.Equal(
            expectedCarry.Replace("\r\n", "\n", StringComparison.Ordinal),
            carry.Default.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>turn.ownPostLine</c> (RS §6.7, finding P-7) is <see cref="PromptTiming.Live"/>, requires
    /// <c>{{text}}</c>, and carries the exact wording <c>"You, from another Room: {{text}}"</c>.
    /// </summary>
    [Fact]
    public void Catalog_HasOwnPostLine()
    {
        var prompt = PromptCatalog.Get("turn.ownPostLine");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Contains("{{text}}", prompt.RequiredPlaceholders);
        Assert.Equal("You, from another Room: {{text}}", prompt.Default);
    }

    /// <summary>
    /// The three Transcript Catch-up prompts RS §6.9 defines (<c>turn.transcriptHeader</c>,
    /// <c>turn.transcriptResumedHeader</c>, <c>turn.transcriptOmitted</c>) all exist, are
    /// <see cref="PromptTiming.Live"/>, carry their required placeholder, and carry no
    /// <c>mcp__team__</c> literal (D24).
    /// </summary>
    [Fact]
    public void Catalog_HasTranscriptCatchUpPrompts()
    {
        var header = PromptCatalog.Get("turn.transcriptHeader");
        Assert.Equal(PromptTiming.Live, header.Timing);
        Assert.Equal(["{{roomLabel}}"], header.RequiredPlaceholders);
        Assert.False(header.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var resumedHeader = PromptCatalog.Get("turn.transcriptResumedHeader");
        Assert.Equal(PromptTiming.Live, resumedHeader.Timing);
        Assert.Equal(["{{roomLabel}}"], resumedHeader.RequiredPlaceholders);
        Assert.False(resumedHeader.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var omitted = PromptCatalog.Get("turn.transcriptOmitted");
        Assert.Equal(PromptTiming.Live, omitted.Timing);
        Assert.Equal(["{{count}}"], omitted.RequiredPlaceholders);
        Assert.False(omitted.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>systemPrompt.sharedSession</c> (RS §6.9, D16) is <see cref="PromptTiming.NextSession"/>,
    /// takes no placeholders, and its default names no Room: identity stays in the Turn's own label.
    /// </summary>
    [Fact]
    public void Catalog_HasSharedSessionPrompt()
    {
        var prompt = PromptCatalog.Get("systemPrompt.sharedSession");

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>The <c>turn.fileByYouSuffix</c> prompt (FC §6.15, D13) is <see cref="PromptTiming.Live"/>, keeps its leading space, requires <c>{{roomName}}</c>, and carries no <c>mcp__team__</c> literal.</summary>
    [Fact]
    public void Catalog_HasFileByYouSuffixPrompt()
    {
        var prompt = PromptCatalog.Get("turn.fileByYouSuffix");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.StartsWith(" ", prompt.Default, StringComparison.Ordinal);
        Assert.Equal(["{{roomName}}"], prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>The four memory prompts FC §6.13 defines all exist, are <see cref="PromptTiming.NextSession"/>, carry the right required placeholders, and carry no <c>mcp__team__</c> literal (D12).</summary>
    [Fact]
    public void Catalog_HasMemoryPrompts()
    {
        var memory = PromptCatalog.Get("systemPrompt.memory");
        Assert.Equal(PromptTiming.NextSession, memory.Timing);
        Assert.Equal(["{{memoryPath}}", "{{memoryIndex}}"], memory.RequiredPlaceholders);
        Assert.False(memory.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var entry = PromptCatalog.Get("systemPrompt.memoryEntry");
        Assert.Equal(PromptTiming.NextSession, entry.Timing);
        Assert.Contains("{{summary}}", entry.Placeholders);
        Assert.Contains("{{path}}", entry.Placeholders);

        var empty = PromptCatalog.Get("systemPrompt.memoryEmpty");
        Assert.Equal(PromptTiming.NextSession, empty.Timing);
        Assert.Empty(empty.Placeholders);

        var more = PromptCatalog.Get("systemPrompt.memoryMore");
        Assert.Equal(PromptTiming.NextSession, more.Timing);
        Assert.Contains("{{count}}", more.RequiredPlaceholders);
        Assert.False(more.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>The six File Changes Turn prompts FC §6.13 defines all exist, are Live, and carry no <c>mcp__team__</c> literal.</summary>
    [Fact]
    public void Catalog_HasFileChangesTurnPrompts()
    {
        AssertFileChangesPrompt("turn.fileChangesHeader", [], []);
        AssertFileChangesPrompt("turn.fileAdded", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileChanged", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileDeleted", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileChangesMore", ["{{count}}"], ["{{count}}"]);
        AssertFileChangesPrompt("turn.folderUnchecked", ["{{path}}", "{{max}}"], ["{{path}}"]);
    }

    /// <summary>The six Library Turn prompts Spec §6.14 defines all exist, are Live, and have their exact defaults and required placeholders.</summary>
    [Theory]
    [InlineData("turn.libraryDocsHeader", "Library documents mentioned in these messages:", new object[] { }, new object[] { })]
    [InlineData("turn.libraryDoc", "- {{path}} ({{location}}, {{size}})", new object[] { "{{path}}", "{{location}}", "{{size}}" }, new object[] { "{{path}}", "{{location}}", "{{size}}" })]
    [InlineData("turn.libraryDocInline", "{{fence}}\n{{text}}\n{{fence}}", new object[] { "{{fence}}", "{{text}}" }, new object[] { "{{fence}}", "{{text}}" })]
    [InlineData("turn.libraryDocTruncated", "(cut at {{max}} bytes; the file is {{size}}.)", new object[] { "{{max}}", "{{size}}" }, new object[] { "{{max}}", "{{size}}" })]
    [InlineData("turn.libraryDocsMore", "…and {{count}} more documents.", new object[] { "{{count}}" }, new object[] { "{{count}}" })]
    [InlineData("turn.libraryDocTooLarge", "- {{label}}: {{path}} (too large to include: {{sizeKb}} KB; open it with your file tools)", new object[] { "{{label}}", "{{path}}", "{{sizeKb}}" }, new object[] { "{{label}}", "{{path}}", "{{sizeKb}}" })]
    public void LibraryPrompts_HaveSpecDefaultsAndPlaceholders(string key, string expectedDefault, object?[] expectedPlaceholders, object?[] expectedRequired)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Equal(expectedDefault, prompt.Default);
        Assert.Equal(expectedPlaceholders.Cast<string>().ToList(), prompt.Placeholders);
        Assert.Equal(expectedRequired.Cast<string>().ToList(), prompt.RequiredPlaceholders);
    }

    /// <summary>The two <c>watch_folder</c>/<c>unwatch_folder</c> tool descriptions exist, are <see cref="PromptTiming.NextSession"/> like every other <c>tool.*.description</c>, declare no placeholders, and carry no <c>mcp__team__</c> literal — FC §6.9, §6.13.</summary>
    [Fact]
    public void Catalog_HasWatchFolderToolPrompts()
    {
        var watch = PromptCatalog.Get("tool.watchFolder.description");
        Assert.Equal(PromptTiming.NextSession, watch.Timing);
        Assert.Empty(watch.Placeholders);
        Assert.False(watch.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var unwatch = PromptCatalog.Get("tool.unwatchFolder.description");
        Assert.Equal(PromptTiming.NextSession, unwatch.Timing);
        Assert.Empty(unwatch.Placeholders);
        Assert.False(unwatch.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>Asserts one File Changes Turn prompt exists, is <see cref="PromptTiming.Live"/>, declares exactly <paramref name="placeholders"/> and requires exactly <paramref name="requiredPlaceholders"/>, and carries no <c>mcp__team__</c> literal.</summary>
    private static void AssertFileChangesPrompt(string key, IReadOnlyList<string> placeholders, IReadOnlyList<string> requiredPlaceholders)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Equal(placeholders, prompt.Placeholders);
        Assert.Equal(requiredPlaceholders, prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>
    /// D5: each of the nine tool descriptions is a catalog prompt that is <see cref="PromptTiming.NextSession"/>,
    /// takes no placeholders, and whose default is today's hard-coded literal, copied verbatim.
    /// </summary>
    [Theory]
    [InlineData("tool.addEvidence.description", "Adds a line of evidence to a Task: the command you ran with its exit code, or a note when you ran no command. A command needs its exit code, or the line is refused and nothing is added. Evidence is append-only; earlier lines are never changed.")]
    [InlineData("tool.readRecords.description", "Reads a Team's evidence for a retro. With only 'team' (and optionally 'since', a date like 2026-10-01) it returns the compact records bundle. With 'taskId' it returns that Task's Change log, Evidence and comments. With 'room' it returns the last 200 messages of that Room's transcript, each cut to 300 characters. Output is capped at 12000 characters.")]
    [InlineData("tool.listTeams.description", "Lists every Team with its type, manager, what it owns and how many Requests are open in its backlog. Use it to choose the Team to file a Request with.")]
    [InlineData("tool.fileRequest.description", "Files a Request in another Team's backlog. Give the Team, the kind (bug, feature, feedback or question), a title and a body whose '## ' headings cover what the kind needs. Optionally name one of your own Tasks that waits on the answer.")]
    [InlineData("tool.confirmRequest.description", "Confirms that a Done Request you filed was delivered as asked, and closes it. Only your Team's verifier may call it, and only once the Request is in Done. Optionally add a note.")]
    [InlineData("tool.triageRequest.description", "Triages a Request filed with your Team. Decision 'accept' moves it to To Do (optionally name the assignee); 'decline' rejects it and needs a reason; 'needs_info' keeps it in Backlog and needs a reason saying what is missing.")]
    [InlineData("tool.proposeSprint.description", "Proposes a Sprint to the Human: a name and the Tasks to put in it. Nothing is created until the Human approves. Only the manager of the Team can propose, and every Task must belong to that Team and not be Done.")]
    [InlineData("tool.markRetroRow.description", "Marks a Done or Partial row of the Team's Retro register as Verified, Partial or Changed after checking it against later evidence. Verified needs evidence that links a Task id, a logs/... line (logs/...#L12) or a _retros/ file.")]
    [InlineData("tool.proposeRetroChanges.description", "Proposes up to 3 changes to the Human after a retro. Each change names a targetFile (AGENTS.md, _team.md or an Agent's Name.md), the exact text to find (it must occur once in that file) and the text to replace it with. Huddle shows each as a diff; nothing is edited until the Human accepts it, and then Huddle makes the edit, not you.")]
    public void ToolDescriptionPrompts_AreNextSessionWithTodaysDefault(string key, string expectedDefault)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Equal(expectedDefault, prompt.Default);
        Assert.Empty(prompt.Placeholders);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.addEvidence.description</c> is the text the store renders for that key,
    /// which is the text the tool's Description returns once Task 12 reads it from the catalog.
    /// </summary>
    [Fact]
    public async Task AddEvidenceDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Records one line of evidence for a Task.";

        store.Save("tool.addEvidence.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddEvidenceTool tool = new(harness.Service, harness.Store, new ToolCallLogWriter(harness.Options), harness.Directory, harness.Clock, "caller-id", store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.readRecords.description</c> is the text the store renders for that key,
    /// which is the text the tool's Description returns once Task 12 reads it from the catalog.
    /// </summary>
    [Fact]
    public void ReadRecordsDescription_OverriddenValue_IsWhatTheStoreRenders()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Reads a Team's records for a retro.";

        store.Save("tool.readRecords.description", overriddenText);

        Assert.Equal(overriddenText, store.Render("tool.readRecords.description", new Dictionary<string, string>()));
    }

    /// <summary>
    /// D5: an override saved for <c>tool.listTeams.description</c> is the text the store renders for that key,
    /// which is the text the tool's Description returns once Task 12 reads it from the catalog.
    /// </summary>
    [Fact]
    public async Task ListTeamsDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Lists the Teams you can file a Request with.";

        store.Save("tool.listTeams.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ListTeamsTool tool = new(new FakeTeamCatalog(), harness.Store, harness.Personas, harness.Directory, "caller-id", store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.fileRequest.description</c> is the text the tool's Description returns, read from the
    /// catalog through the store.
    /// </summary>
    [Fact]
    public async Task FileRequestDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Files a Request in another Team's backlog.";

        store.Save("tool.fileRequest.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        FileRequestTool tool = new(new FakeTeamCatalog(), harness.Service, harness.Store, new FakeRequestNotifier(), harness.Directory, "caller-id", store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.confirmRequest.description</c> is the text the tool's Description returns, read from the
    /// catalog through the store.
    /// </summary>
    [Fact]
    public async Task ConfirmRequestDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        using var gateDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Confirms a Done Request you filed.";

        store.Save("tool.confirmRequest.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        ConfirmRequestTool tool = new(
            harness.Service,
            harness.Store,
            new RequestConfirmers(new FakeTeamCatalog(), harness.Personas, TaskGateText.BuildGates(gateDir, null)),
            new FakeRequestNotifier(),
            harness.Directory,
            "caller-id",
            store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.triageRequest.description</c> is the text the tool's Description returns, read from the
    /// catalog through the store.
    /// </summary>
    [Fact]
    public async Task TriageRequestDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Triages a Request filed with your Team.";

        store.Save("tool.triageRequest.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TriageRequestTool tool = new(new FakeTeamCatalog(), harness.Service, harness.Store, new FakeRequestNotifier(), harness.Directory, "caller-id", store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.proposeSprint.description</c> is the text the tool's Description returns, read from
    /// the catalog through the store.
    /// </summary>
    [Fact]
    public async Task ProposeSprintDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Proposes a Sprint to the Human.";

        store.Save("tool.proposeSprint.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Agency.Huddle.App.Tasks.SprintProposalStore proposals = new(new DiskTeamFolders(Path.Combine(harness.Options.Value.DataDir, "platform-folder")));
        ProposeSprintTool tool = new(harness.Store, harness.Sprints, proposals, new FakeTeamCatalog(), harness.Directory, "caller-id", store);

        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>
    /// D5: an override saved for <c>tool.proposeRetroChanges.description</c> is the text the tool's Description returns, read from
    /// the catalog through the store.
    /// </summary>
    [Fact]
    public async Task ProposeRetroChangesDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Proposes up to 3 retro changes to the Human.";

        store.Save("tool.proposeRetroChanges.description", overriddenText);
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        Assert.Equal(overriddenText, fixture.Tool("Nova", store).Description);
    }

    /// <summary>No two prompts share a <see cref="PromptDefinition.Key"/>.</summary>
    [Fact]
    public void All_KeysAreUnique()
    {
        var keys = PromptCatalog.All.Select(prompt => prompt.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every required placeholder is also a declared placeholder.</summary>
    [Fact]
    public void All_RequiredPlaceholdersAreDeclaredPlaceholders()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            foreach (var required in prompt.RequiredPlaceholders)
            {
                Assert.Contains(required, prompt.Placeholders);
            }
        }
    }

    /// <summary>No prompt's default text is null, empty, or whitespace-only.</summary>
    [Fact]
    public void All_DefaultsAreNeverBlank()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(prompt.Default), $"Prompt '{prompt.Key}' has a blank Default.");
        }
    }

    /// <summary><see cref="PromptCatalog.Get"/> returns the prompt whose key was asked for.</summary>
    [Fact]
    public void Get_ReturnsTheMatchingPrompt()
    {
        var prompt = PromptCatalog.Get("turn.roomLabel");

        Assert.Equal("turn.roomLabel", prompt.Key);
    }

    /// <summary><see cref="PromptCatalog.Get"/> throws <see cref="KeyNotFoundException"/>, naming the key, when nothing matches.</summary>
    [Fact]
    public void Get_UnknownKey_ThrowsKeyNotFoundExceptionNamingTheKey()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => PromptCatalog.Get("nope"));

        Assert.Contains("nope", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>task.wake.message</c> Prompt (Spec §10.5, §11.9) exists, is <see cref="PromptTiming.Live"/>,
    /// requires all seven placeholders a wake-up Message needs, its Default starts with the Mention
    /// that must stay first, and it carries no <c>mcp__team__</c> literal.
    /// </summary>
    [Fact]
    public void TaskWakeMessage_Exists_Live_RequiresAllSevenPlaceholders()
    {
        var prompt = PromptCatalog.Get("task.wake.message");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Equal(
            ["{{assignee}}", "{{taskId}}", "{{title}}", "{{actor}}", "{{changes}}", "{{status}}", "{{team}}"],
            prompt.RequiredPlaceholders);
        Assert.Equal(
            "@{{assignee}} Task {{taskId}} \"{{title}}\" ({{status}}, {{team}}) was changed by {{actor}}:\n" +
            "{{changes}}\n" +
            "Call get_task with taskId {{taskId}} for the full task.",
            prompt.Default);
    }

    /// <summary>
    /// Every <c>{{...}}</c> token that appears in a prompt's Default is declared in that same prompt's
    /// Placeholders — catching a typo'd placeholder at build/test time rather than at render time.
    /// </summary>
    [Fact]
    public void All_EveryTokenInDefaultIsDeclaredAsAPlaceholder()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            var tokensFound = PlaceholderToken().Matches(prompt.Default).Select(m => m.Value).Distinct();

            foreach (var token in tokensFound)
            {
                Assert.Contains(token, prompt.Placeholders);
            }
        }
    }

    /// <summary>
    /// No prompt's Default still carries the literal <c>mcp__team__</c> tool prefix: every mention of a
    /// tool name — the help tool in the orientation, the five-name list in the tools paragraph, and
    /// the per-tool name in the help catalog entry — was replaced by a placeholder when the text was
    /// lifted out of C#, so the prefix itself now belongs only to whatever substitutes the
    /// placeholder's value, never to the template text.
    /// </summary>
    [Fact]
    public void All_NoDefaultContainsTheLiteralToolPrefix()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            Assert.False(
                prompt.Default.Contains("mcp__team__", StringComparison.Ordinal),
                $"Prompt '{prompt.Key}' still contains the literal 'mcp__team__' prefix in its Default.");
        }
    }

    /// <summary>
    /// The six <c>tool.*Task.description</c> prompts Spec §11.9 defines all exist, are
    /// <see cref="PromptTiming.NextSession"/> like every other <c>tool.*.description</c>, declare no
    /// placeholders, and carry no <c>mcp__team__</c> literal (Task 10.7).
    /// </summary>
    [Fact]
    public void Catalog_HasTaskToolDescriptionPrompts()
    {
        AssertTaskToolDescriptionPrompt("tool.createTask.description");
        AssertTaskToolDescriptionPrompt("tool.getTask.description");
        AssertTaskToolDescriptionPrompt("tool.listTasks.description");
        AssertTaskToolDescriptionPrompt("tool.updateTask.description");
        AssertTaskToolDescriptionPrompt("tool.closeTask.description");
        AssertTaskToolDescriptionPrompt("tool.reopenTask.description");
    }

    /// <summary>Asserts one <c>tool.*Task.description</c> prompt exists, is <see cref="PromptTiming.NextSession"/>, declares no placeholders, and carries no <c>mcp__team__</c> literal.</summary>
    /// <param name="key">The prompt's key.</param>
    private static void AssertTaskToolDescriptionPrompt(string key)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>
    /// Spec §13.13's sentence — Agents already pass a Task id such as <c>PLAT-0042</c> to
    /// <c>get_task</c> — must appear in <c>tool.getTask.description</c>'s Default, so a model reading
    /// only its own tool description still learns what the id it sees in a Message is for.
    /// </summary>
    [Fact]
    public void ToolGetTaskDescription_MentionsTaskIdAndGetTask()
    {
        var prompt = PromptCatalog.Get("tool.getTask.description");

        Assert.Equal(
            "Reads one Task by its id, such as PLAT-0042: its fields, its description, then its timeline's comments and Evidence, with an option to also see its events (the Change log). Task ids such as PLAT-0042 seen in a Message refer to Tasks; call get_task to read one.",
            prompt.Default);
    }

    /// <summary>#586: <c>getHelp.tasks</c> names add_evidence and add_comment, says the timeline is never edited, and sends hand-offs to a comment rather than the description.</summary>
    [Fact]
    public void GetHelpTasks_NamesCommentsAndTheTimeline()
    {
        string help = PromptCatalog.Get("getHelp.tasks").Default.ReplaceLineEndings(" ");

        Assert.Contains("reopen_task, add_evidence and add_comment to work with them", help, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("A Task's description is the only text you edit; its comments and Evidence are a timeline that nobody edits or deletes.", help, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Contains("put those in a Message or a comment on the Task", help, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
    }

    /// <summary>#586: <c>tool.addComment.description</c> sends a check you ran to add_evidence, and an override saved for it is what the tool's Description returns.</summary>
    [Fact]
    public async Task AddCommentDescription_PointsToAddEvidenceAndIsRenderedFromTheCatalog()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Leaves a remark on a Task.";
        store.Save("tool.addComment.description", overriddenText);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        AddCommentTool tool = new(harness.Service, harness.Store, harness.Directory, "caller-id", store);

        Assert.Contains("use add_evidence instead", PromptCatalog.Get("tool.addComment.description").Default, StringComparison.Ordinal); // contains-ok: pins the prompt term an Agent reads
        Assert.Equal(overriddenText, tool.Description);
    }

    /// <summary>Spec §11.9: <c>getHelp.tasks</c> exists and is <see cref="PromptTiming.Live"/> (rendered fresh into <c>BuildHelp</c>'s output, not baked into the system prompt at session start).</summary>
    [Fact]
    public void GetHelpTasks_Exists_Live()
    {
        var prompt = PromptCatalog.Get("getHelp.tasks");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
    }

    /// <summary>
    /// Sprints spec §10.4 (SP-T10): the three Task tool descriptions and <c>getHelp.tasks</c> tell an Agent it can set a
    /// sprint by name, id or <c>current</c>, and (for create, update and help) that the sprint must already exist and
    /// that a sprint groups Tasks in time and spans Teams.
    /// </summary>
    /// <param name="key">The prompt's key.</param>
    /// <param name="mustExist">Whether the prompt also says the sprint must already exist and cannot be created.</param>
    [Theory]
    [InlineData("tool.createTask.description", true)]
    [InlineData("tool.updateTask.description", true)]
    [InlineData("tool.listTasks.description", false)]
    [InlineData("getHelp.tasks", true)]
    public void TaskPrompts_ExplainSprints(string key, bool mustExist)
    {
        string text = PromptCatalog.Get(key).Default.Replace("\n", " ", StringComparison.Ordinal);

        Assert.Contains("sprint", text, StringComparison.Ordinal);
        Assert.Contains("current", text, StringComparison.Ordinal);
        Assert.Contains("name or id", text, StringComparison.Ordinal);
        if (mustExist)
        {
            Assert.Contains("must already exist, because you cannot create one", text, StringComparison.Ordinal);
            Assert.Contains("groups Tasks in time and", text, StringComparison.Ordinal);
            Assert.Contains("spans Teams", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("none", text, StringComparison.Ordinal);
        }
    }

    /// <summary>Spec §11.9: <c>systemPrompt.tools</c> gains a clause mentioning Tasks (Task 10.7).</summary>
    [Fact]
    public void SystemPromptTools_MentionsTasks()
    {
        var prompt = PromptCatalog.Get("systemPrompt.tools");

        // contains-ok: prompt.Default is the whole multi-paragraph tools clause; this test only checks the Task-tracking phrase Task 10.7 added.
        Assert.Contains("to track work as Tasks and", prompt.Default, StringComparison.Ordinal);
    }

    /// <summary>
    /// Corrections-B4 D10 item 8 (DM): the new <c>systemPrompt.tools</c> clause is static and generic
    /// - it names no individual tool - so that <c>Team:Tasks:Enabled=false</c> stays truthful even
    /// though this prompt is baked in at session start regardless of whether the Tasks tools are
    /// actually offered.
    /// </summary>
    [Fact]
    public void SystemPromptTools_TasksClause_NeverNamesATool()
    {
        var prompt = PromptCatalog.Get("systemPrompt.tools");

        Assert.DoesNotContain("create_task", prompt.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("get_task", prompt.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("list_tasks", prompt.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("update_task", prompt.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("close_task", prompt.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("reopen_task", prompt.Default, StringComparison.Ordinal);
    }

    /// <summary>AT-T18: each Referenced items prompt takes and requires the placeholders spec §9.2 gives it, and carries no tool prefix.</summary>
    [Theory]
    [InlineData("turn.referencesHeader", "")]
    [InlineData("turn.referenceTask", "id,title,status,assignee,team")]
    [InlineData("turn.referenceRoom", "name,id,scope,members")]
    [InlineData("turn.referenceTeam", "name,type,members,teamRoom")]
    [InlineData("turn.referenceGone", "kind,key")]
    [InlineData("turn.referencesMore", "count")]
    [InlineData("turn.referencesHint", "listAgents")]
    [InlineData("turn.referencesHintChiefOfStaff", "listAgents,search")]
    [InlineData("turn.referencesTaskHint", "getTask")]
    public void ReferencePrompts_Exist_WithRequiredPlaceholders(string key, string names)
    {
        var prompt = PromptCatalog.Get(key);
        string[] expected = string.IsNullOrEmpty(names) ? [] : names.Split(',').Select(name => string.Concat("{{", name, "}}")).ToArray();

        Assert.Equal(expected, prompt.Placeholders);
        Assert.Equal(expected, prompt.RequiredPlaceholders);
    }

    /// <summary>AT-T18: the hint is the verbatim spec §9.2 text, and it names the unprefixed placeholder, not a tool prefix.</summary>
    [Fact]
    public void ReferencesHint_DefaultIsVerbatim()
    {
        var prompt = PromptCatalog.Get("turn.referencesHint");

        Assert.Equal(
            "To learn more, list Agents and their Teams with {{listAgents}}. You cannot read the Messages of a Room you are not a Member of.",
            prompt.Default.TrimEnd('\r', '\n'));
    }

    /// <summary>
    /// Issue 581 (D-18, amended 2026-11-14): the Chief of Staff's hint is the verbatim spec §9.2 text. It names
    /// <c>search</c>, which reads any Room for the Chief of Staff, and never says a Room is unreadable.
    /// </summary>
    [Fact]
    public void ReferencesHintChiefOfStaff_DefaultIsVerbatim()
    {
        var prompt = PromptCatalog.Get("turn.referencesHintChiefOfStaff");

        Assert.Equal(
            "To learn more, list Agents and their Teams with {{listAgents}}. You can read any Room's Messages with {{search}}: give the Room's id as 'room' and no query for its newest Messages.",
            prompt.Default.TrimEnd('\r', '\n'));
        Assert.DoesNotContain("cannot read", prompt.Default, StringComparison.Ordinal);
    }

    /// <summary>AT-T18: every Referenced items prompt is Live, so an edit takes effect on the next Turn.</summary>
    [Fact]
    public void ReferencePrompts_AreLive()
    {
        string[] keys =
        [
            "turn.referencesHeader",
            "turn.referenceTask",
            "turn.referenceRoom",
            "turn.referenceTeam",
            "turn.referenceGone",
            "turn.referencesMore",
            "turn.referencesHint",
            "turn.referencesHintChiefOfStaff",
            "turn.referencesTaskHint",
        ];

        Assert.All(keys, key => Assert.Equal(PromptTiming.Live, PromptCatalog.Get(key).Timing));
    }

    [GeneratedRegex(@"\{\{[a-zA-Z]+\}\}")]
    private static partial Regex PlaceholderToken();
}
