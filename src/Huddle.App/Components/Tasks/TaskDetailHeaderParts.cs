using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>The pieces of a Task's title that <see cref="TaskDetail"/> hands to a host that draws the title in its own header.</summary>
/// <param name="TitleEditor">The title field while the title is being edited (always for a new Task), or <see langword="null"/> while the host shows the saved title.</param>
/// <param name="Trailing">The title's pencil, Copy id, closed chip and actions menu, for the end of the title's line.</param>
public sealed record TaskDetailHeaderParts(RenderFragment? TitleEditor, RenderFragment Trailing);
