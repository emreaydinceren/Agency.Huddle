namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The real <see cref="IRemoteAccessState"/>: reads the gateway options as they are running, not the overrides stored for the next start
/// (Spec 6.15), so turning remote access on in Settings changes nothing until Huddle restarts. Every remote exposure counts: a network bind,
/// Tailscale, trusted-proxy auth, a listed trusted proxy, or a bound app endpoint that is not loopback (R-5). Under strict local mode nothing
/// remote is served, so it is never configured.
/// </summary>
/// <param name="running">The options the gateway runs with.</param>
/// <param name="appEndpoints">The bound app endpoints, read when asked (they are known only after start).</param>
internal sealed class RemoteAccessState(GatewayOptions running, Func<IReadOnlyList<System.Net.IPEndPoint>>? appEndpoints = null) : IRemoteAccessState
{
    private const string Loopback = "loopback";

    private const string Off = "off";

    /// <inheritdoc />
    public bool IsConfigured =>
        !running.StrictLocal
        && (!string.Equals(running.Bind, Loopback, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(running.Tailscale.Mode, Off, StringComparison.OrdinalIgnoreCase)
        || running.Auth.Mode == GatewayAuthMode.TrustedProxy
        || running.TrustedProxies.Count > 0
        || (appEndpoints?.Invoke().Any(static endpoint => !System.Net.IPAddress.IsLoopback(endpoint.Address)) ?? false));
}
