namespace Agency.Huddle.App.Prompts;

/// <summary>
/// Reads a prompt's current text — the catalog default, or a configured override when one exists. This
/// is the only surface later call sites depend on, so a hand-written fake can stand in for it in
/// tests without a mocking framework.
/// </summary>
internal interface IPromptSource
{
    /// <summary>The prompt's current text with placeholders substituted.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="values">
    /// A value for each placeholder the prompt's text may contain. A placeholder in
    /// <see cref="PromptDefinition.RequiredPlaceholders"/> missing from this dictionary is a caller
    /// error; a placeholder outside <see cref="PromptDefinition.Placeholders"/> that happens to be
    /// present is simply unused.
    /// </param>
    /// <returns>The prompt's text, with every recognised <c>{{name}}</c> token replaced by its value.</returns>
    string Render(string key, IReadOnlyDictionary<string, string> values);

    /// <summary>The prompt's current text, unsubstituted — placeholders left as literal <c>{{name}}</c> tokens.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <returns>The raw text, for display and editing in the settings UI.</returns>
    string Raw(string key);
}
