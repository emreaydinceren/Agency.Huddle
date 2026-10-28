using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Sidebar;

/// <summary>A Task is selected on the Tasks page; the Right sidebar reads it there.</summary>
/// <param name="Id">The selected Task's id.</param>
public sealed record TaskDetailSubject(TaskId Id) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "task-detail:" + this.Id;
}
