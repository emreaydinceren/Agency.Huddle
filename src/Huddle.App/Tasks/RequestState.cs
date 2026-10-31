namespace Agency.Huddle.App.Tasks;

/// <summary>Where a Request stands: its Task status wire name and its triage state.</summary>
/// <param name="Status">The Task status wire name; empty before the Request is filed.</param>
/// <param name="Triage">The triage state.</param>
internal sealed record RequestState(string Status, TriageState Triage);
