using System.Collections.ObjectModel;

namespace Agency.Huddle.App.Tasks;

/// <summary>The lifecycle state of a Task.</summary>
public enum TaskState
{
    Backlog,
    ToDo,
    InProgress,
    Review,
    Done,
    Cancelled,
    Duplicate,
    Rejected,
}

/// <summary>Extension methods for <see cref="TaskState"/>.</summary>
public static class TaskStates
{
    /// <summary>The wire names for each state: "To Do", "In Progress", etc.</summary>
    private static readonly Dictionary<TaskState, string> WireNames = new()
    {
        { TaskState.Backlog, "Backlog" },
        { TaskState.ToDo, "To Do" },
        { TaskState.InProgress, "In Progress" },
        { TaskState.Review, "Review" },
        { TaskState.Done, "Done" },
        { TaskState.Cancelled, "Cancelled" },
        { TaskState.Duplicate, "Duplicate" },
        { TaskState.Rejected, "Rejected" },
    };

    /// <summary>Declaration order: Backlog, ToDo, InProgress, Review, Done, Cancelled, Duplicate, Rejected.</summary>
    private static readonly IReadOnlyList<TaskState> AllStates = new ReadOnlyCollection<TaskState>(
    [
        TaskState.Backlog,
        TaskState.ToDo,
        TaskState.InProgress,
        TaskState.Review,
        TaskState.Done,
        TaskState.Cancelled,
        TaskState.Duplicate,
        TaskState.Rejected,
    ]);

    /// <summary>All states in declaration order.</summary>
    public static IReadOnlyList<TaskState> All => AllStates;

    /// <summary>Returns true for Done, Cancelled, Duplicate, Rejected.</summary>
    public static bool IsTerminal(this TaskState state) =>
        state is TaskState.Done or TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected;

    /// <summary>Returns true for Cancelled, Duplicate, Rejected (the "Won't do" family).</summary>
    public static bool IsWontDo(this TaskState state) =>
        state is TaskState.Cancelled or TaskState.Duplicate or TaskState.Rejected;

    /// <summary>Returns the wire name for the state.</summary>
    public static string ToWire(this TaskState state) =>
        WireNames.TryGetValue(state, out string? wire) ? wire : state.ToString();

    /// <summary>Parses a wire name or enum identifier (case-insensitive, trims, accepts snake_case).</summary>
    public static bool TryParse(string? wire, out TaskState state)
    {
        if (wire is null)
        {
            state = default;
            return false;
        }

        wire = wire.Trim();
        if (wire.Length == 0)
        {
            state = default;
            return false;
        }

        // Try exact match against wire names first
        foreach (var (stateValue, wireName) in WireNames)
        {
            if (string.Equals(wire, wireName, StringComparison.OrdinalIgnoreCase))
            {
                state = stateValue;
                return true;
            }
        }

        // Normalize: remove spaces and underscores, compare case-insensitively
        string normalized = NormalizeInput(wire);
        foreach (var (stateValue, wireName) in WireNames)
        {
            string normalizedWire = NormalizeInput(wireName);
            if (string.Equals(normalized, normalizedWire, StringComparison.OrdinalIgnoreCase))
            {
                state = stateValue;
                return true;
            }
        }

        // Try enum identifier (e.g., "ToDo")
        foreach (var (stateValue, _) in WireNames)
        {
            string enumName = stateValue.ToString();
            string normalizedEnum = NormalizeInput(enumName);
            if (string.Equals(normalized, normalizedEnum, StringComparison.OrdinalIgnoreCase))
            {
                state = stateValue;
                return true;
            }
        }

        state = default;
        return false;
    }

    /// <summary>Removes spaces and underscores for normalization.</summary>
    private static string NormalizeInput(string input) =>
        input.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
}

/// <summary>The priority level of a Task.</summary>
public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

/// <summary>Extension methods for <see cref="TaskPriority"/>.</summary>
public static class TaskPriorities
{
    /// <summary>The wire names for each priority.</summary>
    private static readonly Dictionary<TaskPriority, string> WireNames = new()
    {
        { TaskPriority.Low, "Low" },
        { TaskPriority.Medium, "Medium" },
        { TaskPriority.High, "High" },
        { TaskPriority.Urgent, "Urgent" },
    };

    /// <summary>Returns the wire name for the priority.</summary>
    public static string ToWire(this TaskPriority priority) =>
        WireNames.TryGetValue(priority, out string? wire) ? wire : priority.ToString();

    /// <summary>Parses a wire name or enum identifier (case-insensitive, trims, accepts snake_case).</summary>
    public static bool TryParse(string? wire, out TaskPriority priority)
    {
        if (wire is null)
        {
            priority = default;
            return false;
        }

        wire = wire.Trim();
        if (wire.Length == 0)
        {
            priority = default;
            return false;
        }

        // Try exact match against wire names first
        foreach (var (priorityValue, wireName) in WireNames)
        {
            if (string.Equals(wire, wireName, StringComparison.OrdinalIgnoreCase))
            {
                priority = priorityValue;
                return true;
            }
        }

        // Normalize: remove spaces and underscores, compare case-insensitively
        string normalized = NormalizeInput(wire);
        foreach (var (priorityValue, wireName) in WireNames)
        {
            string normalizedWire = NormalizeInput(wireName);
            if (string.Equals(normalized, normalizedWire, StringComparison.OrdinalIgnoreCase))
            {
                priority = priorityValue;
                return true;
            }
        }

        // Try enum identifier (e.g., "Low")
        foreach (var (priorityValue, _) in WireNames)
        {
            string enumName = priorityValue.ToString();
            string normalizedEnum = NormalizeInput(enumName);
            if (string.Equals(normalized, normalizedEnum, StringComparison.OrdinalIgnoreCase))
            {
                priority = priorityValue;
                return true;
            }
        }

        priority = default;
        return false;
    }

    /// <summary>Removes spaces and underscores for normalization.</summary>
    private static string NormalizeInput(string input) =>
        input.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
}
