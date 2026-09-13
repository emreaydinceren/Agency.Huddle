namespace Agency.Huddle.Tests.Acp.Fakes;

using Agency.Huddle.App.Hooks;

/// <summary>
/// A hand-written <see cref="IHookSource"/> test double: every key resolves to <see cref="HookCatalog"/>'s
/// own default text unless a test has configured an override for it via <see cref="SetOverride"/>. This
/// lets a test prove that a hook override actually reaches a composed prompt, with no file I/O and no
/// mocking framework.
/// </summary>
internal sealed class FakeHookSource : IHookSource
{
    private readonly Dictionary<string, string> overrides = new(StringComparer.Ordinal);

    /// <summary>Configures <paramref name="key"/> to resolve to <paramref name="text"/> instead of its catalog default.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <param name="text">The override text, unsubstituted — placeholders are still expanded by <see cref="Render"/>.</param>
    public void SetOverride(string key, string text)
    {
        this.overrides[key] = text;
    }

    /// <summary>The hook's current text — the configured override, or the catalog default — with placeholders substituted.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    /// <param name="values">A value for each placeholder the hook's text may contain.</param>
    public string Render(string key, IReadOnlyDictionary<string, string> values) =>
        HookRenderer.Render(this.Raw(key), values);

    /// <summary>The hook's current text — the configured override, or the catalog default — unsubstituted.</summary>
    /// <param name="key">The hook's <see cref="HookDefinition.Key"/>.</param>
    public string Raw(string key) =>
        this.overrides.TryGetValue(key, out var text) ? text : HookCatalog.Get(key).Default;
}
