namespace Agency.Huddle.App.Gateway;

/// <summary>The result of classifying an ingress (Spec 8.6).</summary>
internal enum IngressKind
{
    /// <summary>A local request: a loopback socket with no forwarding headers, or no socket address at all.</summary>
    Loopback,

    /// <summary>A direct non-loopback socket.</summary>
    Direct,

    /// <summary>Arrived over the managed (Tailscale) listener with full attribution headers.</summary>
    Managed,

    /// <summary>Arrived from a configured trusted proxy.</summary>
    TrustedProxy,

    /// <summary>Refused; see <see cref="IngressResult.Error"/>.</summary>
    Rejected,
}
