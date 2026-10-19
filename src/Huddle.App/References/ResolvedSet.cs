namespace Agency.Huddle.App.References;

/// <summary>What a scan of a Message's text resolved to (finding 2).</summary>
/// <param name="Items">The references after the caps of D-19, in candidate order.</param>
/// <param name="EligibleCount">How many Task, Room and Team references resolved before the cap, so the header can say how many it left out.</param>
internal sealed record ResolvedSet(IReadOnlyList<ResolvedReference> Items, int EligibleCount);
