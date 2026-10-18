using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.SkillPacks.Prompts;

/// <summary>An <see cref="IPromptSource"/> over the catalog defaults that records the values it was given.</summary>
internal sealed class RecordingPromptSource : IPromptSource
{
    /// <summary>The values of the last render.</summary>
    public IReadOnlyDictionary<string, string> LastValues { get; private set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string Render(string key, IReadOnlyDictionary<string, string> values)
    {
        this.LastValues = values;
        return PromptRenderer.Render(this.Raw(key), values);
    }

    /// <inheritdoc/>
    public string Raw(string key)
    {
        return PromptCatalog.All.Single(definition => definition.Key == key).Default;
    }
}
