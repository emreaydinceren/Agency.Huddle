using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>Neighbouring Feed items drawn as one line (Feed spec §6.12): the first item leads, the rest are "N more".</summary>
/// <param name="Items">The items, newest first. Never empty.</param>
public sealed record FeedGroup(IReadOnlyList<FeedItem> Items)
{
    /// <summary>The item whose summary the line shows.</summary>
    public FeedItem Lead => this.Items[0];

    /// <summary>How many items follow the lead.</summary>
    public int More => this.Items.Count - 1;

    /// <summary>Whether any member is an unresolved Notification, which keeps the line's dot.</summary>
    public bool AnyUnresolved => this.Items.Any(static item => item.Notify && item.ResolvedAt is null);
}
