namespace Agency.Huddle.App.Teams;

/// <summary>One Request (a Task with Request fields) that changed since the cutoff.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Request title.</param>
/// <param name="Kind">The Request kind (Bug, Feature, Feedback or Question).</param>
/// <param name="FromTeam">The requesting Team.</param>
/// <param name="Triage">The triage state (None, NeedsInfo, Accepted or Declined).</param>
/// <param name="Status">The Task status, by its wire name.</param>
internal sealed record RequestRecord(string Id, string Title, string Kind, string FromTeam, string Triage, string Status);
