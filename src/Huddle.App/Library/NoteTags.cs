namespace Agency.Huddle.App.Library;

/// <summary>
/// A note's tags: the union of its frontmatter <c>tags</c> and its inline <c>#tag</c>s (Spec §6.17), a tag written more than one way
/// counted once (case-insensitively, keeping the first spelling).
/// </summary>
internal static class NoteTags
{
    /// <summary>The note's own tags, frontmatter first and then the body, each in source order and without repeats.</summary>
    /// <param name="text">The note's decoded text, frontmatter included.</param>
    internal static IReadOnlyList<string> Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        NoteFrontmatterBlock located = NoteFrontmatter.Locate(text);
        string? block = located.Text;
        int bodyStart = located.BodyStart;
        List<string> tags = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        if (block is not null)
        {
            NoteTags.AddDistinct(NoteTagReader.Read(block, located.Form), seen, tags);
        }

        NoteTags.AddDistinct(NoteTagParser.Parse(text[bodyStart..]), seen, tags);
        return tags;
    }

    private static void AddDistinct(IReadOnlyList<string> candidates, HashSet<string> seen, List<string> tags)
    {
        foreach (string candidate in candidates)
        {
            if (seen.Add(candidate))
            {
                tags.Add(candidate);
            }
        }
    }
}
