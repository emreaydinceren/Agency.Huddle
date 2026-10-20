namespace Agency.Huddle.App.Feed;

/// <summary>What a Feed item says: a closed set of immutable records, one per <see cref="FeedSourceType"/> (spec 6.3).</summary>
#pragma warning disable S1694 // Spec 6.3 fixes a closed record hierarchy (the PageSubject pattern); an interface would lose the base record's value equality.
public abstract record FeedPayload;
#pragma warning restore S1694
