namespace Agency.Huddle.App.Search;

/// <summary>
/// Reads the open Team from the focused Tab's address (spec §7.4 clause 1). Follows the path split of
/// <c>TabKeys.Describe</c>: the origin, query and fragment are dropped and each segment is unescaped.
/// </summary>
internal static class SearchOpenTeam
{
    /// <summary>The characters that end the path: the query and the fragment.</summary>
    private static readonly char[] PathEnds = ['?', '#'];

    /// <summary>The Team named by <c>/teams/{team}</c> and anything below it, except the New Team page at exactly <c>/teams/new</c>.</summary>
    /// <param name="url">An absolute address or a site-relative one; may be null or empty.</param>
    /// <returns>The Team's name, or null when the address opens no Team.</returns>
    public static string? FromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string path = url;
        int end = path.IndexOfAny(PathEnds);
        if (end >= 0)
        {
            path = path[..end];
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absolute) && absolute.Scheme is "http" or "https")
        {
            path = absolute.AbsolutePath;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !string.Equals(segments[0], "teams", StringComparison.Ordinal))
        {
            return null;
        }

        string team = Uri.UnescapeDataString(segments[1]);
        if (segments.Length == 2 && string.Equals(team, "new", StringComparison.Ordinal))
        {
            return null;
        }

        return team;
    }
}
