namespace Agency.Huddle.App.Gateway;

/// <summary>The decision behind <c>POST /auth/connect</c> (Spec 8.3).</summary>
/// <param name="Result">Connected, pairing required, or failed.</param>
/// <param name="HttpStatus">200 for connected and pairing required; 401, 429 or 503 for a failure.</param>
/// <param name="Code">The OpenClaw-style error or pairing code; <see langword="null"/> when connected.</param>
/// <param name="RetryAfterMs">The lockout time left, on 429.</param>
/// <param name="RequestId">The pairing request id, when pairing is required.</param>
/// <param name="DeviceId">The device id, when connected or pairing is required.</param>
/// <param name="DeviceToken">A device token issued by this handshake (new pairing, or an approval just delivered); otherwise <see langword="null"/>.</param>
/// <param name="Scopes">The scopes of the session, when connected.</param>
/// <param name="PairedVia">How the device was paired, when connected.</param>
internal sealed record ConnectOutcome(
    ConnectOutcome.Kind Result,
    int HttpStatus,
    string? Code,
    long? RetryAfterMs,
    string? RequestId,
    string? DeviceId,
    string? DeviceToken,
    IReadOnlyList<string> Scopes,
    string? PairedVia)
{
    /// <summary>
    /// How long a loopback caller must wait before the answer to a failure is sent (the limiter's progressive delay, Spec 6.5);
    /// zero for everything else. The endpoint applies it.
    /// </summary>
    public TimeSpan Delay { get; init; }

    /// <summary>The gateway token was wrong.</summary>
    public const string TokenMismatch = "AUTH_TOKEN_MISMATCH";

    /// <summary>The gateway password was wrong.</summary>
    public const string PasswordMismatch = "AUTH_PASSWORD_MISMATCH";

    /// <summary>The device token was wrong, revoked or belongs to another device.</summary>
    public const string DeviceTokenMismatch = "AUTH_DEVICE_TOKEN_MISMATCH";

    /// <summary>The bootstrap token is unknown, spent by another device, burned or expired.</summary>
    public const string BootstrapTokenInvalid = "AUTH_BOOTSTRAP_TOKEN_INVALID";

    /// <summary>Token mode, no credential.</summary>
    public const string TokenMissing = "AUTH_TOKEN_MISSING";

    /// <summary>Password mode, no credential.</summary>
    public const string PasswordMissing = "AUTH_PASSWORD_MISSING";

    /// <summary>Mode none away from loopback, or no gateway auth possible.</summary>
    public const string AuthRequired = "AUTH_REQUIRED";

    /// <summary>The device must be approved.</summary>
    public const string PairingRequired = "PAIRING_REQUIRED";

    /// <summary>Ten requests are already pending.</summary>
    public const string PairingQueueFull = "PAIRING_QUEUE_FULL";

    /// <summary>The three results.</summary>
    public enum Kind
    {
        /// <summary>A session may be issued.</summary>
        Connected,

        /// <summary>A request is open (200, <see cref="PairingRequired"/>).</summary>
        PairingRequired,

        /// <summary>Refused.</summary>
        Failed,
    }
}
