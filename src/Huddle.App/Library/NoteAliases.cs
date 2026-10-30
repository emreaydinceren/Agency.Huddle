namespace Agency.Huddle.App.Library;

/// <summary>A note's aliases (front matter properties spec D-7): the other names it is found by, read from its front matter <c>aliases</c> property.</summary>
internal static class NoteAliases
{
    /// <summary>The note's aliases in source order, trimmed, without empty entries or repeats. A single value is read as a one-item list, as the Aliases type does.</summary>
    /// <param name="text">The note's decoded text, front matter included.</param>
    internal static IReadOnlyList<string> Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        NoteFrontmatterBlock block = NoteFrontmatter.Locate(text);
        if (block.Text is null)
        {
            return [];
        }

        foreach (NoteProperty property in NoteProperties.Read(block))
        {
            if (string.Equals(property.Key, "aliases", StringComparison.Ordinal))
            {
                return Distinct(property);
            }
        }

        return [];
    }

    private static List<string> Distinct(NoteProperty property)
    {
        IEnumerable<string> raw = property.Shape switch
        {
            NotePropertyShape.List => property.Items,
            NotePropertyShape.Scalar => [property.Value],
            _ => [],
        };

        List<string> aliases = [];
        foreach (string alias in raw.Select(static item => item.Trim()))
        {
            if (alias.Length > 0 && !aliases.Contains(alias, StringComparer.Ordinal))
            {
                aliases.Add(alias);
            }
        }

        return aliases;
    }
}
