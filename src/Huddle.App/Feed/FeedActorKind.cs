namespace Agency.Huddle.App.Feed;

/// <summary>Who did a Feed item's deed: a Human, a Teammate Agent, Huddle itself, a Helper, or an edit made outside Huddle.</summary>
public enum FeedActorKind
{
    /// <summary>The Human.</summary>
    Human,

    /// <summary>A Teammate that is an Agent.</summary>
    Agent,

    /// <summary>Huddle itself.</summary>
    Huddle,

    /// <summary>An edit made outside Huddle.</summary>
    OutsideHuddle,

    /// <summary>A Helper: a background judgement Huddle applied. It is not a Teammate.</summary>
    Helper,
}
