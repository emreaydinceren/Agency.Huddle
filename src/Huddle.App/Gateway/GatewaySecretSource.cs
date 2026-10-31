namespace Agency.Huddle.App.Gateway;

/// <summary>Where a resolved gateway secret came from, in resolution order (Spec 6.1).</summary>
internal enum GatewaySecretSource
{
    /// <summary><c>Team:Gateway:Auth:Token</c> or <c>:Password</c>.</summary>
    Configuration,

    /// <summary><c>HUDDLE_GATEWAY_TOKEN</c> or <c>HUDDLE_GATEWAY_PASSWORD</c>.</summary>
    Environment,

    /// <summary>The value stored in <c>team.db</c>.</summary>
    Stored,
}
