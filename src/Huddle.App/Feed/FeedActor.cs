namespace Agency.Huddle.App.Feed;

/// <summary>Who did it: an Agent by Name, Huddle itself, or an edit made outside Huddle.</summary>
/// <param name="Kind">The kind of actor.</param>
/// <param name="Name">The actor's Name at the time, or <c>Huddle</c>.</param>
public sealed record FeedActor(FeedActorKind Kind, string Name)
{
    /// <summary>Huddle itself.</summary>
    public static FeedActor Huddle { get; } = new(FeedActorKind.Huddle, "Huddle");
}
