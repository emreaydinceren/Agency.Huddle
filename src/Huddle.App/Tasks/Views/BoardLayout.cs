using System.Text;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The Board's arrangement: its swimlanes, its visible columns, and how many Tasks a hidden column hides.</summary>
public sealed record BoardModel(IReadOnlyList<BoardLane> Lanes, IReadOnlyList<BoardColumn> VisibleColumns, int HiddenTaskCount);

/// <summary>One swimlane of a Board: its raw grouping key, its display label, and its cells. Key is <c>""</c> when ungrouped.</summary>
public sealed record BoardLane(string Key, string? Label, IReadOnlyList<BoardCell> Cells);

/// <summary>The Tasks in one lane that fall under one Board column.</summary>
public sealed record BoardCell(BoardColumn Column, IReadOnlyList<TaskItem> Items);

/// <summary>Builds a <see cref="BoardModel"/> from sorted Tasks and a <see cref="TaskView"/>, for Spec §12.6.</summary>
internal static class BoardLayout
{
    /// <summary>Separates the raw grouping-field values that make up a lane's Key. A control character, so it never collides with user text.</summary>
    private const string KeySeparator = "\u001f";

    /// <summary>Replaces a null grouping value in a lane's Key, so a Teammate literally named "Unassigned" cannot collide with the null group.</summary>
    private const string NullSentinel = "\u0000";

    /// <summary>The six columns Spec §12.3 shows for a new Board View.</summary>
    internal static readonly IReadOnlyList<BoardColumn> DefaultColumns =
    [
        new("Backlog", [TaskState.Backlog]),
        new("To Do", [TaskState.ToDo]),
        new("In Progress", [TaskState.InProgress]),
        new("Review", [TaskState.Review]),
        new("Done", [TaskState.Done]),
        new("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
    ];

    /// <summary>
    /// Builds the Board's lanes (from <see cref="TaskQuery.Group"/>, flattened to leaves) and columns.
    /// With no grouping, there is exactly one lane, with Key <c>""</c> and a null Label.
    /// </summary>
    /// <param name="sorted">The already-filtered and sorted Tasks to lay out.</param>
    /// <param name="view">The Board View: its grouping and its columns.</param>
    public static BoardModel Build(IReadOnlyList<TaskItem> sorted, TaskView view)
    {
        List<BoardColumn> visibleColumns = view.Columns.Where(column => !column.Hidden).ToList();
        HashSet<TaskState> hiddenStates = view.Columns
            .Where(column => column.Hidden)
            .SelectMany(column => column.States)
            .ToHashSet();
        int hiddenTaskCount = sorted.Count(task => hiddenStates.Contains(task.Status));

        TaskGroupNode root = TaskQuery.Group(sorted, view.Grouping);
        List<BoardLane> lanes = FlattenLeaves(root, [])
            .Select(leaf => BuildLane(leaf.Labels, leaf.Items, visibleColumns, view.Grouping))
            .ToList();

        return new BoardModel(lanes, visibleColumns, hiddenTaskCount);
    }

    /// <summary>The opaque zone id for one lane/column drop target: the lane's Key, Base64Url-encoded, plus the state's wire name.</summary>
    /// <param name="laneKey">The lane's raw Key, which may contain any character.</param>
    /// <param name="state">The Task state the zone represents.</param>
    public static string ZoneId(string laneKey, TaskState state) =>
        $"lane:{Base64UrlEncode(laneKey)}|state:{state.ToWire()}";

    /// <summary>Parses a zone id built by <see cref="ZoneId"/> back into its lane Key and Task state.</summary>
    /// <param name="zoneId">The zone id to parse.</param>
    /// <param name="laneKey">The decoded lane Key, or <c>""</c> when parsing fails.</param>
    /// <param name="state">The parsed Task state, or the default when parsing fails.</param>
    public static bool TryParseZone(string zoneId, out string laneKey, out TaskState state)
    {
        laneKey = "";
        state = default;

        const string lanePrefix = "lane:";
        const string stateMarker = "|state:";

        if (!zoneId.StartsWith(lanePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int markerIndex = zoneId.IndexOf(stateMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        string encodedKey = zoneId[lanePrefix.Length..markerIndex];
        string wireState = zoneId[(markerIndex + stateMarker.Length)..];

        if (!TryBase64UrlDecode(encodedKey, out string decodedKey) || !TaskStates.TryParse(wireState, out TaskState parsedState))
        {
            return false;
        }

        laneKey = decodedKey;
        state = parsedState;
        return true;
    }

    /// <summary>Recursively flattens a group tree into its leaves, each carrying the path of labels from the root and its Tasks.</summary>
    private static List<(IReadOnlyList<string> Labels, IReadOnlyList<TaskItem> Items)> FlattenLeaves(
        TaskGroupNode node, List<string> path)
    {
        if (node.Children.Count == 0)
        {
            return [(path.ToList(), node.Items)];
        }

        List<(IReadOnlyList<string> Labels, IReadOnlyList<TaskItem> Items)> leaves = [];
        foreach (TaskGroupNode child in node.Children)
        {
            path.Add(child.Label ?? "");
            leaves.AddRange(FlattenLeaves(child, path));
            path.RemoveAt(path.Count - 1);
        }

        return leaves;
    }

    /// <summary>Builds one lane: its Key from the raw grouping values, its Label from the path, and its cells from the columns.</summary>
    private static BoardLane BuildLane(
        IReadOnlyList<string> pathLabels,
        IReadOnlyList<TaskItem> items,
        IReadOnlyList<BoardColumn> visibleColumns,
        IReadOnlyList<TaskGroupField> grouping)
    {
        string key = grouping.Count == 0
            ? ""
            : string.Join(KeySeparator, grouping.Select(field => RawValue(items[0], field)));
        string? label = grouping.Count == 0 ? null : string.Join(" · ", pathLabels);

        List<BoardCell> cells = visibleColumns
            .Select(column => new BoardCell(column, items.Where(task => column.States.Contains(task.Status)).ToList()))
            .ToList();

        return new BoardLane(key, label, cells);
    }

    /// <summary>The raw (unlabelled) grouping-field value for a Task, with a null replaced by <see cref="NullSentinel"/>.</summary>
    private static string RawValue(TaskItem task, TaskGroupField field) => field switch
    {
        TaskGroupField.Team => task.Location.Team,
        TaskGroupField.Project => task.Location.Project ?? NullSentinel,
        TaskGroupField.Assignee => task.Assignee ?? NullSentinel,
        TaskGroupField.State => task.Status.ToWire(),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown grouping field."),
    };

    /// <summary>Encodes a string as Base64Url (no padding), so any character survives inside a zone id.</summary>
    private static string Base64UrlEncode(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Decodes a Base64Url string built by <see cref="Base64UrlEncode"/>.</summary>
    private static bool TryBase64UrlDecode(string encoded, out string value)
    {
        string padded = encoded.Replace('-', '+').Replace('_', '/');
        int remainder = padded.Length % 4;
        if (remainder is 2 or 3)
        {
            padded += new string('=', 4 - remainder);
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(padded);
            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            value = "";
            return false;
        }
    }
}
