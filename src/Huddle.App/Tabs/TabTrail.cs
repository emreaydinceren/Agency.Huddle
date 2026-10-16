using System.Globalization;

namespace Agency.Huddle.App.Tabs;

/// <summary>One crumb of a page's breadcrumb trail.</summary>
/// <param name="Text">The label shown.</param>
/// <param name="Href">The site-relative address the crumb opens, or <see langword="null"/> for a crumb that is plain text (the current page, or an area with no page of its own).</param>
/// <param name="Icon">A symbolic icon name, as <see cref="TabIcons.For(string)"/> maps it.</param>
public sealed record TrailCrumb(string Text, string? Href, string Icon);

/// <summary>
/// Works out the breadcrumb trail of a Tab from its URL: the area the page belongs to, then the page.
/// Pure and synchronous, like <see cref="TabKeys"/>, whose table of URLs it follows. A page that draws its
/// name as its h1 (a Room, a Team, a Project) gets only its ancestors, so the name is not said twice; a page
/// without one (a Task View, a settings tab) ends in the Tab's own title, which is the View's display name and
/// never its slug (<see cref="TabReconciler.ReconcileViews"/> keeps it so).
/// </summary>
internal static class TabTrail
{
    /// <summary>Builds the trail for <paramref name="url"/>.</summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    /// <param name="tabTitle">The title of the Tab showing <paramref name="url"/>, used for the last crumb.</param>
    /// <returns>At least one crumb. Every crumb but the last that has a page of its own carries its address.</returns>
    public static IReadOnlyList<TrailCrumb> Build(string url, string tabTitle)
    {
        string relative = TabKeys.ToRelative(url);
        int query = relative.IndexOf('?', StringComparison.Ordinal);
        string path = query >= 0 ? relative[..query] : relative;

        string[] segments = [.. path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)];

        TabDescriptor descriptor = TabKeys.Describe(relative);
        TrailCrumb last = new(tabTitle, null, descriptor.Icon);

        return segments switch
        {
            // A Room, Team or Project draws its own name as the page's h1, so the trail holds only its ancestors.
            ["rooms", ..] => [new("Chats", "/", "chat")],
            ["teams", var team, "projects", ..] => [new("Teams", null, "team"), new(team, TeamHref(team), "team")],
            ["teams", ..] => [new("Teams", null, "team")],
            ["tasks"] => [last],
            ["tasks", ..] => [new("Tasks", "/tasks", "tasks"), last],
            ["settings", var tab, ..] => [new("Settings", "/settings", "settings"), new(Capitalised(tab), null, "settings")],
            _ => [last],
        };
    }

    private static string TeamHref(string team) => $"/teams/{Uri.EscapeDataString(team)}";

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : string.Concat(char.ToUpper(text[0], CultureInfo.InvariantCulture).ToString(), text.AsSpan(1));
}