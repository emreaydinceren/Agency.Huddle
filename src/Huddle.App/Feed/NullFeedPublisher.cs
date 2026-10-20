namespace Agency.Huddle.App.Feed;

/// <summary>The publisher registered when <c>Team:Feed:Enabled</c> is false: both members do nothing.</summary>
internal sealed class NullFeedPublisher : IFeedPublisher
{
    /// <inheritdoc/>
    public void Post(FeedMessage message)
    {
        // Disabled: the Feed keeps nothing.
    }

    /// <inheritdoc/>
    public void Resolve(string targetKey, FeedSourceType type)
    {
        // Disabled: the Feed keeps nothing.
    }
}
