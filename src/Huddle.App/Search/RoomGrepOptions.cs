namespace Agency.Huddle.App.Search;

/// <summary>What an Agent asked <see cref="RoomGrep"/> to find, already validated.</summary>
/// <param name="Matcher">What a Message must match; null for no query, which reads a Room's tail (or counts every Message).</param>
/// <param name="Limit">The most matching Messages to show, or Messages in a tail.</param>
internal sealed record RoomGrepOptions(RoomMatcher? Matcher, int Limit)
{
    /// <summary>Messages to show before and after each match, or null to show each match as a snippet.</summary>
    public int? Context { get; init; }

    /// <summary>Whether to count matches per Room instead of showing them.</summary>
    public bool Count { get; init; }

    /// <summary>Only this Room, by id or by name; null for every Room in scope.</summary>
    public string? Room { get; init; }

    /// <summary>Only Rooms scoped to this Team; null for any.</summary>
    public string? Team { get; init; }

    /// <summary>Only Messages whose displayed sender has this name; null for any.</summary>
    public string? Sender { get; init; }

    /// <summary>Only Messages at or after this time; null for no lower bound.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Only Messages before this time; null for no upper bound.</summary>
    public DateTimeOffset? Until { get; init; }
}
