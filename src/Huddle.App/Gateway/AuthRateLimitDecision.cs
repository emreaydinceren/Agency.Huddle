namespace Agency.Huddle.App.Gateway;

/// <summary>The answer to <see cref="AuthRateLimiter.Check"/>.</summary>
/// <param name="Allowed"><see langword="false"/> while the key is locked out.</param>
/// <param name="RetryAfterMs">Milliseconds until the lockout ends; 0 when allowed.</param>
internal readonly record struct AuthRateLimitDecision(bool Allowed, long RetryAfterMs);
