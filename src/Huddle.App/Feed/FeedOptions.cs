namespace Agency.Huddle.App.Feed;

/// <summary>Configuration for the Feed, bound from <c>Team:Feed</c> (Feed spec 6.11). Public because <see cref="TeamOptions"/> is.</summary>
public sealed class FeedOptions
{
    /// <summary>Off: no publisher, no writer, no button, no Feed link.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A dismissed Notification is deleted this many days after its dismissal. Zero or less keeps it forever.</summary>
    public int DismissedRetentionDays { get; set; } = 30;

    /// <summary>An activity item is deleted this many days after it was created. Zero or less keeps it forever.</summary>
    public int ActivityRetentionDays { get; set; } = 30;

    /// <summary>How often the clean-up runs.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Commands waiting for the writer; beyond it, new ones are dropped and logged.</summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>Most commands one transaction applies.</summary>
    public int BatchSize { get; set; } = 200;

    /// <summary>Items per Feed Tab page.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>Most Notifications the panel shows.</summary>
    public int PanelLimit { get; set; } = 50;
}
