namespace Agency.Huddle.App.Acp;

using System.Globalization;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;

/// <summary>
/// Builds the full system prompt for an Agent's session: a short canned orientation naming
/// <c>mcp__team__get_help</c>, then the Persona's own text, then a fixed block that explains the
/// chat rules and names the application's own tools.
/// </summary>
/// <remarks>
/// <para>
/// Every part of the prompt except the Persona's own <see cref="Persona.Text"/> is a prompt: its text
/// comes from <see cref="IPromptSource"/>, which resolves a configured override or falls back to
/// <see cref="PromptCatalog"/>'s default. The Persona's text has no prompt key — it is structural, not
/// model-facing configuration, so it is spliced in as-is between the orientation and the identity
/// prompt. The composition order itself, and the blank line joining the five parts, are fixed here in
/// code and are not configurable.
/// </para>
/// <para>
/// acp-agent-guide.md §3.6 is binding here: the tools must be named with their full <c>mcp__team__</c>
/// prefix, or a deferred-tool-mode model reports that no such tool exists rather than finding it by a
/// looser name. Getting this wrong cost a previous author four rounds of debugging. That prefix is
/// applied by the caller, in code, from the same tool-server name it hands to <c>AppToolServer</c> —
/// never typed into a prompt's template — so <c>Compose</c> receives both <c>toolNames</c> and
/// <c>helpToolName</c> already prefixed, and only has to join and wrap them. This type holds no
/// <c>"mcp__team__"</c> literal of its own, for either one.
/// </para>
/// <para>
/// The leading block is the progressive-discovery entry point. It says what kind of application this
/// is and points at <see cref="Tools.GetHelpTool"/>, which carries the full detail and is paid for
/// only when a model actually asks. The fixed block below the Persona is deliberately kept as well:
/// it is the same guidance inline, so an Agent that never calls a tool still behaves correctly, and
/// <c>get_help</c> is an amplification rather than a precondition.
/// </para>
/// </remarks>
internal static class SystemPromptComposer
{
    /// <summary>
    /// The column at which <see cref="WrapToolNames"/> wraps the joined tool list. Chosen to reproduce
    /// the hand-wrapped literal this composer replaced; see the type-level remarks.
    /// </summary>
    private const int ToolNameWrapWidth = 80;

    /// <summary>
    /// A Team with at most this many Projects lists its empty Projects with <c>systemPrompt.memoryEmpty</c>;
    /// a Team with more leaves them out to save prompt space (Spec §6.4).
    /// </summary>
    private const int MaxProjectsListedWhenEmpty = 5;

    /// <summary>Composes a Persona's full system prompt from its prompts and its own text.</summary>
    /// <param name="persona">The Persona whose <see cref="Persona.Text"/> and <see cref="Persona.Name"/> are spliced in.</param>
    /// <param name="prompts">Resolves each prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <param name="helpToolName">
    /// <see cref="Tools.GetHelpTool"/>'s own name, already carrying its full <c>mcp__team__</c> prefix
    /// (e.g. <c>"mcp__team__get_help"</c>). Named by the caller from the same tool instance it built,
    /// so this composer never retypes <c>"get_help"</c> or the prefix.
    /// </param>
    /// <param name="toolNames">
    /// Every tool name this session exposes, already carrying its full <c>mcp__team__</c> prefix, in
    /// the order they should be listed.
    /// </param>
    /// <returns>The five parts — orientation, Persona text, identity, chat rules, tools — joined with a blank line.</returns>
    internal static string Compose(Persona persona, IPromptSource prompts, string helpToolName, IReadOnlyList<string> toolNames)
    {
        return Compose(persona, prompts, helpToolName, toolNames, [], string.Empty);
    }

    /// <summary>
    /// Composes a Persona's full system prompt, with a sixth part naming its assigned Skills appended
    /// when it holds any (Spec §6.4). A Persona with no Skills gets output byte-identical to the
    /// four-argument overload, which is exactly this overload called with an empty Skill list.
    /// </summary>
    /// <param name="persona">The Persona whose <see cref="Persona.Text"/> and <see cref="Persona.Name"/> are spliced in.</param>
    /// <param name="prompts">Resolves each prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <param name="helpToolName">
    /// <see cref="Tools.GetHelpTool"/>'s own name, already carrying its full <c>mcp__team__</c> prefix
    /// (e.g. <c>"mcp__team__get_help"</c>). Named by the caller from the same tool instance it built,
    /// so this composer never retypes <c>"get_help"</c> or the prefix.
    /// </param>
    /// <param name="toolNames">
    /// Every tool name this session exposes, already carrying its full <c>mcp__team__</c> prefix, in
    /// the order they should be listed.
    /// </param>
    /// <param name="skills">The Persona's resolved Skills for this session; an empty list omits the block entirely.</param>
    /// <param name="readSkillToolName">
    /// <c>read_skill</c>'s own name, already carrying its full <c>mcp__team__</c> prefix. Only read when
    /// <paramref name="skills"/> is non-empty, so a caller with no Skills may pass an empty string.
    /// </param>
    /// <returns>
    /// The five parts joined with a blank line, plus a sixth Skills block when <paramref name="skills"/> is
    /// non-empty.
    /// </returns>
    internal static string Compose(
        Persona persona,
        IPromptSource prompts,
        string helpToolName,
        IReadOnlyList<string> toolNames,
        IReadOnlyList<Skill> skills,
        string readSkillToolName)
    {
        return Compose(persona, prompts, helpToolName, toolNames, skills, readSkillToolName, memory: null);
    }

    /// <summary>
    /// Composes a Persona's full system prompt, with a seventh part carrying the Memory index
    /// appended after the Skills block (FC §6.15), when <paramref name="memory"/> is non-null. A
    /// <see langword="null"/> <paramref name="memory"/> gets output byte-identical to the six-argument
    /// overload, which is exactly this overload called with <paramref name="memory"/> null.
    /// </summary>
    /// <param name="persona">The Persona whose <see cref="Persona.Text"/> and <see cref="Persona.Name"/> are spliced in.</param>
    /// <param name="prompts">Resolves each prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <param name="helpToolName">
    /// <see cref="Tools.GetHelpTool"/>'s own name, already carrying its full <c>mcp__team__</c> prefix
    /// (e.g. <c>"mcp__team__get_help"</c>). Named by the caller from the same tool instance it built,
    /// so this composer never retypes <c>"get_help"</c> or the prefix.
    /// </param>
    /// <param name="toolNames">
    /// Every tool name this session exposes, already carrying its full <c>mcp__team__</c> prefix, in
    /// the order they should be listed.
    /// </param>
    /// <param name="skills">The Persona's resolved Skills for this session; an empty list omits the block entirely.</param>
    /// <param name="readSkillToolName">
    /// <c>read_skill</c>'s own name, already carrying its full <c>mcp__team__</c> prefix. Only read when
    /// <paramref name="skills"/> is non-empty, so a caller with no Skills may pass an empty string.
    /// </param>
    /// <param name="memory">
    /// The Agent's Memory index, built at session start (FC §6.15), or <see langword="null"/> when the
    /// resolved Adapter cannot read files, or File Changes is disabled for this installation.
    /// </param>
    /// <returns>
    /// The five parts joined with a blank line, plus a sixth Skills block when <paramref name="skills"/>
    /// is non-empty, plus a seventh memory block when <paramref name="memory"/> is non-null.
    /// </returns>
    internal static string Compose(
        Persona persona,
        IPromptSource prompts,
        string helpToolName,
        IReadOnlyList<string> toolNames,
        IReadOnlyList<Skill> skills,
        string readSkillToolName,
        MemorySnapshot? memory)
    {
        return Compose(persona, prompts, helpToolName, toolNames, skills, readSkillToolName, memory, SessionScope.Shared);
    }

    /// <summary>
    /// Composes a Persona's full system prompt, choosing D28's per-mode truthful text (RS §6.9) from
    /// <paramref name="scope"/> instead of always appending <c>systemPrompt.sharedSession</c>.
    /// <see cref="SessionScope.Shared"/> reproduces the seven-argument overload's output
    /// byte-for-byte, which is exactly this overload called with <see cref="SessionScope.Shared"/>.
    /// </summary>
    /// <param name="persona">The Persona whose <see cref="Persona.Text"/> and <see cref="Persona.Name"/> are spliced in.</param>
    /// <param name="prompts">Resolves each prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <param name="helpToolName">
    /// <see cref="Tools.GetHelpTool"/>'s own name, already carrying its full <c>mcp__team__</c> prefix
    /// (e.g. <c>"mcp__team__get_help"</c>). Named by the caller from the same tool instance it built,
    /// so this composer never retypes <c>"get_help"</c> or the prefix.
    /// </param>
    /// <param name="toolNames">
    /// Every tool name this session exposes, already carrying its full <c>mcp__team__</c> prefix, in
    /// the order they should be listed.
    /// </param>
    /// <param name="skills">The Persona's resolved Skills for this session; an empty list omits the block entirely.</param>
    /// <param name="readSkillToolName">
    /// <c>read_skill</c>'s own name, already carrying its full <c>mcp__team__</c> prefix. Only read when
    /// <paramref name="skills"/> is non-empty, so a caller with no Skills may pass an empty string.
    /// </param>
    /// <param name="memory">
    /// The Agent's Memory index, built at session start (FC §6.15), or <see langword="null"/> when the
    /// resolved Adapter cannot read files, or File Changes is disabled for this installation. Also
    /// gates <c>systemPrompt.roomSessionsCarry</c> in <see cref="SessionScope.PerRoom"/> (RS §6.9: the
    /// carry line only applies when the two routes it names - memory, File Changes - exist).
    /// </param>
    /// <param name="scope">
    /// Which of RS §6.9's two truthful texts this session gets, resolved by the caller from the
    /// Adapter Profile's <see cref="AdapterProfile.SessionPerRoom"/>.
    /// </param>
    /// <param name="teamMemory">
    /// The Persona's Team Memory snapshot, or <see langword="null"/> for none. A snapshot with no Groups
    /// adds nothing, so <see langword="null"/> and an empty snapshot give byte-identical output.
    /// </param>
    /// <param name="askHumanToolName">
    /// <c>ask_human</c>'s own name, already carrying its full prefix, or <see langword="null"/> when the
    /// session does not offer it. When set, <c>systemPrompt.askHuman</c> follows the tools paragraph.
    /// </param>
    /// <param name="roleBlock">
    /// The hired member's role block (Task 29, MS-7), or <see langword="null"/> for a Persona with no
    /// <c>_role</c>; <see langword="null"/> leaves the output byte-identical to a call without it. When
    /// set, <c>systemPrompt.roleBlock</c> follows the tools paragraph and any asking-the-Human paragraph.
    /// </param>
    /// <param name="instructions">
    /// The Human's standing Instructions (Instructions spec §3.2), or <see langword="null"/> or an empty
    /// snapshot for none; either leaves the output byte-identical to a call without it. When it holds
    /// a section, <c>systemPrompt.instructions</c> follows the Persona's text and precedes the identity part.
    /// </param>
    /// <param name="memoryChangesListed">
    /// Whether a memory file's change reaches the Agent as a File Change at the start of its next Turn, which
    /// needs File Changes on and an Adapter that reads files. When <see langword="false"/> the Memory block leaves
    /// out <c>systemPrompt.memoryChanges</c> and the per-Room closing leaves out <c>systemPrompt.roomSessionsCarry</c>,
    /// since both name File Changes (Instructions and Memory §6.1). Defaults to <see langword="true"/>.
    /// </param>
    /// <param name="warnings">
    /// Warns, once per override text, about a <c>systemPrompt.teamMemory</c> override that still uses the old frame; <see langword="null"/> warns of nothing.
    /// </param>
    /// <returns>
    /// The five parts joined with a blank line, plus a sixth Skills block when <paramref name="skills"/>
    /// is non-empty, plus a seventh memory block when <paramref name="memory"/> is non-null, plus a
    /// Team Memory block right after it when <paramref name="teamMemory"/> holds a Group, plus a
    /// closing part chosen by <paramref name="scope"/>: <c>systemPrompt.sharedSession</c> for
    /// <see cref="SessionScope.Shared"/>, or <c>systemPrompt.roomSessions</c> — with
    /// <c>systemPrompt.roomSessionsCarry</c> as its own trailing part when <paramref name="memory"/>
    /// is non-null — for <see cref="SessionScope.PerRoom"/>.
    /// </returns>
    internal static string Compose(
        Persona persona,
        IPromptSource prompts,
        string helpToolName,
        IReadOnlyList<string> toolNames,
        IReadOnlyList<Skill> skills,
        string readSkillToolName,
        MemorySnapshot? memory,
        SessionScope scope,
        TeamMemorySnapshot? teamMemory = null,
        string? askHumanToolName = null,
        RoleBlockInput? roleBlock = null,
        InstructionsSnapshot? instructions = null,
        bool memoryChangesListed = true,
        MemoryWarnings? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentException.ThrowIfNullOrWhiteSpace(helpToolName);
        ArgumentNullException.ThrowIfNull(toolNames);
        ArgumentNullException.ThrowIfNull(skills);

        var orientation = prompts.Render(
            "systemPrompt.orientation",
            new Dictionary<string, string> { ["{{helpTool}}"] = helpToolName });

        var identity = prompts.Render(
            "systemPrompt.identity",
            new Dictionary<string, string> { ["{{personaName}}"] = persona.Name });

        var chatRules = prompts.Render("systemPrompt.chatRules", new Dictionary<string, string>());

        var tools = prompts.Render(
            "systemPrompt.tools",
            new Dictionary<string, string> { ["{{toolNames}}"] = WrapToolNames(toolNames) });

        List<string> parts = [orientation, persona.Text];
        AddInstructionsPart(parts, prompts, instructions);
        parts.AddRange([identity, chatRules, tools]);

        AddToolParagraphs(parts, prompts, toolNames, askHumanToolName);

        if (roleBlock is not null)
        {
            AddRoleParts(parts, prompts, roleBlock);
        }

        AddSkillsPart(parts, prompts, skills, readSkillToolName);

        AddMemoryParts(parts, prompts, memory, teamMemory, toolNames, memoryChangesListed, warnings);

        AddClosingParts(parts, prompts, scope, memory is not null && memoryChangesListed);

        return string.Join("\n\n", parts);
    }

    /// <summary>The offered <c>memory</c> tool's prefixed name, or the bare name when the session does not list it.</summary>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <returns>The name as offered; <c>memory</c> when it is not in the list.</returns>
    internal static string FindMemoryTool(IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        return toolNames.FirstOrDefault(MemoryTool.IsToolName) ?? MemoryTool.ToolName;
    }

    /// <summary>The offered <c>propose_team_memory</c> tool's prefixed name, or the bare name when the session does not list it.</summary>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <returns>The name as offered; <c>propose_team_memory</c> when it is not in the list.</returns>
    internal static string FindProposeTool(IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        return toolNames.FirstOrDefault(ProposeTeamMemoryTool.IsToolName) ?? ProposeTeamMemoryTool.ToolName;
    }

    /// <summary>Adds the Memory block, then the Team Memory block, when their snapshots exist.</summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="memory">The Agent's Memory index, or <see langword="null"/> for none.</param>
    /// <param name="teamMemory">The Persona's Team Memory snapshot, or <see langword="null"/> for none.</param>
    /// <param name="toolNames">The offered tools' names, already prefixed: the memory and proposal tools are named from them.</param>
    /// <param name="memoryChangesListed">Whether a memory file's change reaches the Agent as a File Change at its next Turn.</param>
    /// <param name="warnings">Warns once about an old-style Team Memory override; <see langword="null"/> warns of nothing.</param>
    private static void AddMemoryParts(
        List<string> parts,
        IPromptSource prompts,
        MemorySnapshot? memory,
        TeamMemorySnapshot? teamMemory,
        IReadOnlyList<string> toolNames,
        bool memoryChangesListed,
        MemoryWarnings? warnings)
    {
        string memoryToolName = FindMemoryTool(toolNames);
        if (memory is not null)
        {
            parts.Add(BuildMemoryBlock(prompts, memory, memoryToolName, memoryChangesListed));
        }

        if (teamMemory is not null && teamMemory.Groups.Count > 0)
        {
            parts.Add(BuildTeamMemoryBlock(prompts, teamMemory, new TeamMemoryTools(memoryToolName, FindProposeTool(toolNames)), warnings));
        }
    }

    /// <summary>
    /// Adds the Instructions part, one section per level from general to specific, when the snapshot holds any.
    /// Each section is rendered first and handed to the part as a value, so text containing <c>{{x}}</c> stays literal.
    /// </summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="instructions">The Instructions snapshot, or <see langword="null"/>.</param>
    private static void AddInstructionsPart(List<string> parts, IPromptSource prompts, InstructionsSnapshot? instructions)
    {
        if (instructions is null || instructions.IsEmpty)
        {
            return;
        }

        string sections = string.Join(
            "\n\n",
            instructions.Sections.Select(section => prompts.Render(
                "systemPrompt.instructionsSection",
                new Dictionary<string, string> { ["{{source}}"] = section.Source, ["{{text}}"] = section.Text })));

        parts.Add(prompts.Render("systemPrompt.instructions", new Dictionary<string, string> { ["{{instructions}}"] = sections }));
    }

    /// <summary>The offered <c>log_change</c> tool's prefixed name, or <see langword="null"/> when the session does not offer it.</summary>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <returns>The name as offered.</returns>
    internal static string? FindLogChangeTool(IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        return toolNames.FirstOrDefault(static name => name.EndsWith("log_change", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adds the paragraphs for the tools whose own description an Adapter may never show the model. Each is sent only
    /// when its tool is offered, so a session without the tool reads what it read before.
    /// </summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    /// <param name="askHumanToolName"><c>ask_human</c>'s own prefixed name, or <see langword="null"/> when it is not offered.</param>
    private static void AddToolParagraphs(List<string> parts, IPromptSource prompts, IReadOnlyList<string> toolNames, string? askHumanToolName)
    {
        // An Adapter can defer a tool's own description, so the model may see only the name of
        // ask_human; this paragraph is the one place its "when to ask" is always read. The name
        // arrives prefixed and is substituted, never typed (rules.md: App Tool names). Omitted when
        // the session offers no such tool, so a caller that does not pass it gets the old output.
        if (!string.IsNullOrWhiteSpace(askHumanToolName))
        {
            parts.Add(prompts.Render(
                "systemPrompt.askHuman",
                new Dictionary<string, string> { ["{{askHumanTool}}"] = askHumanToolName }));
        }

        // Same reason as ask_human above: the tool's description may never reach the model, and without this
        // paragraph real models finished the task without logging it (DocHistory spec section 10). The name
        // comes from the offered list, already prefixed, so the paragraph exists only when the tool does.
        string? logChangeToolName = FindLogChangeTool(toolNames);
        if (logChangeToolName is not null)
        {
            parts.Add(prompts.Render(
                "systemPrompt.docHistory",
                new Dictionary<string, string> { ["{{logChangeTool}}"] = logChangeToolName }));
        }
    }

    /// <summary>Adds the role block, followed by the Process block (manager or member wording) when the role carries one.</summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="roleBlock">The member's role, Team, colleagues and skills.</param>
    private static void AddRoleParts(List<string> parts, IPromptSource prompts, RoleBlockInput roleBlock)
    {
        parts.Add(BuildRoleBlock(prompts, roleBlock));
        if (roleBlock.Process is { } process)
        {
            parts.Add(process.IsManager
                ? prompts.Render("systemPrompt.processBlockManager", new Dictionary<string, string> { ["{{process}}"] = process.Content })
                : prompts.Render("systemPrompt.processBlockMember", new Dictionary<string, string> { ["{{path}}"] = process.Content }));
        }
    }

    /// <summary>Adds the Skills block when the Persona has Skills; an empty list adds nothing.</summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="skills">The Persona's resolved Skills.</param>
    /// <param name="readSkillToolName"><c>read_skill</c>'s own name; only read, and then required, when <paramref name="skills"/> is non-empty.</param>
    private static void AddSkillsPart(List<string> parts, IPromptSource prompts, IReadOnlyList<Skill> skills, string readSkillToolName)
    {
        if (skills.Count == 0)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(readSkillToolName);

        var skillsBlock = prompts.Render(
            "systemPrompt.skills",
            new Dictionary<string, string>
            {
                ["{{skillIndex}}"] = BuildSkillIndex(skills),
                ["{{readSkillTool}}"] = readSkillToolName,
            });

        parts.Add(skillsBlock);
    }

    /// <summary>
    /// Adds the truthful closing part(s). D16 P0-1 / D28 (RS §6.9, §8.1): the closing part is appended in the
    /// last overload of the Compose chain, once, after everything else including a Skills or Memory block when
    /// either is present. Every other overload delegates into that one, so appending it anywhere else would double it.
    /// </summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="scope">Which of RS §6.9's two truthful texts this session gets.</param>
    /// <param name="carryLine">Whether to add the carry line in <see cref="SessionScope.PerRoom"/>: it names memory and File Changes, so it needs both.</param>
    private static void AddClosingParts(List<string> parts, IPromptSource prompts, SessionScope scope, bool carryLine)
    {
        if (scope == SessionScope.PerRoom)
        {
            parts.Add(prompts.Render("systemPrompt.roomSessions", new Dictionary<string, string>()));
            if (carryLine)
            {
                parts.Add(prompts.Render("systemPrompt.roomSessionsCarry", new Dictionary<string, string>()));
            }
        }
        else
        {
            parts.Add(prompts.Render("systemPrompt.sharedSession", new Dictionary<string, string>()));
        }
    }

    /// <summary>
    /// Renders the <c>systemPrompt.roleBlock</c> block. The colleague and skill lists are built first and
    /// handed over as values, so a name containing <c>{{x}}</c> stays literal; an empty list renders
    /// <c>- (none)</c> so the heading above it is never left dangling.
    /// </summary>
    /// <param name="prompts">Resolves the role block prompt's current text.</param>
    /// <param name="roleBlock">The member's role, Team, colleagues and skills.</param>
    private static string BuildRoleBlock(IPromptSource prompts, RoleBlockInput roleBlock)
    {
        string consult = roleBlock.Consult.Count == 0
            ? "- (none)"
            : string.Join(
                '\n',
                roleBlock.Consult.Select(static line => string.IsNullOrWhiteSpace(line.ConsultWhen)
                    ? $"- {line.Name} ({line.Role})"
                    : $"- {line.Name} ({line.Role}): {line.ConsultWhen}"));

        string skillLines = roleBlock.Skills.Count == 0
            ? "- (none)"
            : string.Join('\n', roleBlock.Skills.Select(static skill => $"- {skill.Name}: {skill.Description}"));

        return prompts.Render(
            "systemPrompt.roleBlock",
            new Dictionary<string, string>
            {
                ["{{name}}"] = roleBlock.Name,
                ["{{role}}"] = roleBlock.Role,
                ["{{team}}"] = roleBlock.Team,
                ["{{consult}}"] = consult,
                ["{{skills}}"] = skillLines,
            });
    }

    /// <summary>
    /// Renders the <c>systemPrompt.memory</c> block, with its index built from <paramref name="memory"/>'s entries. The
    /// tool's name and the sentence about changes are handed over as values, so a name or a path containing <c>{{x}}</c> stays literal.
    /// </summary>
    /// <param name="prompts">Resolves each memory prompt's current text.</param>
    /// <param name="memory">The Agent's Memory index for this session.</param>
    /// <param name="memoryToolName">The memory tool's name, already carrying the Adapter's prefix.</param>
    /// <param name="memoryChangesListed">Whether to add <c>systemPrompt.memoryChanges</c>; it names File Changes, which only some Adapters get.</param>
    private static string BuildMemoryBlock(IPromptSource prompts, MemorySnapshot memory, string memoryToolName, bool memoryChangesListed)
    {
        var index = memory.Entries.Count == 0
            ? prompts.Render("systemPrompt.memoryEmpty", new Dictionary<string, string>())
            : string.Join('\n', memory.Entries.Select(entry => RenderEntry(prompts, entry)));

        if (memory.NotListed > 0)
        {
            var more = prompts.Render(
                "systemPrompt.memoryMore",
                new Dictionary<string, string>
                {
                    ["{{count}}"] = memory.NotListed.ToString(CultureInfo.InvariantCulture),
                    ["{{memoryPath}}"] = memory.MemoryPath,
                });

            index = memory.Entries.Count == 0 ? more : string.Join('\n', index, more);
        }

        return prompts.Render(
            "systemPrompt.memory",
            new Dictionary<string, string>
            {
                ["{{memoryPath}}"] = memory.MemoryPath,
                ["{{memoryIndex}}"] = index,
                ["{{memoryTool}}"] = memoryToolName,
                ["{{memoryChanges}}"] = memoryChangesListed ? prompts.Render("systemPrompt.memoryChanges", new Dictionary<string, string>()) : string.Empty,
            });
    }

    /// <summary>Renders one index line: the file's name, its description and its full path.</summary>
    /// <param name="prompts">Resolves the entry prompt's current text.</param>
    /// <param name="entry">The memory file's entry.</param>
    private static string RenderEntry(IPromptSource prompts, MemoryEntry entry)
    {
        return prompts.Render(
            "systemPrompt.memoryEntry",
            new Dictionary<string, string>
            {
                ["{{file}}"] = entry.FileName,
                ["{{summary}}"] = entry.Summary,
                ["{{path}}"] = entry.FullPath,
            });
    }

    /// <summary>
    /// Renders the <c>systemPrompt.teamMemory</c> block (Instructions and Memory §6.2): one frame with one heading, holding a
    /// paragraph for the Teams the Persona manages and one for the Teams it only belongs to, then the index. Each part is rendered
    /// first and only then handed to the frame as a value: the renderer is one pass and never re-scans a value, so a Team or
    /// Project name containing <c>{{x}}</c> stays literal.
    /// </summary>
    /// <param name="prompts">Resolves each Team Memory prompt's current text.</param>
    /// <param name="teamMemory">The Persona's Team Memory snapshot; it holds at least one Group.</param>
    /// <param name="tools">The memory and proposal tools' names, already carrying the Adapter's prefix.</param>
    /// <param name="warnings">Warns, once per override text, when a Human's override still uses the old frame; <see langword="null"/> warns of nothing.</param>
    private static string BuildTeamMemoryBlock(IPromptSource prompts, TeamMemorySnapshot teamMemory, TeamMemoryTools tools, MemoryWarnings? warnings)
    {
        List<string> lines = [];
        foreach (var group in teamMemory.Groups)
        {
            AddScope(prompts, lines, group.Team, group.Team, group.TeamWide, isProject: false, listWhenEmpty: true);

            var listEmptyProjects = group.Projects.Count <= MaxProjectsListedWhenEmpty;
            foreach (var (project, memory) in group.Projects)
            {
                AddScope(prompts, lines, $"{group.Team} › {project}", $"{group.Team}/{project}", memory, isProject: true, listWhenEmpty: listEmptyProjects);
            }
        }

        if (teamMemory.NotListed > 0)
        {
            lines.Add(prompts.Render(
                "systemPrompt.teamMemoryMore",
                new Dictionary<string, string> { ["{{count}}"] = teamMemory.NotListed.ToString(CultureInfo.InvariantCulture) }));
        }

        string index = string.Join('\n', lines);

        // A Human's override of the pre-roles frame names {{teamMemoryPaths}} and no {{teamMemoryRoles}}. It keeps
        // working, rendered the old way with every Team's paths, and the Warning says what changed, so the custom text
        // is never dropped silently.
        string frame = prompts.Raw("systemPrompt.teamMemory");
        if (frame.Contains("{{teamMemoryPaths}}", StringComparison.Ordinal) && !frame.Contains("{{teamMemoryRoles}}", StringComparison.Ordinal))
        {
            warnings?.OldTeamMemoryFrame(frame, "{{teamMemoryPaths}}", "{{teamMemoryRoles}}");

            return prompts.Render(
                "systemPrompt.teamMemory",
                new Dictionary<string, string>
                {
                    ["{{teamMemoryPaths}}"] = RenderPaths(prompts, teamMemory.Groups),
                    ["{{teamMemoryIndex}}"] = index,
                });
        }

        return prompts.Render(
            "systemPrompt.teamMemory",
            new Dictionary<string, string>
            {
                ["{{teamMemoryRoles}}"] = RenderRoles(prompts, teamMemory.Groups, tools),
                ["{{teamMemoryIndex}}"] = index,
            });
    }

    /// <summary>
    /// Renders the role paragraphs: the Teams the Persona manages first, then the Teams it belongs to. A paragraph is included
    /// only when the Persona has at least one Team in that role.
    /// </summary>
    /// <param name="prompts">Resolves each paragraph's current text.</param>
    /// <param name="groups">The Persona's Team Memory groups.</param>
    /// <param name="tools">The memory and proposal tools' names, already carrying the Adapter's prefix.</param>
    private static string RenderRoles(IPromptSource prompts, IReadOnlyList<TeamMemoryGroup> groups, TeamMemoryTools tools)
    {
        List<TeamMemoryGroup> managed = [.. groups.Where(static group => group.IsManager)];
        List<TeamMemoryGroup> member = [.. groups.Where(static group => !group.IsManager)];

        List<string> paragraphs = [];
        if (managed.Count > 0)
        {
            paragraphs.Add(prompts.Render(
                "systemPrompt.teamMemoryManager",
                new Dictionary<string, string>
                {
                    ["{{managedTeams}}"] = JoinNames(managed),
                    ["{{managedPaths}}"] = RenderPaths(prompts, managed),
                    ["{{memoryTool}}"] = tools.Memory,
                    ["{{proposeTool}}"] = tools.Propose,
                }));
        }

        if (member.Count > 0)
        {
            paragraphs.Add(prompts.Render(
                "systemPrompt.teamMemoryMember",
                new Dictionary<string, string>
                {
                    ["{{memberTeams}}"] = JoinNames(member),
                    ["{{memberPaths}}"] = RenderPaths(prompts, member),
                    ["{{memoryTool}}"] = tools.Memory,
                    ["{{proposeTool}}"] = tools.Propose,
                }));
        }

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>Renders the two folder lines of each group: one for the whole Team and one pattern for a single Project.</summary>
    /// <param name="prompts">Resolves the paths prompt's current text.</param>
    /// <param name="groups">The groups to render.</param>
    private static string RenderPaths(IPromptSource prompts, IReadOnlyList<TeamMemoryGroup> groups)
    {
        string separator = Path.DirectorySeparatorChar.ToString();

        return string.Join(
            '\n',
            groups.Select(group =>
            {
                string teamMemoryPath = Path.TrimEndingDirectorySeparator(group.TeamMemoryPath);
                string teamFolder = Path.GetDirectoryName(teamMemoryPath) ?? string.Empty;

                return prompts.Render(
                    "systemPrompt.teamMemoryPaths",
                    new Dictionary<string, string>
                    {
                        ["{{team}}"] = group.Team,
                        ["{{teamMemoryPath}}"] = teamMemoryPath + separator,
                        ["{{projectMemoryPattern}}"] = Path.Combine(teamFolder, "<Project>", "memory") + separator,
                    });
            }));
    }

    /// <summary>Joins Team names as a sentence would: <c>A</c>, <c>A and B</c>, <c>A, B and C</c>.</summary>
    /// <param name="groups">The groups whose Teams are named.</param>
    private static string JoinNames(IReadOnlyList<TeamMemoryGroup> groups)
    {
        List<string> names = [.. groups.Select(static group => group.Team)];
        if (names.Count <= 1)
        {
            return string.Concat(names);
        }

        string head = string.Join(", ", names[..^1]);
        return $"{head} and {names[^1]}";
    }

    /// <summary>
    /// Adds one scope's heading and entries to <paramref name="lines"/>. A scope with entries is always
    /// listed. A scope with none reads <c>systemPrompt.memoryEmpty</c> only when it holds no unlisted
    /// files either, and only when <paramref name="listWhenEmpty"/> allows it; a scope that holds only
    /// unlisted files gets its heading and nothing under it if it is the Team-wide scope, and is left out
    /// if it is a Project (its files still count in the closing "more" line).
    /// </summary>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="lines">The index lines built so far.</param>
    /// <param name="scope">The heading's scope: the Team, or <c>Team › Project</c>.</param>
    /// <param name="folder">The <c>folder</c> value that reaches the scope with the memory tool: the Team, or <c>Team/Project</c>.</param>
    /// <param name="memory">The scope's Memory snapshot.</param>
    /// <param name="isProject">Whether the scope is a Project rather than the Team-wide Memory.</param>
    /// <param name="listWhenEmpty">Whether a scope with neither entries nor unlisted files is listed.</param>
    private static void AddScope(
        IPromptSource prompts,
        List<string> lines,
        string scope,
        string folder,
        MemorySnapshot memory,
        bool isProject,
        bool listWhenEmpty)
    {
        var hasEntries = memory.Entries.Count > 0;
        var isEmpty = !hasEntries && memory.NotListed == 0;

        if (!hasEntries && ((isProject && memory.NotListed > 0) || (isEmpty && !listWhenEmpty)))
        {
            return;
        }

        lines.Add(prompts.Render(
            "systemPrompt.teamMemoryHeading",
            new Dictionary<string, string> { ["{{scope}}"] = scope, ["{{folder}}"] = folder }));

        if (isEmpty)
        {
            lines.Add(prompts.Render("systemPrompt.memoryEmpty", new Dictionary<string, string>()));
        }

        foreach (var entry in memory.Entries)
        {
            lines.Add(RenderEntry(prompts, entry));
        }
    }

    /// <summary>Renders the Skill Index: one <c>- {name}: {description}</c> line per Skill, in the given order.</summary>
    /// <param name="skills">The Persona's resolved Skills, in the order they should be listed.</param>
    /// <returns>The Skill Index text, ready to substitute for <c>{{skillIndex}}</c>.</returns>
    private static string BuildSkillIndex(IReadOnlyList<Skill> skills)
    {
        return string.Join('\n', skills.Select(skill => $"- {skill.Name}: {skill.Description}"));
    }

    /// <summary>
    /// Joins <paramref name="toolNames"/> with <c>", "</c> and word-wraps the result at
    /// <see cref="ToolNameWrapWidth"/> columns, breaking only between names, never inside one. The
    /// list is dynamic, so this is a general wrapping rule rather than a hand-copied line break — it
    /// happens to reproduce the original hand-wrapped literal's break today, and keeps working
    /// whichever names, or however many, arrive here in future.
    /// </summary>
    /// <param name="toolNames">Each tool's full, prefixed name, in the order they should be listed.</param>
    /// <returns>
    /// <paramref name="toolNames"/> joined with <c>", "</c> and split across as many lines as needed so
    /// that no line exceeds <see cref="ToolNameWrapWidth"/> columns. Every line but the last carries the
    /// trailing comma from the join; the caller's template supplies the closing period.
    /// </returns>
    private static string WrapToolNames(IReadOnlyList<string> toolNames)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var name in toolNames)
        {
            var candidate = current.Length == 0 ? name : $"{current}, {name}";

            if (candidate.Length > ToolNameWrapWidth && current.Length > 0)
            {
                lines.Add($"{current},");
                current = name;
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);

        return string.Join('\n', lines);
    }
}
