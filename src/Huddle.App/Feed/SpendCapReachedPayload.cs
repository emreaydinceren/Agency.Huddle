namespace Agency.Huddle.App.Feed;

/// <summary>A scope spent its cap running inside the Team, so it fell back to Ask me and Huddle asks whether to let it spend one more cap (ADR-0087).</summary>
/// <param name="Scope">The scope key: <c>global</c>, <c>team:Team</c> or <c>project:Team/Project</c>.</param>
/// <param name="Team">The Team's name; <see langword="null"/> for the global scope.</param>
/// <param name="Cap">The cap in USD.</param>
/// <param name="Message">The item text.</param>
internal sealed record SpendCapReachedPayload(string Scope, string? Team, decimal Cap, string Message) : FeedPayload;
