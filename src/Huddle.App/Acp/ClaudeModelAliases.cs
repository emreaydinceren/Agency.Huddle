namespace Agency.Huddle.App.Acp;

/// <summary>
/// Readable names for the Claude Adapter's model alias ids, used only while nothing is cached for that
/// Adapter. A card or profile popover renders before anything has probed the Adapter, and rendering
/// must never spawn an adapter process, so without this a stored id such as <c>sonnet</c> would show
/// as-is. A name from a cached catalog always wins over these; the names carry no version number
/// because a version here would go stale.
/// </summary>
internal static class ClaudeModelAliases
{
    /// <summary>The id of the Claude Adapter profile.</summary>
    private const string ClaudeAdapterId = "claude";

    /// <summary>The readable name of the Claude Adapter's alias id <paramref name="model"/>, or null when it is not one.</summary>
    /// <param name="adapterId">The resolved Adapter profile's id.</param>
    /// <param name="model">The chosen model id.</param>
    internal static string? NameOf(string adapterId, string model)
    {
        if (!string.Equals(adapterId, ClaudeModelAliases.ClaudeAdapterId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return model switch
        {
            "sonnet" => "Sonnet",
            "opus" => "Opus",
            "haiku" => "Haiku",
            _ => null,
        };
    }
}
