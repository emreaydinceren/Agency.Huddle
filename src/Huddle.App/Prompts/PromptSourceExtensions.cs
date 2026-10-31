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

    /// <summary>Renders a prompt, naming each placeholder by its bare name, as a tool's refusals do.</summary>
    /// <param name="source">The prompt source.</param>
    /// <param name="key">The prompt's <see cref="PromptDefinition.Key"/>.</param>
    /// <param name="values">The placeholder names, without braces, and their values.</param>
    /// <returns>The prompt's current text with the values in place.</returns>
    public static string Say(this IPromptSource source, string key, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string name, string value) in values)
        {
            map["{{" + name + "}}"] = value;
        }

        return source.Render(key, map);
    }
}
