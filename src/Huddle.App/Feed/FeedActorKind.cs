namespace Agency.Huddle.App.Feed;

/// <summary>Who did a Feed item's deed: a Human, a Teammate Agent, Huddle itself, or an edit made outside Huddle.</summary>
internal enum FeedActorKind
{
    /// <summary>The Human.</summary>
    Human,

    /// <summary>A Teammate that is an Agent.</summary>
    Agent,

    /// <summary>Huddle itself.</summary>
    Huddle,

    /// <summary>An edit made outside Huddle.</summary>
    OutsideHuddle,
}
