namespace Agency.Huddle.App.Tabs;

/// <summary>One crumb of a page's breadcrumb trail.</summary>
/// <param name="Text">The label shown.</param>
/// <param name="Href">The site-relative address the crumb opens, or <see langword="null"/> for a crumb that is plain text (the current page, or an area with no page of its own).</param>
/// <param name="Icon">A symbolic icon name, as <see cref="TabIcons.For(string)"/> maps it.</param>
public sealed record TrailCrumb(string Text, string? Href, string Icon);

/// <summary>
/// Works out the breadcrumb trail of a Tab from its URL: the ancestors of the page, never the page itself
/// (design-system "A page"). Every page shows its name as its h1, so the trail never repeats it; a page with
/// no ancestor (Teammates, Settings) has an empty trail and the bar draws none.
/// Pure and synchronous, like <see cref="TabKeys"/>, whose table of URLs it follows.
/// </summary>
internal static class TabTrail
{
    /// <summary>Builds the trail for <paramref name="url"/>.</summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    /// <returns>The ancestors, outermost first, or an empty list when the page has none. Every crumb that has a page of its own carries its address.</returns>
    public static IReadOnlyList<TrailCrumb> Build(string url)
    {
        string relative = TabKeys.ToRelative(url);
        int query = relative.IndexOf('?', StringComparison.Ordinal);
        string path = query >= 0 ? relative[..query] : relative;

        string[] segments = [.. path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)];

        return segments switch
        {
            ["rooms", ..] => [new("Rooms", "/", "chat")],
            ["teams", var team, "projects", ..] => [new("Teams", null, "team"), new(team, TeamHref(team), "team")],
            ["teams", ..] => [new("Teams", null, "team")],
            ["tasks", _, ..] => [new("Tasks", "/tasks", "tasks")],
            _ => [],
        };
    }

    private static string TeamHref(string team) => $"/teams/{Uri.EscapeDataString(team)}";
}