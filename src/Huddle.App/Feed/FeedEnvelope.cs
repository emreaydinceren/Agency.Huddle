namespace Agency.Huddle.App.Feed;

/// <summary>What <see cref="FeedPayloads.Read"/> found in a <c>payload</c> column: either part may be missing, and reading never throws.</summary>
/// <param name="ActorKind">The actor's kind, or <see langword="null"/> when the envelope was unreadable.</param>
/// <param name="Payload">The payload, or <see langword="null"/> when its data did not deserialise or its source type is unknown.</param>
internal readonly record struct FeedEnvelope(FeedActorKind? ActorKind, FeedPayload? Payload);
