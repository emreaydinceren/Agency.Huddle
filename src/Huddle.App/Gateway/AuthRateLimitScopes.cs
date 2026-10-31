namespace Agency.Huddle.App.Gateway;

/// <summary>The limiter's scope names, the first half of a <c>{scope}:{ip}</c> key (Spec 6.5).</summary>
internal static class AuthRateLimitScopes
{
    /// <summary>The gateway token or password.</summary>
    public const string SharedSecret = "shared-secret";

    /// <summary>A device token.</summary>
    public const string DeviceToken = "device-token";

    /// <summary>A bootstrap token.</summary>
    public const string BootstrapToken = "bootstrap-token";

    /// <summary>A device signature.</summary>
    public const string DeviceSignature = "device-signature";

    /// <summary>A pairing request.</summary>
    public const string Pairing = "pairing";
}
