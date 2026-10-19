namespace Agency.Huddle.App.Tasks;

/// <summary>One entry in a Task's change log.</summary>
public sealed record ChangeLogEntry(DateTimeOffset At, string Actor, string Summary);
