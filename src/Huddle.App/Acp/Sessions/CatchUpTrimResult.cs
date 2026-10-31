using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The outcome of <see cref="CatchUpTrim.Apply"/>: the Messages kept in input order, how many were dropped (and how many of those were
/// the receiving Agent's own), and the estimated tokens of what is kept (LR-R11, corrections C6 and C7).
/// </summary>
/// <param name="Kept">The Messages kept, in the order they were given.</param>
/// <param name="Dropped">How many Messages were dropped by either pass.</param>
/// <param name="DroppedOwn">How many of the dropped Messages were the receiving Agent's own.</param>
/// <param name="EstimatedTokens">The sum of <see cref="TokenEstimate.OfMessage"/> over <paramref name="Kept"/>.</param>
internal sealed record CatchUpTrimResult(IReadOnlyList<ChatMessage> Kept, int Dropped, int DroppedOwn, int EstimatedTokens);
