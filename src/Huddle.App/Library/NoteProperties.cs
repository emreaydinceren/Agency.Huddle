namespace Agency.Huddle.App.Library;

/// <summary>
/// Reads a note's front matter into typed properties (front matter properties spec D-1). Pure: no I/O, and it never fails the note
/// (D-10): an entry it cannot read comes back Unsupported with its source, and the entries around it are read normally.
/// The types it gives are D-2 step 3, the inference from the value; <c>PropertyTyping</c> applies the root's assigned types on top.
/// </summary>
internal static class NoteProperties
{
    /// <summary>The block's entries in file order, a repeated key keeping its last entry at the last one's place (D-10).</summary>
    /// <param name="block">The front matter as <see cref="NoteFrontmatter.Locate"/> found it.</param>
    /// <returns>The properties; empty when the note has no front matter.</returns>
    internal static IReadOnlyList<NoteProperty> Read(NoteFrontmatterBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block.Text is null)
        {
            return [];
        }

        List<NoteProperty> read = block.Form == NoteFrontmatterForm.Json
            ? NoteJsonProperties.Read(block.Text)
            : [.. NoteYamlEntries.Segment(block.Text).Select(NoteYamlValue.Parse)];

        List<NoteProperty> properties = [];
        foreach (NoteProperty property in read)
        {
            _ = properties.RemoveAll(existing => string.Equals(existing.Key, property.Key, StringComparison.Ordinal));
            properties.Add(property);
        }

        return properties;
    }
}
