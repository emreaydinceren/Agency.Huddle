namespace Agency.Huddle.App.Acp;

using System.Globalization;
using Agency.Huddle.App.FileChanges;
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
/// agent-guide.md §3.6 is binding here: the tools must be named with their full <c>mcp__team__</c>
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
        TeamMemorySnapshot? teamMemory = null)
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

        List<string> parts = [orientation, persona.Text, identity, chatRules, tools];

        if (skills.Count > 0)
        {
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

        if (memory is not null)
        {
            parts.Add(BuildMemoryBlock(prompts, memory));
        }

        if (teamMemory is not null && teamMemory.Groups.Count > 0)
        {
            parts.Add(BuildTeamMemoryBlock(prompts, teamMemory));
        }

        // D16 P0-1 / D28 (RS §6.9, §8.1): the truthful closing part is appended here, in this — the
        // last — overload of the Compose chain, once, after everything else including a Skills or
        // Memory block when either is present. Every other overload delegates into this one, so
        // appending it anywhere else would double it.
        if (scope == SessionScope.PerRoom)
        {
            parts.Add(prompts.Render("systemPrompt.roomSessions", new Dictionary<string, string>()));
            if (memory is not null)
            {
                parts.Add(prompts.Render("systemPrompt.roomSessionsCarry", new Dictionary<string, string>()));
            }
        }
        else
        {
            parts.Add(prompts.Render("systemPrompt.sharedSession", new Dictionary<string, string>()));
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>Renders the <c>systemPrompt.memory</c> block, with its index built from <paramref name="memory"/>'s entries.</summary>
    /// <param name="prompts">Resolves each memory prompt's current text.</param>
    /// <param name="memory">The Agent's Memory index for this session.</param>
    private static string BuildMemoryBlock(IPromptSource prompts, MemorySnapshot memory)
    {
        var index = memory.Entries.Count == 0
            ? prompts.Render("systemPrompt.memoryEmpty", new Dictionary<string, string>())
            : string.Join(
                '\n',
                memory.Entries.Select(entry => prompts.Render(
                    "systemPrompt.memoryEntry",
                    new Dictionary<string, string> { ["{{summary}}"] = entry.Summary, ["{{path}}"] = entry.FullPath })));

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
            new Dictionary<string, string> { ["{{memoryPath}}"] = memory.MemoryPath, ["{{memoryIndex}}"] = index });
    }

    /// <summary>
    /// Renders the <c>systemPrompt.teamMemory</c> block (Spec §6.4). The paths part, each heading and each
    /// entry are rendered first and only then handed to the block prompt as values: the renderer is one
    /// pass and never re-scans a value, so a Team or Project name containing <c>{{x}}</c> stays literal.
    /// </summary>
    /// <param name="prompts">Resolves each Team Memory prompt's current text.</param>
    /// <param name="teamMemory">The Persona's Team Memory snapshot; it holds at least one Group.</param>
    private static string BuildTeamMemoryBlock(IPromptSource prompts, TeamMemorySnapshot teamMemory)
    {
        var separator = Path.DirectorySeparatorChar.ToString();

        var paths = string.Join(
            '\n',
            teamMemory.Groups.Select(group =>
            {
                var teamMemoryPath = Path.TrimEndingDirectorySeparator(group.TeamMemoryPath);
                var teamFolder = Path.GetDirectoryName(teamMemoryPath) ?? string.Empty;

                return prompts.Render(
                    "systemPrompt.teamMemoryPaths",
                    new Dictionary<string, string>
                    {
                        ["{{team}}"] = group.Team,
                        ["{{teamMemoryPath}}"] = teamMemoryPath + separator,
                        ["{{projectMemoryPattern}}"] = Path.Combine(teamFolder, "<Project>", "memory") + separator,
                    });
            }));

        List<string> lines = [];
        foreach (var group in teamMemory.Groups)
        {
            AddScope(prompts, lines, group.Team, group.TeamWide, isProject: false, listWhenEmpty: true);

            var listEmptyProjects = group.Projects.Count <= MaxProjectsListedWhenEmpty;
            foreach (var (project, memory) in group.Projects)
            {
                AddScope(prompts, lines, $"{group.Team} › {project}", memory, isProject: true, listWhenEmpty: listEmptyProjects);
            }
        }

        if (teamMemory.NotListed > 0)
        {
            lines.Add(prompts.Render(
                "systemPrompt.teamMemoryMore",
                new Dictionary<string, string> { ["{{count}}"] = teamMemory.NotListed.ToString(CultureInfo.InvariantCulture) }));
        }

        return prompts.Render(
            "systemPrompt.teamMemory",
            new Dictionary<string, string>
            {
                ["{{teamMemoryPaths}}"] = paths,
                ["{{teamMemoryIndex}}"] = string.Join('\n', lines),
            });
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
    /// <param name="memory">The scope's Memory snapshot.</param>
    /// <param name="isProject">Whether the scope is a Project rather than the Team-wide Memory.</param>
    /// <param name="listWhenEmpty">Whether a scope with neither entries nor unlisted files is listed.</param>
    private static void AddScope(
        IPromptSource prompts,
        List<string> lines,
        string scope,
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

        lines.Add(prompts.Render("systemPrompt.teamMemoryHeading", new Dictionary<string, string> { ["{{scope}}"] = scope }));

        if (isEmpty)
        {
            lines.Add(prompts.Render("systemPrompt.memoryEmpty", new Dictionary<string, string>()));
        }

        foreach (var entry in memory.Entries)
        {
            lines.Add(prompts.Render(
                "systemPrompt.memoryEntry",
                new Dictionary<string, string> { ["{{summary}}"] = entry.Summary, ["{{path}}"] = entry.FullPath }));
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

        return string.Join("\n", lines);
    }
}
