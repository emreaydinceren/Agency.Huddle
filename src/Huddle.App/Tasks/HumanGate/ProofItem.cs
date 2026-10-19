using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>One line of a Task's Proof: a Definition of Done item and whether the Task meets it.</summary>
/// <param name="Label">The name shown for the item.</param>
/// <param name="Met">Whether the item is met.</param>
/// <param name="Owner">Who has to act when the item is open, or <see langword="null"/> when nobody can be named or the item is met.</param>
/// <param name="Detail">Why the item is open (Spec §6.3), or <see langword="null"/> when there is nothing to add.</param>
/// <param name="Source">Where the item came from.</param>
public sealed record ProofItem(string Label, bool Met, string? Owner, string? Detail, ProofItemSource Source);
