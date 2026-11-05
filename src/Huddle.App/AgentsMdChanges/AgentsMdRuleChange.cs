namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>One changed rule in a Helper's summary.</summary>
/// <param name="Kind">The change: <c>added</c>, <c>removed</c> or <c>changed</c>.</param>
/// <param name="Summary">The rule, in a sentence.</param>
/// <param name="Quote">A verified quote from the file.</param>
internal sealed record AgentsMdRuleChange(string Kind, string Summary, string Quote);
