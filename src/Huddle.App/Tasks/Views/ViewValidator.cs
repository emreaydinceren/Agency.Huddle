namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Validates a <see cref="TaskView"/> against Spec §12.4. Pure; never throws for an invalid View.</summary>
internal static class ViewValidator
{
    /// <summary>Returns every problem with <paramref name="view"/>, or an empty list if it is valid.</summary>
    /// <param name="view">The View to validate.</param>
    /// <param name="existing">The other Views it is validated against, for name uniqueness.</param>
    public static IReadOnlyList<string> Validate(TaskView view, IReadOnlyList<TaskView> existing)
    {
        List<string> problems = [];

        ValidateName(view, existing, problems);
        ValidateFields(view, problems);
        ValidateGrouping(view, problems);
        ValidateBoardOrList(view, problems);
        ValidateSort(view, problems);

        return problems;
    }

    /// <summary>The name must be 1-60 characters, and unique ignoring case among the other Views.</summary>
    private static void ValidateName(TaskView view, IReadOnlyList<TaskView> existing, List<string> problems)
    {
        if (view.Name.Length is 0 or > 60)
        {
            problems.Add("Name must be 1-60 characters.");
        }

        bool duplicate = existing.Any(other =>
            !string.Equals(other.Id, view.Id, StringComparison.Ordinal) &&
            string.Equals(other.Name, view.Name, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            problems.Add("Another View already has this name.");
        }
    }

    /// <summary>Every field key must be known.</summary>
    private static void ValidateFields(TaskView view, List<string> problems)
    {
        foreach (string field in view.Fields)
        {
            if (!ViewFieldKeys.All.Contains(field))
            {
                problems.Add($"Unknown field '{field}'.");
            }
        }
    }

    /// <summary>There are no duplicate grouping fields, and a Board doesn't group by State.</summary>
    private static void ValidateGrouping(TaskView view, List<string> problems)
    {
        if (view.Grouping.Count != view.Grouping.Distinct().Count())
        {
            problems.Add("A field can only be grouped by once.");
        }

        if (view.Kind == ViewKind.Board && view.Grouping.Contains(TaskGroupField.State))
        {
            problems.Add("A Board cannot group by State.");
        }
    }

    /// <summary>A Board must be Active-scoped with columns covering every state once; a List has no columns.</summary>
    private static void ValidateBoardOrList(TaskView view, List<string> problems)
    {
        if (view.Kind == ViewKind.List)
        {
            if (view.Columns.Count > 0)
            {
                problems.Add("A List cannot have columns.");
            }

            return;
        }

        if (view.Scope == ViewScope.Closed)
        {
            problems.Add("A Board's scope must be Active.");
        }

        Dictionary<TaskState, int> occurrences = [];
        foreach (BoardColumn column in view.Columns)
        {
            if (column.States.Count == 0)
            {
                problems.Add($"Column '{column.Label}' must have at least one state.");
            }

            if (column.Label.Length is 0 or > 30)
            {
                problems.Add($"Column label '{column.Label}' must be 1-30 characters.");
            }

            foreach (TaskState state in column.States)
            {
                occurrences[state] = occurrences.GetValueOrDefault(state) + 1;
            }
        }

        foreach (TaskState state in TaskStates.All)
        {
            int count = occurrences.GetValueOrDefault(state);
            if (count == 0)
            {
                problems.Add($"Place {state.ToWire()} in a column.");
            }
            else if (count > 1)
            {
                problems.Add($"{state.ToWire()} appears in more than one column.");
            }
        }
    }

    /// <summary>Sort fields must be sortable, with no duplicates.</summary>
    private static void ValidateSort(TaskView view, List<string> problems)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (SortKey key in view.Sort)
        {
            if (!ViewFieldKeys.Sortable.Contains(key.Field))
            {
                problems.Add($"'{key.Field}' cannot be sorted on.");
            }

            if (!seen.Add(key.Field))
            {
                problems.Add($"'{key.Field}' is sorted on more than once.");
            }
        }
    }
}
