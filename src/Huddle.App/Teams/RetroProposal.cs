namespace Agency.Huddle.App.Teams;

/// <summary>One proposed change of a retro report.</summary>
/// <param name="Id">The proposal's id within the report.</param>
/// <param name="Title">A short name for the change.</param>
/// <param name="TargetFile">The file the change edits.</param>
/// <param name="Diff">The change as unified diff text.</param>
internal sealed record RetroProposal(string Id, string Title, string TargetFile, string Diff);
