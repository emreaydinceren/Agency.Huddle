namespace Agency.Huddle.App.Data;

/// <summary>One Persona's all-time spend in one currency, summed over its Tasks.</summary>
/// <param name="PersonaName">The Persona name, as stored.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="Amount">The amount, in that currency.</param>
public sealed record PersonaSpendRow(string PersonaName, string Currency, decimal Amount);
