namespace Agency.Huddle.App.Prompts;

/// <summary>Conveniences over <see cref="IPromptSource"/>.</summary>
internal static class PromptSourceExtensions
{
    /// <summary>The placeholder values of a prompt that takes none.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <summary>Renders a prompt that takes no placeholders, such as every App Tool's description.</summary>
    /// <param name="source">The prompt source.</param>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <returns>The prompt's current text.</returns>
    public static string Render(this IPromptSource source, string key)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Render(key, NoValues);
    }
}
