namespace Agency.Huddle.App.Acp;

/// <summary>An amount of money in one currency, as an Adapter reported it.</summary>
/// <param name="Amount">The amount, in <paramref name="Currency"/>.</param>
/// <param name="Currency">The currency code the Adapter reported, for example <c>USD</c>.</param>
internal sealed record SpendAmount(decimal Amount, string Currency);
