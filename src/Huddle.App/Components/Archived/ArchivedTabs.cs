namespace Agency.Huddle.App.Components.Archived;

/// <summary>
/// The Archived page's addresses: <c>/archived</c> opens the Teams tab, and <c>/archived/teams</c>, <c>/archived/projects</c>
/// and <c>/archived/rooms</c> name a tab, so a Tab that remembers its URL remembers its tab. The old <c>/archived/chats</c>
/// still names the Rooms tab, so a Tab saved before the tab was renamed opens where it did.
/// </summary>
internal static class ArchivedTabs
{
    /// <summary>The page's own address, which opens the Teams tab.</summary>
    public const string Root = "/archived";

    /// <summary>The tab an address names; an address with no tab segment, or one this page does not know, is the Teams tab.</summary>
    /// <param name="url">A site-relative address, or <see langword="null"/>.</param>
    public static ArchivedTab FromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return ArchivedTab.Teams;
        }

        int end = url.IndexOfAny(['?', '#']);
        string[] segments = (end < 0 ? url : url[..end]).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? Parse(Uri.UnescapeDataString(segments[1])) : ArchivedTab.Teams;
    }

    /// <summary>The tab a route segment names, ignoring case; anything else is the Teams tab.</summary>
    /// <param name="segment">The segment after <c>/archived/</c>, or <see langword="null"/>.</param>
    public static ArchivedTab Parse(string? segment) => segment?.ToUpperInvariant() switch
    {
        "PROJECTS" => ArchivedTab.Projects,
        "ROOMS" or "CHATS" => ArchivedTab.Rooms,
        _ => ArchivedTab.Teams,
    };

    /// <summary>The address of <paramref name="tab"/>.</summary>
    /// <param name="tab">The tab.</param>
    public static string Href(ArchivedTab tab) => tab switch
    {
        ArchivedTab.Projects => Root + "/projects",
        ArchivedTab.Rooms => Root + "/rooms",
        _ => Root + "/teams",
    };
}
