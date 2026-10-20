namespace Agency.Huddle.App.Tabs;

/// <summary>What the Right sidebar is showing while it is open (Feed spec §6.8).</summary>
internal enum RightSidebarMode
{
    /// <summary>The panels of the active Tab's Subject. The default.</summary>
    Subject,

    /// <summary>The Notifications panel, which belongs to no page.</summary>
    Notifications,
}
