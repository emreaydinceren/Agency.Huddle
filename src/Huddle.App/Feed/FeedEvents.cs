namespace Agency.Huddle.App.Feed;

/// <summary>
/// A plain hub, like <c>TaskEvents</c>: the <see cref="FeedWriter"/> raises these after a batch commits, and the
/// Notifications button, its panel and the Feed Tab subscribe. The <c>Raise</c> methods are internal so component
/// tests can drive the UI without a writer.
/// </summary>
internal sealed class FeedEvents
{
    /// <summary>Raised after a batch commits, with the rows it inserted.</summary>
    public event Action<IReadOnlyList<FeedItem>>? ItemsAdded;

    /// <summary>Raised once after a batch whose Resolve, MarkSeen or Dismiss touched a row.</summary>
    public event Action? ItemsChanged;

    /// <summary>Invokes every <see cref="ItemsAdded"/> subscriber in turn.</summary>
    /// <param name="items">The inserted rows.</param>
    internal void RaiseItemsAdded(IReadOnlyList<FeedItem> items) => this.ItemsAdded?.Invoke(items);

    /// <summary>Invokes every <see cref="ItemsChanged"/> subscriber in turn.</summary>
    internal void RaiseItemsChanged() => this.ItemsChanged?.Invoke();
}
