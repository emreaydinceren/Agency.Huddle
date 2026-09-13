namespace Agency.Huddle.App.Hooks;

/// <summary>
/// Checks a single hook's text, a full set of configured overrides, or a fully rendered and composed
/// system prompt, for the two silent failure modes <c>docs/agencyteam/rules.md</c> documents as
/// binding — a missing <c>mcp__team__</c> tool name (rule 32) and a Room label that lost its id (rule
/// 33) — plus the more general placeholder mistakes that share the same shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>This validator never throws and never blocks a save.</b> It only ever returns findings; the
/// caller decides what to do with them, and the caller will choose to save anyway. Experimenting with
/// prompt text is the entire point of this feature, and a validator that blocks is one users route
/// around. An <see cref="HookIssueSeverity.Error"/> means "this will probably not work", never "this
/// is refused" — including when the inputs themselves are null or malformed.
/// </para>
/// <para>
/// This type is pure: no I/O, no dependency injection, no mutable static state. It scans for
/// <c>{{...}}</c> tokens using the same tokeniser <see cref="HookRenderer.FindPlaceholders"/> uses, so
/// this validator and the renderer can never disagree about what a "placeholder" is.
/// </para>
/// </remarks>
internal static class HookValidator
{
    /// <summary>
    /// Checks one hook's text against its own <see cref="HookDefinition"/>: that every one of its
    /// <see cref="HookDefinition.RequiredPlaceholders"/> is present, that no unrecognised
    /// <c>{{...}}</c> token appears, and that the text itself is not blank.
    /// </summary>
    /// <param name="definition">The hook definition <paramref name="text"/> is being checked against.</param>
    /// <param name="text">The hook's current text — a catalog default or a configured override.</param>
    /// <returns>
    /// Every issue found, in no particular order; an empty list means the text is clean. Never throws:
    /// a <see langword="null"/> <paramref name="definition"/> or <paramref name="text"/> is reported as
    /// a finding rather than raised as an exception.
    /// </returns>
    internal static IReadOnlyList<HookIssue> Validate(HookDefinition? definition, string? text)
    {
        var key = definition?.Key ?? "(unknown hook)";
        var issues = new List<HookIssue>();

        if (string.IsNullOrWhiteSpace(text))
        {
            issues.Add(new HookIssue(
                key,
                HookIssueSeverity.Error,
                $"The text for '{key}' is empty or whitespace-only; an empty prompt block is rejected downstream."));

            return issues;
        }

        if (definition is null)
        {
            return issues;
        }

        foreach (var required in definition.RequiredPlaceholders)
        {
            if (!text.Contains(required, StringComparison.Ordinal))
            {
                issues.Add(new HookIssue(
                    key,
                    HookIssueSeverity.Error,
                    $"'{key}' is missing the required placeholder {required}. {definition.HelperText}"));
            }
        }

        foreach (var token in FindPlaceholderTokens(text))
        {
            if (!definition.Placeholders.Contains(token, StringComparer.Ordinal))
            {
                issues.Add(new HookIssue(
                    key,
                    HookIssueSeverity.Warning,
                    $"'{key}' contains the token {token}, which is not one of this hook's declared " +
                    $"placeholders ({string.Join(", ", definition.Placeholders)}) — most likely a typo."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Checks every entry of a configured set of hook overrides at once, looking each key up in
    /// <see cref="HookCatalog"/> and running <see cref="Validate"/> against the matching definition.
    /// </summary>
    /// <param name="overrides">
    /// A configured override's text keyed by <see cref="HookDefinition.Key"/>, as it would be read
    /// from the user-editable JSON configuration file.
    /// </param>
    /// <returns>
    /// Every issue found across all overrides, including a <see cref="HookIssueSeverity.Warning"/> for
    /// any key that names no hook in <see cref="HookCatalog"/>. Never throws, even when
    /// <paramref name="overrides"/> is <see langword="null"/> or contains a <see langword="null"/> key
    /// or value.
    /// </returns>
    internal static IReadOnlyList<HookIssue> ValidateAll(IReadOnlyDictionary<string, string?>? overrides)
    {
        var issues = new List<HookIssue>();

        if (overrides is null)
        {
            return issues;
        }

        foreach (var (key, text) in overrides)
        {
            HookDefinition definition;

            try
            {
                definition = HookCatalog.Get(key);
            }
            catch (KeyNotFoundException)
            {
                issues.Add(new HookIssue(
                    key,
                    HookIssueSeverity.Warning,
                    $"'{key}' is not a hook this application knows about; it will be ignored."));

                continue;
            }

            issues.AddRange(Validate(definition, text));
        }

        return issues;
    }

    /// <summary>
    /// Checks a fully rendered and composed system prompt for every tool name a running Agent needs to
    /// find spelled out in full — the failure <c>docs/agencyteam/rules.md:32</c> documents: a tool name
    /// that is missing, or mangled, makes the model report that no such tool exists.
    /// </summary>
    /// <param name="renderedPrompt">
    /// The complete system prompt as it will actually reach the model, after every hook has been
    /// rendered and joined. This runs against the composed whole rather than a single block, because a
    /// tool name may legitimately live in any one of them.
    /// </param>
    /// <param name="toolNames">
    /// Every tool name the running agent roster actually has, already prefixed (e.g.
    /// <c>mcp__team__get_help</c>).
    /// </param>
    /// <returns>
    /// An <see cref="HookIssueSeverity.Error"/> naming each tool from <paramref name="toolNames"/> that
    /// is absent from <paramref name="renderedPrompt"/>. Never throws, even when either argument is
    /// <see langword="null"/> or contains a <see langword="null"/> entry.
    /// </returns>
    internal static IReadOnlyList<HookIssue> ValidateSystemPrompt(string? renderedPrompt, IReadOnlyList<string?>? toolNames)
    {
        var issues = new List<HookIssue>();

        if (toolNames is null)
        {
            return issues;
        }

        foreach (var toolName in toolNames)
        {
            if (string.IsNullOrEmpty(toolName))
            {
                continue;
            }

            if (renderedPrompt is null || !renderedPrompt.Contains(toolName, StringComparison.Ordinal))
            {
                issues.Add(new HookIssue(
                    toolName,
                    HookIssueSeverity.Error,
                    $"The tool '{toolName}' does not appear anywhere in the rendered system prompt; a " +
                    "model that is never told this exact name will report that no such tool exists."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Finds every <c>{{...}}</c> placeholder token in <paramref name="text"/>, deferring to
    /// <see cref="HookRenderer.FindPlaceholders"/> so this validator and the renderer never disagree
    /// about what a placeholder looks like.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>Every placeholder token found, e.g. <c>{{roomId}}</c>.</returns>
    private static IReadOnlyList<string> FindPlaceholderTokens(string text) => HookRenderer.FindPlaceholders(text);
}
