namespace Agency.Huddle.App.Tasks;

/// <summary>An open Task whose description links to a Library document, shown beside the notes in the
/// Backlinks panel: one row per Task, for the first line that links.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="State">The Task's state, for its dot.</param>
/// <param name="LineText">The description line that links: the attached path, or the line holding the <c>[[wikilink]]</c>.</param>
internal sealed record TaskBacklink(TaskId Id, string Title, TaskState State, string LineText);
