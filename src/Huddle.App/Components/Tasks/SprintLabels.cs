using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// How the Tasks UI words a Sprint (Sprints spec §11.4, §12): the name, <c>S5 (missing)</c> for an id that is not in
/// <c>sprints.md</c>, and the options of the Sprint filter. Pure over an <see cref="ISprintCatalog"/>.
/// </summary>
internal static class SprintLabels
{
    /// <summary>The filter token for the Sprint with the highest number.</summary>
    public const string CurrentToken = "@current";

    /// <summary>The filter token for a Task with no Sprint.</summary>
    public const string NoneToken = "@none";

    /// <summary>The tooltip and accessible name of the warning icon beside a Sprint that is not in the file.</summary>
    public const string MissingText = "Not in sprints.md";

    /// <summary>The Sprint's name, or <c>S5 (missing)</c> when <paramref name="catalog"/> has no such Sprint.</summary>
    /// <param name="catalog">The Sprints in the file.</param>
    /// <param name="id">The Sprint a Task holds.</param>
    public static string Text(ISprintCatalog catalog, SprintId id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Get(id)?.Name ?? $"{id} (missing)";
    }

    /// <summary>
    /// The options of a Sprint filter, in order: <c>Current sprint</c>, <c>No sprint</c>, then every Sprint newest first by
    /// name, then every selected value that none of those names, as <c>S5 (missing)</c>, so it stays visible and selected.
    /// </summary>
    /// <param name="catalog">The Sprints in the file.</param>
    /// <param name="selected">The values the filter holds now.</param>
    public static List<(string Value, string Label)> FilterOptions(ISprintCatalog catalog, IEnumerable<string> selected)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selected);

        List<(string Value, string Label)> options = [(CurrentToken, "Current sprint"), (NoneToken, "No sprint")];
        options.AddRange(catalog.All
            .OrderByDescending(static sprint => sprint.Id.Number)
            .Select(static sprint => (sprint.Id.ToString(), sprint.Name)));
        foreach (string value in selected)
        {
            if (!options.Any(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                options.Add((value, $"{value} (missing)"));
            }
        }

        return options;
    }

    /// <summary>The label of one filter value, as <see cref="FilterOptions"/> words it.</summary>
    /// <param name="catalog">The Sprints in the file.</param>
    /// <param name="value">A filter value: <c>@current</c>, <c>@none</c> or an id.</param>
    public static string FilterLabel(ISprintCatalog catalog, string value) =>
        FilterOptions(catalog, [value]).First(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)).Label;
}
