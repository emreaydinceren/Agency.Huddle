using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The singleton that knows the bound listeners (Spec 6.2; corrections D-2, D-5): it applies the <see cref="GatewayListenerPlan"/> to Kestrel, records
/// the managed endpoint and the app URL after start and tells the gate which listener a connection arrived on.
/// </summary>
internal sealed class GatewayListener
{
    private readonly List<(GatewayListenerPlan.Role Role, ListenOptions Options, bool Https)> pending = [];

    /// <summary>The bound managed (Tailscale) loopback endpoint, or <see langword="null"/> when there is none.</summary>
    public IPEndPoint? ManagedEndpoint { get; private set; }

    /// <summary>The app's own URL (first app endpoint, any-address mapped to <c>127.0.0.1</c>), or <see langword="null"/> before start.</summary>
    public string? AppUrl { get; private set; }

    /// <summary>The bound app endpoints (an any-address bind shows as <c>0.0.0.0</c> or <c>[::]</c>).</summary>
    public IReadOnlyList<IPEndPoint> AppEndpoints { get; private set; } = [];

    /// <summary>Why the app is loopback-only or remote access is unavailable, or <see langword="null"/>.</summary>
    public string? Reason { get; private set; }

    /// <summary>Records the bound managed endpoint.</summary>
    /// <param name="endpoint">The endpoint Kestrel bound.</param>
    public void RecordManaged(IPEndPoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        this.ManagedEndpoint = endpoint;
    }

    /// <summary>Records the app URL.</summary>
    /// <param name="url">The first app address.</param>
    public void RecordApp(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        this.AppUrl = url
            .Replace("//0.0.0.0:", "//127.0.0.1:", StringComparison.Ordinal)
            .Replace("//[::]:", "//127.0.0.1:", StringComparison.Ordinal)
            .Replace("//+:", "//127.0.0.1:", StringComparison.Ordinal)
            .Replace("//*:", "//127.0.0.1:", StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="IngressListenerKind.Managed"/> when the connection's local address (IPv4-mapped normalised) and port equal <see cref="ManagedEndpoint"/>;
    /// otherwise <see cref="IngressListenerKind.App"/> (a null local address or port 0, as a <c>TestServer</c> has, is App).
    /// </summary>
    /// <param name="connection">The connection.</param>
    public IngressListenerKind KindOf(ConnectionInfo connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        IPEndPoint? managed = this.ManagedEndpoint;
        IPAddress? local = connection.LocalIpAddress;
        if (managed is null || local is null || connection.LocalPort == 0 || connection.LocalPort != managed.Port)
        {
            return IngressListenerKind.App;
        }

        return Normalise(local).Equals(Normalise(managed.Address)) ? IngressListenerKind.Managed : IngressListenerKind.App;
    }

    /// <summary>Adds each planned endpoint to Kestrel (D-3) and keeps its <see cref="ListenOptions"/> to read the bound port after start (D-5).</summary>
    /// <param name="options">The Kestrel options being configured.</param>
    /// <param name="plan">The plan.</param>
    internal void Apply(KestrelServerOptions options, GatewayListenerPlan.Result plan)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(plan);

        this.Reason = plan.Reason;
        foreach (GatewayListenerPlan.Endpoint endpoint in plan.Endpoints)
        {
            Action<ListenOptions> capture = listen =>
            {
                if (endpoint.Https)
                {
                    _ = listen.UseHttps();
                }

                this.pending.Add((endpoint.Role, listen, endpoint.Https));
            };
            switch (endpoint.Bind)
            {
                case GatewayListenerPlan.Bind.Localhost:
                    options.ListenLocalhost(endpoint.Port, capture);
                    break;
                case GatewayListenerPlan.Bind.Address when endpoint.Address is { } address:
                    options.Listen(address, endpoint.Port, capture);
                    break;
                default:
                    options.ListenAnyIP(endpoint.Port, capture);
                    break;
            }
        }
    }

    /// <summary>Reads the bound ports back from the kept <see cref="ListenOptions"/> once Kestrel has started (D-5).</summary>
    internal void ReadBoundEndpoints()
    {
        List<IPEndPoint> app = [];
        bool https = false;
        foreach ((GatewayListenerPlan.Role role, ListenOptions listen, bool tls) in this.pending)
        {
            if (listen.IPEndPoint is not { } bound)
            {
                continue;
            }

            if (role == GatewayListenerPlan.Role.Managed)
            {
                this.RecordManaged(bound);
            }
            else
            {
                https = app.Count == 0 ? tls : https;
                app.Add(bound);
            }
        }

        this.AppEndpoints = app;
        if (app.Count > 0)
        {
            this.RecordApp(UrlOf(app[0], https));
        }
    }

    private static string UrlOf(IPEndPoint endpoint, bool https)
    {
        // GetAddressBytes drops an IPv6 scope id; UriBuilder adds the brackets an IPv6 host needs.
        IPAddress address = endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? new IPAddress(endpoint.Address.GetAddressBytes()) : endpoint.Address;
        return new UriBuilder(https ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, address.ToString(), endpoint.Port).Uri.GetLeftPart(UriPartial.Authority);
    }

    private static IPAddress Normalise(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
