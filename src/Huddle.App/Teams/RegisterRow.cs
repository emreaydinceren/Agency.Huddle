namespace Agency.Huddle.App.Teams;

/// <summary>One row of <c>Retro register.md</c>.</summary>
/// <param name="Id">The row id, <c>R-001</c>, <c>R-002</c>, and so on.</param>
/// <param name="Finding">What the retro found.</param>
/// <param name="Action">What was decided to do about it.</param>
/// <param name="Owner">Who owns the action.</param>
/// <param name="Status">Where the row stands.</param>
/// <param name="EvidenceNextCheck">The evidence so far, or what to check at the next retro.</param>
internal sealed record RegisterRow(string Id, string Finding, string Action, string Owner, RetroStatus Status, string EvidenceNextCheck);
