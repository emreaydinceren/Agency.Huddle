namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The real <see cref="IRemoteAccessState"/>: reads the gateway options as they are running, not the overrides stored for the next start
/// (Spec 6.15), so turning remote access on in Settings changes nothing until Huddle restarts.
/// </summary>
/// <param name="running">The options the gateway runs with.</param>
internal sealed class RemoteAccessState(GatewayOptions running) : IRemoteAccessState
{
    private const string Loopback = "loopback";

    private const string Off = "off";

    /// <inheritdoc />
    public bool IsConfigured =>
        !string.Equals(running.Bind, Loopback, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(running.Tailscale.Mode, Off, StringComparison.OrdinalIgnoreCase);
}
