using System.Globalization;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Maps a URL to the <see cref="TabDescriptor"/> it opens as. Pure and synchronous: a title that
/// needs a lookup (a Room's name) is a fallback here and is refreshed later through
/// <see cref="TabService.SetTitle(string, string)"/>.
/// </summary>
internal static class TabKeys
{
    /// <summary>Whether <paramref name="url"/> is a site-relative path (one slash, never <c>//</c> or a backslash) that a Tab may remember.</summary>
    /// <param name="url">The address to check.</param>
    public static bool IsSafeRelative(string? url) =>
        url is { Length: > 0 }
        && url[0] == '/'
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.Contains('\\');

    /// <summary>The site-relative path and query of <paramref name="url"/>, with any fragment and origin removed.</summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    public static string ToRelative(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        string relative = url;
        if (url[0] != '/' && Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
        {
            relative = absolute.PathAndQuery;
        }

        int fragment = relative.IndexOf('#', StringComparison.Ordinal);
        return fragment >= 0 ? relative[..fragment] : relative;
    }

    /// <summary>Works out which Tab <paramref name="url"/> opens as, following the table in section 3 of the specification.</summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    public static TabDescriptor Describe(string url)
    {
        string relative = ToRelative(url);
        int query = relative.IndexOf('?', StringComparison.Ordinal);
        string path = query >= 0 ? relative[..query] : relative;

        string[] segments = [.. path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)];

        return segments switch
        {
            [] => new("chat", "Rooms", "chat"),
            ["rooms", var roomId, ..] => new($"room:{roomId}", "Chat", "chat"),
            ["teams", "build"] => new("team-build", "Build a team", "team"),
            ["teams", var team, "projects", var project, "tasks", var view] => new($"project-tasks:{Lower(team)}/{Lower(project)}/{view}", view, "tasks"),
            ["teams", var team, "projects", var project, ..] => new($"project:{Lower(team)}/{Lower(project)}", project, "project"),
            ["teams", var team, "tasks", var view] => new($"team-tasks:{Lower(team)}/{view}", view, "tasks"),
            ["teams", var team, ..] => new($"team:{Lower(team)}", team, "team"),
            ["tasks"] => new("tasks", "Tasks", "tasks"),
            ["tasks", "new"] => new("task-new", "New task", "tasks"),
            ["tasks", "item", "new", "edit"] => new("task-new-edit", "New task", "tasks"),
            ["tasks", "item", var taskId, "copy"] => new($"task-copy:{taskId}", string.Create(CultureInfo.InvariantCulture, $"Copy of {taskId}"), "tasks"),
            ["tasks", "item", var taskId, "edit"] => new($"task-edit:{taskId}", string.Create(CultureInfo.InvariantCulture, $"Edit {taskId}"), "tasks"),
            ["tasks", "item", var taskId, ..] => new($"task:{taskId}", string.Create(CultureInfo.InvariantCulture, $"Task {taskId}"), "tasks"),
            ["tasks", var view, ..] => new($"tasks:{view}", view, "tasks"),
            ["agents", ..] => new("agents", "Agents", "agents"),
            ["settings", ..] => new("settings", "Settings", "settings"),
            ["library", ..] => new("library", "Library", "library"),
            ["needs-you"] => new("needs-you", "Needs you", "needs-you"),
            ["scheduled"] => new("scheduled", "Scheduled", "scheduled"),
            ["scheduled", var jobId, ..] => new($"job:{jobId}", "Job", "scheduled"),
            ["feed"] => new("feed", "Feed", "feed"),
            _ => new(path, segments[^1], "page"),
        };
    }

    private static string Lower(string name) => name.ToLowerInvariant();
}
