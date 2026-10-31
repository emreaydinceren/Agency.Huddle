namespace Agency.Huddle.App.Teams;

/// <summary>A register row that the next retro must check against later evidence (RT-7).</summary>
/// <param name="RowId">The row id, <c>R-001</c> and so on.</param>
/// <param name="Finding">What the retro found.</param>
/// <param name="Action">What was decided to do about it.</param>
/// <param name="EvidenceNextCheck">The evidence so far, or what to check.</param>
internal sealed record VerificationPrompt(string RowId, string Finding, string Action, string EvidenceNextCheck);
