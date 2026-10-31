namespace Agency.Huddle.App.Library;

/// <summary>One top-level YAML entry of a front matter block, cut out but not yet read (D-1, D-10).</summary>
/// <param name="Key">The key, with any quotes removed.</param>
/// <param name="Rest">What follows the colon on the key's own line, trimmed.</param>
/// <param name="Tail">The lines below the key line that belong to the entry, as written.</param>
internal sealed record NoteYamlEntry(string Key, string Rest, IReadOnlyList<string> Tail);
