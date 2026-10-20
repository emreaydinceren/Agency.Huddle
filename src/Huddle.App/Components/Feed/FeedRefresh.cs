using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>What an in-place refresh of a <see cref="FeedPager"/> changed, so the list can patch the rows it holds itself.</summary>
/// <param name="Changed">The new versions of rows whose resolved or dismissed state changed.</param>
/// <param name="Removed">The ids of rows that left the list.</param>
internal sealed record FeedRefresh(IReadOnlyList<FeedItem> Changed, IReadOnlySet<long> Removed)
{
    /// <summary>A refresh that changed nothing.</summary>
    public static FeedRefresh None { get; } = new([], new HashSet<long>());

    /// <summary>Whether any row changed or left.</summary>
    public bool Any => this.Changed.Count > 0 || this.Removed.Count > 0;
}
