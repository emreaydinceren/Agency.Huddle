namespace Agency.Huddle.App.Teams;

/// <summary>One notable event, with a link back to its source.</summary>
/// <param name="Time">When it happened.</param>
/// <param name="TaskId">The Task it concerns.</param>
/// <param name="Member">Who made the change.</param>
/// <param name="Summary">The Change log summary, verbatim.</param>
/// <param name="Link">The Task id, or a log path with a line number.</param>
internal sealed record RecordEvent(DateTimeOffset Time, string TaskId, string Member, string Summary, string Link);
