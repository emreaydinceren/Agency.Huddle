namespace Agency.Huddle.App.Feed;

/// <summary>The wire names of <see cref="FeedSourceType"/>, both ways.</summary>
internal static class FeedSourceTypes
{
    private static readonly Dictionary<string, FeedSourceType> ByWire = BuildIndex();

    /// <summary>The wire name the <c>source_type</c> column holds.</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(FeedSourceType type) => type switch
    {
        FeedSourceType.TaskCreated => "task-created",
        FeedSourceType.TaskGateOn => "task-gate-on",
        FeedSourceType.TaskNeedsYou => "task-needs-you",
        FeedSourceType.TaskAssignedToYou => "task-assigned-to-you",
        FeedSourceType.RequestAwaitsYou => "request-awaits-you",
        FeedSourceType.GateDecisionDue => "gate-decision-due",
        FeedSourceType.RoomMention => "room-mention",
        FeedSourceType.RoomQuestion => "room-question",
        FeedSourceType.RoomForm => "room-form",
        FeedSourceType.RoomBudget => "room-budget",
        FeedSourceType.RoomCreated => "room-created",
        FeedSourceType.RoomJoined => "room-joined",
        FeedSourceType.RoomArchived => "room-archived",
        FeedSourceType.DocumentChanged => "document-changed",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Reads a wire name back.</summary>
    /// <param name="wire">The text in the column.</param>
    /// <param name="type">The source type when the name is known.</param>
    /// <returns><see langword="true"/> when this build knows the name; a newer build's name is <see langword="false"/>.</returns>
    public static bool TryParse(string? wire, out FeedSourceType type)
    {
        type = default;
        return wire is not null && ByWire.TryGetValue(wire, out type);
    }

    private static Dictionary<string, FeedSourceType> BuildIndex()
    {
        Dictionary<string, FeedSourceType> index = new(StringComparer.Ordinal);
        foreach (FeedSourceType type in Enum.GetValues<FeedSourceType>())
        {
            index.Add(ToWire(type), type);
        }

        return index;
    }
}
