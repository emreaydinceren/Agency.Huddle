namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Which Tasks a View considers: the open ones, or the closed ones.</summary>
public enum ViewScope
{
    /// <summary>Tasks that are not closed.</summary>
    Active,

    /// <summary>Tasks that are closed.</summary>
    Closed,
}
