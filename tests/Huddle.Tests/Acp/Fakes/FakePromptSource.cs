namespace Agency.Huddle.Tests.Acp.Fakes;

using Agency.Huddle.App.Prompts;

/// <summary>
/// A hand-written <see cref="IPromptSource"/> test double: every key resolves to <see cref="PromptCatalog"/>'s
/// own default text unless a test has configured an override for it via <see cref="SetOverride"/>. This
/// lets a test prove that a prompt override actually reaches a composed prompt, with no file I/O and no
/// mocking framework.
/// </summary>
internal sealed class FakePromptSource : IPromptSource
{
    private readonly Dictionary<string, string> overrides = new(StringComparer.Ordinal);

    /// <summary>Configures <paramref name="key"/> to resolve to <paramref name="text"/> instead of its catalog default.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="text">The override text, unsubstituted — placeholders are still expanded by <see cref="Render"/>.</param>
    public void SetOverride(string key, string text)
    {
        this.overrides[key] = text;
    }

    /// <summary>The prompt's current text — the configured override, or the catalog default — with placeholders substituted.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="values">A value for each placeholder the prompt's text may contain.</param>
    public string Render(string key, IReadOnlyDictionary<string, string> values) =>
        PromptRenderer.Render(this.Raw(key), values);

    /// <summary>The prompt's current text — the configured override, or the catalog default — unsubstituted.</summary>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    public string Raw(string key) =>
        this.overrides.TryGetValue(key, out var text) ? text : PromptCatalog.Get(key).Default;
}
