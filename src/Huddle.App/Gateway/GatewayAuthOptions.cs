namespace Agency.Huddle.App.Gateway;

/// <summary><c>Team:Gateway:Auth</c> (Spec Appendix A).</summary>
internal sealed class GatewayAuthOptions
{
    /// <summary>
    /// The auth mode as configured text: <c>token</c>, <c>password</c>, <c>trusted-proxy</c> or <c>none</c>, in any case.
    /// Bound from <c>Mode</c>; text rather than the enum because the binder cannot read the hyphenated value.
    /// </summary>
    [ConfigurationKeyName("Mode")]
    public string ModeText { get; set; } = "token";

    /// <summary>The auth mode parsed from <see cref="ModeText"/>.</summary>
    /// <exception cref="InvalidOperationException">The text names no mode.</exception>
    [ConfigurationKeyName("ModeParsed")]
    public GatewayAuthMode Mode =>
        Enum.TryParse(this.ModeText.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true, out GatewayAuthMode mode)
            ? mode
            : throw new InvalidOperationException($"Team:Gateway:Auth:Mode '{this.ModeText}' is not token, password, trusted-proxy or none.");

    /// <summary>The configured gateway token, or <see langword="null"/>.</summary>
    public string? Token { get; set; }

    /// <summary>The configured gateway password, or <see langword="null"/>.</summary>
    public string? Password { get; set; }

    /// <summary>Whether a verified Tailscale identity stands in for gateway auth.</summary>
    public bool AllowTailscale { get; set; } = true;

    /// <summary>The rate limiter's settings.</summary>
    public AuthRateLimitOptions RateLimit { get; set; } = new();
}
