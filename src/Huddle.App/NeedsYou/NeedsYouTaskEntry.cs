using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>One gated Task waiting on the Human (Human gate Spec §6.7).</summary>
/// <param name="TaskId">The Task's id.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="Since">When the Task last moved into Needs You.</param>
/// <param name="Proof">The Task's Proof, evaluated when the entry was read.</param>
internal sealed record NeedsYouTaskEntry(TaskId TaskId, string Title, DateTimeOffset Since, Proof Proof);
