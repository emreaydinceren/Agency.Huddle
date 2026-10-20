namespace Agency.Huddle.App.Feed;

/// <summary>Which <see cref="FeedCategory"/> each <see cref="FeedSourceType"/> belongs to (spec 6.9).</summary>
internal static class FeedCategories
{
    private static readonly Dictionary<FeedCategory, IReadOnlyList<FeedSourceType>> TypesByCategory = BuildIndex();

    /// <summary>The one type segment (Tasks, Gates, Rooms or Documents) a source type belongs to.</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The segment.</returns>
    public static FeedCategory Of(FeedSourceType type) => type switch
    {
        FeedSourceType.TaskCreated or FeedSourceType.TaskAssignedToYou or FeedSourceType.RequestAwaitsYou => FeedCategory.Tasks,
        FeedSourceType.TaskGateOn or FeedSourceType.TaskNeedsYou or FeedSourceType.GateDecisionDue => FeedCategory.Gates,
        FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget
            or FeedSourceType.RoomCreated or FeedSourceType.RoomJoined or FeedSourceType.RoomArchived => FeedCategory.Rooms,
        FeedSourceType.DocumentChanged => FeedCategory.Documents,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>The source types a segment lists. All and Needs you are not defined by type, so they list none.</summary>
    /// <param name="category">The segment.</param>
    /// <returns>The source types, in enum order.</returns>
    public static IReadOnlyList<FeedSourceType> SourceTypes(FeedCategory category) =>
        TypesByCategory.TryGetValue(category, out IReadOnlyList<FeedSourceType>? types) ? types : [];

    /// <summary>Reads the <c>category=</c> query value; anything unknown reads as All.</summary>
    /// <param name="text">The query value, or <see langword="null"/> when absent.</param>
    /// <returns>The segment.</returns>
    public static FeedCategory Parse(string? text) => text switch
    {
        "needs-you" => FeedCategory.NeedsYou,
        "tasks" => FeedCategory.Tasks,
        "documents" => FeedCategory.Documents,
        "rooms" => FeedCategory.Rooms,
        "gates" => FeedCategory.Gates,
        _ => FeedCategory.All,
    };

    /// <summary>The <c>category=</c> query value of a segment, the inverse of <see cref="Parse"/>.</summary>
    /// <param name="category">The segment.</param>
    /// <returns>The query value.</returns>
    public static string ToWire(FeedCategory category) => category switch
    {
        FeedCategory.NeedsYou => "needs-you",
        FeedCategory.Tasks => "tasks",
        FeedCategory.Documents => "documents",
        FeedCategory.Rooms => "rooms",
        FeedCategory.Gates => "gates",
        _ => "all",
    };

    private static Dictionary<FeedCategory, IReadOnlyList<FeedSourceType>> BuildIndex()
    {
        Dictionary<FeedCategory, IReadOnlyList<FeedSourceType>> index = [];
        foreach (IGrouping<FeedCategory, FeedSourceType> group in Enum.GetValues<FeedSourceType>().GroupBy(Of))
        {
            index[group.Key] = [.. group];
        }

        return index;
    }
}
