namespace Agency.Huddle.App.Acp;

using Agency.Huddle.App.Prompts;

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
/// never typed into a prompt's template — so <see cref="Compose"/> receives both <c>toolNames</c> and
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
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentException.ThrowIfNullOrWhiteSpace(helpToolName);
        ArgumentNullException.ThrowIfNull(toolNames);

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

        return string.Join("\n\n", orientation, persona.Text, identity, chatRules, tools);
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
