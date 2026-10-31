namespace Agency.Huddle.App.Gateway;

/// <summary>A pending pairing request (Spec 6.7). Held in memory by <see cref="PairingService"/>.</summary>
internal sealed class PairingRequest
{
    /// <summary>The request id, 32 random bytes as base64url.</summary>
    public required string RequestId { get; init; }

    /// <summary>The device id.</summary>
    public required string DeviceId { get; init; }

    /// <summary>The 32 key bytes.</summary>
    public required byte[] PublicKey { get; init; }

    /// <summary>The scopes asked for, normalised.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>The device name, from platform and device family.</summary>
    public required string Name { get; init; }

    /// <summary>The client address.</summary>
    public required string Address { get; init; }

    /// <summary>The user agent.</summary>
    public string? UserAgent { get; init; }

    /// <summary>The ingress kind, as text.</summary>
    public required string Ingress { get; init; }

    /// <summary>The Tailscale login, if any.</summary>
    public string? TailscaleLogin { get; init; }

    /// <summary>When it was opened.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it lapses: five minutes after <see cref="CreatedAt"/>.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Whether it asks for <see cref="GatewayScopes.Admin"/> (the Feed's <c>AdminRequested</c>).</summary>
    public bool IsAdminRequest => this.Scopes.Contains(GatewayScopes.Admin, StringComparer.Ordinal);
}
