namespace Agency.Huddle.App.Teams;

/// <summary>Tool calls of one member and tool since the cutoff.</summary>
/// <param name="Member">The member whose log was read.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Calls">How many calls were logged.</param>
/// <param name="Failures">How many of them failed.</param>
internal sealed record ToolCallSummary(string Member, string Tool, int Calls, int Failures);
