using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Seeder.Model;

/// <summary>One Feed item the seeder stores, in the Feed's own shape but with names instead of ids (Feed spec §6.3).</summary>
/// <param name="Type">What the item reports.</param>
/// <param name="Actor">Who did it, as the Feed stores the name: a Teammate, <c>You</c>, <c>Huddle</c> or a Helper's name.</param>
/// <param name="ActorKind">The actor's kind.</param>
/// <param name="Target">A Task id, a Room key, a Helper id, or a path below <c>Teams/</c> for a document.</param>
/// <param name="MinutesAgo">How long before the seed time it happened.</param>
/// <param name="Notify">Whether it is a Notification.</param>
/// <param name="ResolvedMinutesAgo">When a Notification was resolved, or <see langword="null"/> while it waits.</param>
/// <param name="Detail">The type's one free field: a Helper's reason or last error, a document's summary, a gate's message, an Action.</param>
/// <param name="Extra">The type's second field: a document's kind, a Request's kind, a gate's id, a Task's previous assignee.</param>
internal sealed record SeedFeedItem(
    FeedSourceType Type,
    string Actor,
    FeedActorKind ActorKind,
    string Target,
    int MinutesAgo,
    bool Notify = false,
    int? ResolvedMinutesAgo = null,
    string? Detail = null,
    string? Extra = null);
