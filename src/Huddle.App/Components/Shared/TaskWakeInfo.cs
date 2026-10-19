namespace Agency.Huddle.App.Components.Shared;

/// <summary>A Task wake-up Message, read back into the parts a slim system row shows.</summary>
/// <param name="TaskId">The Task's id, such as <c>PLAT-0004</c>.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="Actor">Who changed the Task.</param>
/// <param name="Changes">One line per change, without the leading bullet.</param>
internal sealed record TaskWakeInfo(string TaskId, string Title, string Actor, IReadOnlyList<string> Changes);
