namespace Agency.Huddle.App.Feed;

/// <summary>What a producer calls to put an item in the Feed. Both members only enqueue, and never throw.</summary>
internal interface IFeedPublisher
{
    /// <summary>Queues one Feed item. The time is taken now, from the injected TimeProvider.</summary>
    /// <param name="message">The item to queue.</param>
    void Post(FeedMessage message);

    /// <summary>Queues the resolution of every unresolved Notification of this type about this target.</summary>
    /// <param name="targetKey">The target the Notifications are about.</param>
    /// <param name="type">The source type to resolve.</param>
    void Resolve(string targetKey, FeedSourceType type);
}
