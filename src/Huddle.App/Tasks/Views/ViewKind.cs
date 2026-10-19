namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The layout a View renders as: a data grid or a Kanban board.</summary>
public enum ViewKind
{
    /// <summary>A data grid, one row per Task.</summary>
    List,

    /// <summary>A Kanban board, one column per <see cref="BoardColumn"/>. Saved as "kanban"; the old "board" still reads.</summary>
    Kanban,
}
