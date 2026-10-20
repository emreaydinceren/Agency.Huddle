using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Sidebar;

/// <summary>A Task is open for editing.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="HasBlockers">Whether the saved Task names a Task in Blocked by; the Blocked by panel applies only then.</param>
/// <param name="IsBlocking">Whether an open Task names this one in its Blocked by; the Blocks panel applies only then.</param>
public sealed record TaskSubject(TaskId Id, bool HasBlockers = false, bool IsBlocking = false) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "task:" + this.Id;
}
