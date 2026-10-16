using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// The sidebar's density rules in one place: a list longer than <see cref="Limit"/> rows shows the first
/// <see cref="Limit"/> and a "Show all (N)" row, and a group is open by default only when the current page
/// is inside it.
/// </summary>
internal static class SidebarRows
{
    /// <summary>The most rows a sidebar list shows before it offers "Show all (N)".</summary>
    public const int Limit = 8;

    /// <summary>The current page's path relative to the base, without query or fragment, such as <c>rooms/ab12</c>.</summary>
    /// <param name="navigation">The navigation manager to read the current address from.</param>
    public static string CurrentPath(NavigationManager navigation)
    {
        string path = navigation.ToBaseRelativePath(navigation.Uri);
        int end = path.IndexOfAny(['?', '#']);
        return end < 0 ? path : path[..end];
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="section"/> itself or inside it (<c>tasks</c> matches <c>tasks/my-tasks</c>, not <c>tasksx</c>).</summary>
    /// <param name="path">A path from <see cref="CurrentPath"/>.</param>
    /// <param name="section">The first path segment, such as <c>tasks</c>.</param>
    public static bool InSection(string path, string section) =>
        string.Equals(path, section, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(section + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The Room id of a <c>rooms/{id}</c> path, or <see langword="null"/> for any other page.</summary>
    /// <param name="path">A path from <see cref="CurrentPath"/>.</param>
    public static string? RoomId(string path)
    {
        string[] segments = path.Split('/');
        return segments.Length >= 2 && string.Equals(segments[0], "rooms", StringComparison.OrdinalIgnoreCase) && segments[1].Length > 0
            ? Uri.UnescapeDataString(segments[1])
            : null;
    }

    /// <summary>
    /// The rows to draw: all of <paramref name="items"/> when <paramref name="showAll"/> is set or there are at most
    /// <see cref="Limit"/>; otherwise the first <see cref="Limit"/>, plus the active row when it would be hidden, so the
    /// page the Human is on is never folded away.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="items">Every row, in order.</param>
    /// <param name="showAll">Whether the Human asked for every row.</param>
    /// <param name="isActive">Whether a row is the current page.</param>
    public static IReadOnlyList<T> Visible<T>(IReadOnlyList<T> items, bool showAll, Func<T, bool> isActive)
    {
        if (showAll || items.Count <= Limit)
        {
            return items;
        }

        List<T> visible = [.. items.Take(Limit)];
        T? active = items.Skip(Limit).FirstOrDefault(isActive);
        if (active is not null)
        {
            visible.Add(active);
        }

        return visible;
    }
}
