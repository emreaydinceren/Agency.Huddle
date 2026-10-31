namespace Agency.Huddle.App.Teams;

/// <summary>One line of a Team's Definition of Done checklist (ADR-0062).</summary>
/// <param name="Label">The name shown for the item.</param>
/// <param name="Rule">The rule that decides when the item is met.</param>
/// <param name="Source">Where the item came from.</param>
internal sealed record DoneItem(string Label, DoneRule Rule, ProofItemSource Source);
