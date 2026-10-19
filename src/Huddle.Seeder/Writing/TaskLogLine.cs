namespace Agency.Huddle.Seeder.Writing;

/// <summary>One line of a Task's Change log.</summary>
/// <param name="At">When it happened, in UTC, to the minute.</param>
/// <param name="Actor">Who did it.</param>
/// <param name="Summary">What happened, in the app's wording.</param>
internal sealed record TaskLogLine(DateTimeOffset At, string Actor, string Summary);
