namespace Agency.Huddle.App.Teams;

/// <summary>The outcome of hiring a batch of drafts.</summary>
/// <param name="Error">Why the batch stopped, or <see langword="null"/> when every draft was hired.</param>
/// <param name="Hired">The names written before the batch stopped, in request order.</param>
internal sealed record DraftHireResult(string? Error, IReadOnlyList<string> Hired);
