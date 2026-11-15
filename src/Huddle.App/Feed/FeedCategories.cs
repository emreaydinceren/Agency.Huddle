namespace Agency.Huddle.App.Feed;

/// <summary>Which <see cref="FeedCategory"/> each <see cref="FeedSourceType"/> belongs to (spec 6.9).</summary>
internal static class FeedCategories
{
    private static readonly Dictionary<FeedCategory, IReadOnlyList<FeedSourceType>> TypesByCategory = BuildIndex();

    /// <summary>Every Category, in the order <see cref="FeedStore.Counts"/> reads their counts.</summary>
    public static IReadOnlyList<FeedCategory> Order { get; } =
        [FeedCategory.All, FeedCategory.NeedsYou, FeedCategory.Tasks, FeedCategory.Documents, FeedCategory.Rooms, FeedCategory.Gates, FeedCategory.Helpers, FeedCategory.Devices, FeedCategory.SecondBrain];

    /// <summary>
    /// The two Categories the Feed Tab's control and the panel offer, in order: All and Needs you. The kind Categories (Tasks, Rooms and the
    /// rest) still sort items for <see cref="Of"/> and the counts, but no control offers them: each item already says its kind.
    /// </summary>
    public static IReadOnlyList<FeedCategory> Choices { get; } = [FeedCategory.All, FeedCategory.NeedsYou];

    /// <summary>The offered Category a parsed one reads as: Needs you stays, anything else (an old <c>?category=rooms</c> link) reads as All.</summary>
    /// <param name="category">The Category, from an address or a stored Tab.</param>
    /// <returns>All or Needs you.</returns>
    public static FeedCategory Offered(FeedCategory category) => category == FeedCategory.NeedsYou ? FeedCategory.NeedsYou : FeedCategory.All;

    /// <summary>The label a Category carries in the Category control and the panel.</summary>
    /// <param name="category">The Category.</param>
    /// <returns>The label.</returns>
    public static string Label(FeedCategory category) => category switch
    {
        FeedCategory.NeedsYou => "Needs you",
        FeedCategory.Tasks => "Tasks",
        FeedCategory.Documents => "Documents",
        FeedCategory.Rooms => "Rooms",
        FeedCategory.Gates => "Gates",
        FeedCategory.Helpers => "Helpers",
        FeedCategory.Devices => "Devices",
        FeedCategory.SecondBrain => "Second Brain",
        _ => "All",
    };

    /// <summary>The number the Category's segment shows.</summary>
    /// <param name="counts">The seven counts.</param>
    /// <param name="category">The Category.</param>
    /// <returns>Its count.</returns>
    public static int CountOf(FeedCounts counts, FeedCategory category) => category switch
    {
        FeedCategory.NeedsYou => counts.NeedsYou,
        FeedCategory.Tasks => counts.Tasks,
        FeedCategory.Documents => counts.Documents,
        FeedCategory.Rooms => counts.Rooms,
        FeedCategory.Gates => counts.Gates,
        FeedCategory.Helpers => counts.Helpers,
        FeedCategory.Devices => counts.Devices,
        FeedCategory.SecondBrain => counts.SecondBrain,
        _ => counts.All,
    };

    /// <summary>The one type segment (Tasks, Gates, Rooms, Documents or Helpers) a source type belongs to.</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The segment.</returns>
    public static FeedCategory Of(FeedSourceType type) => type switch
    {
        FeedSourceType.TaskCreated or FeedSourceType.TaskAssignedToYou or FeedSourceType.RequestAwaitsYou or FeedSourceType.TaskWakesPaused or FeedSourceType.TaskProceeded or FeedSourceType.SprintProceeded => FeedCategory.Tasks,
        FeedSourceType.TaskGateOn or FeedSourceType.TaskNeedsYou or FeedSourceType.GateDecisionDue => FeedCategory.Gates,
        FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.RoomJob or FeedSourceType.RoomMentionHeld
            or FeedSourceType.RoomCreated or FeedSourceType.RoomJoined or FeedSourceType.RoomArchived => FeedCategory.Rooms,
        FeedSourceType.DocumentChanged => FeedCategory.Documents,
        FeedSourceType.HelperPaused or FeedSourceType.HelperFailing or FeedSourceType.TeamMemoryNotFiled or FeedSourceType.AgentsMdChangeSkipped => FeedCategory.Helpers,
        FeedSourceType.PairingRequested or FeedSourceType.AdminRequested or FeedSourceType.DevicePaired or FeedSourceType.DeviceRevoked => FeedCategory.Devices,
        FeedSourceType.SecondBrainOverCap or FeedSourceType.SpendCapReached or FeedSourceType.TeamArchived or FeedSourceType.TeamRenameStuck or FeedSourceType.BuiltinAgentUpdateAvailable or FeedSourceType.AgentConnectorsChanged => FeedCategory.SecondBrain,
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
        "helpers" => FeedCategory.Helpers,
        "devices" => FeedCategory.Devices,
        "second-brain" => FeedCategory.SecondBrain,
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
        FeedCategory.Helpers => "helpers",
        FeedCategory.Devices => "devices",
        FeedCategory.SecondBrain => "second-brain",
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
