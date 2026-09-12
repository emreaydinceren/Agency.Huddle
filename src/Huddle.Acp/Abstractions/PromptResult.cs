namespace Agency.Huddle.Acp.Abstractions;

/// <summary>The outcome of a completed prompt turn.</summary>
public sealed record PromptResult(StopReason StopReason);