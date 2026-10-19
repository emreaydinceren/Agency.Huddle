namespace Agency.Huddle.App.Data;

/// <summary>One persisted spend total in one currency.</summary>
/// <param name="Currency">The currency code.</param>
/// <param name="Amount">The amount, in that currency.</param>
public sealed record StoredSpend(string Currency, decimal Amount);
