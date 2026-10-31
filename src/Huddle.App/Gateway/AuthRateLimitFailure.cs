namespace Agency.Huddle.App.Gateway;

/// <summary>The outcome of <see cref="AuthRateLimiter.RecordFailure"/>.</summary>
/// <param name="LockedOut">Whether this failure started (or hit) a lockout.</param>
/// <param name="RetryAfterMs">Milliseconds until the lockout ends; 0 when not locked out.</param>
/// <param name="Delay">The progressive delay a loopback caller should wait before the response; zero otherwise.</param>
internal readonly record struct AuthRateLimitFailure(bool LockedOut, long RetryAfterMs, TimeSpan Delay);
