namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Whether a View keeps Tasks held up by an open blocker (<see cref="TaskItem.BlockedBy"/>), the ones that are not, or both.</summary>
public enum BlockedFilter
{
    /// <summary>Blocked and unblocked Tasks alike.</summary>
    All,

    /// <summary>Only Tasks with at least one blocker that exists and is not terminal.</summary>
    Blocked,

    /// <summary>Only Tasks with no blockers, only terminal blockers, or only blockers that no longer exist.</summary>
    Unblocked,
}
