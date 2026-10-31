namespace Agency.Huddle.App.Gateway;

/// <summary><c>Team:Gateway:Auth:RateLimit</c> (Spec Appendix A).</summary>
internal sealed class AuthRateLimitOptions
{
    /// <summary>Failures within <see cref="WindowSeconds"/> that cause a lockout.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>The sliding window, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>How long a lockout lasts, in seconds.</summary>
    public int LockoutSeconds { get; set; } = 300;

    /// <summary>Whether loopback clients are exempt from lockout.</summary>
    public bool ExemptLoopback { get; set; } = true;
}
