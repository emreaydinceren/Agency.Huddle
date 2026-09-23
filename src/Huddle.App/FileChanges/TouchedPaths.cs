using System.Text.Json;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// Pulls file paths out of an ACP tool call's raw input JSON, per FC §6.8 and FC D-3b. Adapter-
/// agnostic on purpose: it walks every string value in the document rather than looking for a
/// named argument, because <c>claude-agent-acp</c>'s <c>Write</c> and <c>Edit</c> tools use
/// <c>file_path</c> and another Adapter will use something else.
/// </summary>
internal static class TouchedPaths
{
    /// <summary>
    /// Returns every JSON string value in <paramref name="rawInputJson"/>, at any depth, that is a
    /// rooted path, normalised with <see cref="Path.GetFullPath(string)"/> and de-duplicated by
    /// <see cref="FolderSnapshot.PathComparer"/>. Knows no argument name: a relative string, a
    /// non-path string, <see langword="null"/> and malformed JSON all contribute nothing.
    /// </summary>
    /// <param name="rawInputJson">The tool call's raw input, as carried by <c>ToolCallStarted</c> or <c>ToolCallUpdated</c>.</param>
    /// <returns>Every rooted path found, each listed once.</returns>
    internal static IReadOnlyList<string> From(string? rawInputJson)
    {
        if (string.IsNullOrWhiteSpace(rawInputJson))
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawInputJson);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            HashSet<string> found = new(FolderSnapshot.PathComparer);
            CollectPaths(document.RootElement, found);
            return [.. found];
        }
    }

    /// <summary>Recursively walks <paramref name="element"/>, adding every rooted string value to <paramref name="found"/>.</summary>
    /// <param name="element">The JSON element to walk.</param>
    /// <param name="found">Accumulates every rooted path found so far.</param>
    private static void CollectPaths(JsonElement element, HashSet<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectPaths(property.Value, found);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectPaths(item, found);
                }

                break;
            case JsonValueKind.String:
                string? value = element.GetString();
                if (value is { Length: > 0 } && Path.IsPathFullyQualified(value))
                {
                    found.Add(Path.GetFullPath(value));
                }

                break;
        }
    }
}
