namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>One queued Turn, with the sequence number a Stop compares against.</summary>
/// <param name="Sequence">This item's position in the Persona-wide arrival order (finding P-4).</param>
/// <param name="Item">The Turn to run.</param>
internal sealed record QueuedWork(long Sequence, WorkItem Item);
