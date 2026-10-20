namespace Agency.Huddle.App.Components.Feed;

/// <summary>How a <see cref="FeedList"/> draws its items.</summary>
public enum FeedListMode
{
    /// <summary>One compact <see cref="FeedItemRow"/> per group, as the Right sidebar panel does.</summary>
    Rows,

    /// <summary>One expandable <see cref="FeedCard"/> per group, as the Feed Tab does.</summary>
    Cards,
}
