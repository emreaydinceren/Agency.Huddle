namespace Agency.Huddle.App.Hooks;

/// <summary>
/// Reads a hook's current text — the catalog default, or a configured override when one exists. This
/// is the only surface later call sites depend on, so a hand-written fake can stand in for it in
/// tests without a mocking framework.
/// </summary>
internal interface IHookSource
{
    /// <summary>The hook's current text with placeholders substituted.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <param name="values">
    /// A value for each placeholder the hook's text may contain. A placeholder in
    /// <see cref="HookDefinition.RequiredPlaceholders"/> missing from this dictionary is a caller
    /// error; a placeholder outside <see cref="HookDefinition.Placeholders"/> that happens to be
    /// present is simply unused.
    /// </param>
    /// <returns>The hook's text, with every recognised <c>{{name}}</c> token replaced by its value.</returns>
    string Render(string key, IReadOnlyDictionary<string, string> values);

    /// <summary>The hook's current text, unsubstituted — placeholders left as literal <c>{{name}}</c> tokens.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <returns>The raw text, for display and editing in the settings UI.</returns>
    string Raw(string key);
}
