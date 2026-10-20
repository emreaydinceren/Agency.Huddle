namespace Agency.Huddle.App.Feed;

/// <summary>One page of the Feed, newest first. Keyset by id, so live inserts never shift a page.</summary>
/// <param name="BeforeId">Only items with a lower id, or <see langword="null"/> for the first page.</param>
/// <param name="Limit">The most items to return.</param>
/// <param name="Teammate">Only items by this actor (case-insensitive), or <see langword="null"/>.</param>
/// <param name="Team">Only items about this Team (case-insensitive), or <see langword="null"/>.</param>
/// <param name="Category">The segment to list.</param>
internal sealed record FeedQuery(long? BeforeId, int Limit, string? Teammate, string? Team, FeedCategory Category = FeedCategory.All);
