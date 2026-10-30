using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The global hub filter that closes a revoked or expired device's <c>/_blazor</c> sockets (Spec 6.8, C-5, C-6). It records each connection's device and
/// session hash, aborts a device's connections when the device is revoked, removed or rotated, and re-checks the session on every hub invocation.
/// Registered as a singleton: <c>services.AddSingleton&lt;GatewayConnectionGuard&gt;(); services.AddSignalR(o =&gt; o.AddFilter&lt;GatewayConnectionGuard&gt;());</c>.
/// </summary>
internal sealed class GatewayConnectionGuard : IHubFilter
{
    private readonly ISessionLiveness? liveness;
    private readonly Dictionary<string, Connection> connections = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Creates the guard and starts listening for devices that end.</summary>
    /// <param name="devices">The devices.</param>
    /// <param name="resolver">The session resolver; the re-check on invocation applies only when it implements <see cref="ISessionLiveness"/>.</param>
    public GatewayConnectionGuard(DeviceRegistry devices, IGatewaySessionResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(resolver);

        this.liveness = resolver as ISessionLiveness;
        devices.Subscribe(this.AbortDevice);
    }

    /// <summary>
    /// How many hub connections the device holds open. The filter records a connection in <c>OnConnectedAsync</c>, which can run
    /// a moment after the client has the handshake reply, so a test polls this before it revokes.
    /// </summary>
    /// <param name="deviceId">The device id.</param>
    public int ConnectionCount(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        lock (this.gate)
        {
            return this.connections.Values.Count(connection => string.Equals(connection.DeviceId, deviceId, StringComparison.Ordinal));
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        ClaimsPrincipal? user = context.Context.User;
        string deviceId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string sessionHash = user?.FindFirstValue(GatewayClaims.Session) ?? string.Empty;
        lock (this.gate)
        {
            this.connections[context.Context.ConnectionId] = new Connection(deviceId, sessionHash, context.Context);
        }

        await next(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        lock (this.gate)
        {
            _ = this.connections.Remove(context.Context.ConnectionId);
        }

        await next(context, exception).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        Connection? connection;
        lock (this.gate)
        {
            this.connections.TryGetValue(invocationContext.Context.ConnectionId, out connection);
        }

        if (connection is not null && this.liveness is not null && connection.SessionHash.Length > 0 && !this.liveness.IsLive(connection.SessionHash))
        {
            connection.Context.Abort();
            throw new HubException("The session has ended.");
        }

        return await next(invocationContext).ConfigureAwait(false);
    }

    private void AbortDevice(string deviceId)
    {
        List<HubCallerContext> toAbort;
        lock (this.gate)
        {
            toAbort = [.. this.connections.Values.Where(connection => string.Equals(connection.DeviceId, deviceId, StringComparison.Ordinal)).Select(connection => connection.Context)];
        }

        foreach (HubCallerContext context in toAbort)
        {
            context.Abort();
        }
    }

    private sealed record Connection(string DeviceId, string SessionHash, HubCallerContext Context);
}
