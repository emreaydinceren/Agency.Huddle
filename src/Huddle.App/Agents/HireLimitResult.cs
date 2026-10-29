namespace Agency.Huddle.App.Agents;

/// <summary>The outcome of a <see cref="HireLimits.Check"/>.</summary>
/// <param name="Allowed">True when the hire fits both limits.</param>
/// <param name="Kind">The limit that refused, or <see cref="HireLimitKind.None"/>.</param>
/// <param name="Limit">The value of the limit that refused; 0 when allowed.</param>
internal sealed record HireLimitResult(bool Allowed, HireLimitKind Kind, int Limit);
