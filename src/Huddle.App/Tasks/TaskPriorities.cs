namespace Agency.Huddle.App.Tasks;

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
