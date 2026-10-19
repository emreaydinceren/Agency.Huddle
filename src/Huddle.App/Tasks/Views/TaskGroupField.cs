namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A field a View can group Tasks by. Board views cannot group by <see cref="TaskGroupField.State"/>.</summary>
public enum TaskGroupField
{
    /// <summary>Group by <see cref="TaskLocation.Team"/>.</summary>
    Team,

    /// <summary>Group by <see cref="TaskLocation.Project"/>.</summary>
    Project,

    /// <summary>Group by <see cref="TaskItem.Assignee"/>.</summary>
    Assignee,

    /// <summary>Group by <see cref="TaskItem.Status"/>. Invalid on a Board.</summary>
    State,

    /// <summary>Group by <see cref="TaskItem.Sprint"/>, newest first, with "No sprint" last.</summary>
    Sprint,
}
