namespace Agency.Huddle.App.Data;

/// <summary>What every Helper has used, all time: calls made, and the reported cost per currency.</summary>
/// <param name="Calls">The calls made, summed over every Helper and day.</param>
/// <param name="Cost">The reported cost, one entry per currency; empty when none was reported.</param>
public sealed record HelperSpendTotal(int Calls, IReadOnlyList<StoredSpend> Cost);
