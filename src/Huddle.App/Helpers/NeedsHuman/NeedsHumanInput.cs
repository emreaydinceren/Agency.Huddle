using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>Everything the Needs Human Helper reads about a Task, captured when the Teammate saved it.</summary>
/// <param name="Id">The Task.</param>
/// <param name="Fingerprint">The Task's content fingerprint at that moment; a Verdict applies only while the Task still has it.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description, with its Evidence section.</param>
/// <param name="Team">The Team label.</param>
/// <param name="Tags">The tags.</param>
internal sealed record NeedsHumanInput(TaskId Id, string Fingerprint, string Title, string Description, string Team, IReadOnlyList<string> Tags);
