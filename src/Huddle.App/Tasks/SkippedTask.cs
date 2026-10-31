namespace Agency.Huddle.App.Tasks;

/// <summary>A proposed Task that did not join the Sprint.</summary>
/// <param name="Id">The Task's id, as the manager wrote it.</param>
/// <param name="Reason">Why it was skipped.</param>
internal sealed record SkippedTask(string Id, string Reason);
