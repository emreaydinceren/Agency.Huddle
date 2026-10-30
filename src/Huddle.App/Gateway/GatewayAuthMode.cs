namespace Agency.Huddle.App.Gateway;

/// <summary>What a connection must present to the gateway (Spec 6.5); <c>Team:Gateway:Auth:Mode</c>.</summary>
internal enum GatewayAuthMode
{
    /// <summary>The gateway token, a device token or a bootstrap token. The default.</summary>
    Token,

    /// <summary>The gateway password or a device token.</summary>
    Password,

    /// <summary>Nothing; identity comes from a trusted proxy. Configured as <c>trusted-proxy</c>.</summary>
    TrustedProxy,

    /// <summary>Nothing; loopback only.</summary>
    None,
}
