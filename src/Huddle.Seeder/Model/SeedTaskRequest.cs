namespace Agency.Huddle.Seeder.Model;

/// <summary>The Request fields of a Task that another Team filed (frontmatter <c>request</c>, <c>from_team</c>, <c>requester</c>, <c>triage</c>, <c>decline_reason</c>, <c>confirmed_by</c>).</summary>
/// <param name="Kind">The wire kind: <c>bug</c>, <c>feature</c>, <c>feedback</c> or <c>question</c>.</param>
/// <param name="FromTeam">The Team that filed it.</param>
/// <param name="Requester">The Agent who filed it.</param>
/// <param name="Triage">The wire triage state: empty, <c>needs-info</c>, <c>accepted</c> or <c>declined</c>.</param>
/// <param name="DeclineReason">Why it was declined; empty otherwise.</param>
/// <param name="ConfirmedBy">The requesting Team's QA who confirmed it; empty until then.</param>
internal sealed record SeedTaskRequest(string Kind, string FromTeam, string Requester, string Triage, string DeclineReason = "", string ConfirmedBy = "");
