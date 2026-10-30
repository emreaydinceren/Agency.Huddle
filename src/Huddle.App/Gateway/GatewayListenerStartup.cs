using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// After the server has started (D-5): reads the bound ports into <see cref="GatewayListener"/>, remembers the desktop port (D-4), logs why the app is
/// loopback-only, and feeds the dynamic host list (D-7). A server without Kestrel listen options (the in-memory <c>TestServer</c>) records nothing but
/// the first reported address.
/// </summary>
/// <param name="server">The web server, whose address feature is the fallback when the plan added no endpoint.</param>
/// <param name="listener">The recorded listeners.</param>
/// <param name="settings">The gateway settings.</param>
/// <param name="configuration">Tells whether desktop mode is on.</param>
/// <param name="team">Where <c>team.db</c> lives.</param>
/// <param name="hosts">The dynamic host list.</param>
/// <param name="logger">The logger.</param>
internal sealed class GatewayListenerStartup(
    IServer server,
    GatewayListener listener,
    GatewaySettings settings,
    IConfiguration configuration,
    IOptions<TeamOptions> team,
    GatewayDynamicHosts hosts,
    ILogger<GatewayListenerStartup> logger) : IHostedLifecycleService
{
    private NetworkAddressChangedEventHandler? addressChanged;

    /// <summary>Whether desktop mode is switched on (<c>Team:Desktop:Enabled</c>).</summary>
    /// <param name="configuration">The configuration.</param>
    internal static bool IsDesktop(IConfiguration configuration) => configuration.GetSection("Team:Desktop").GetValue<bool>("Enabled");

    /// <summary>Whether Tailscale Serve or Funnel needs the managed loopback listener.</summary>
    /// <param name="options">The gateway options.</param>
    internal static bool UsesManagedListener(GatewayOptions options) =>
        string.Equals(options.Tailscale.Mode, "serve", StringComparison.OrdinalIgnoreCase)
        || string.Equals(options.Tailscale.Mode, "funnel", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        listener.ReadBoundEndpoints();
        if (listener.AppUrl is null)
        {
            string? first = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault(static address => !string.IsNullOrWhiteSpace(address));
            if (first is not null)
            {
                listener.RecordApp(first);
            }
        }

        if (IsDesktop(configuration) && listener.AppEndpoints.Count > 0)
        {
            DesktopPortChoice.Remember(new GatewaySettingsStore(team.Value.DataDir), listener.AppEndpoints[0].Port);
        }

        if (listener.Reason is not null)
        {
            logger.LogWarning("Remote access is limited: {Reason}", listener.Reason);
        }

        this.RefreshHosts();
        if (!string.Equals(settings.Options.Bind, "loopback", StringComparison.OrdinalIgnoreCase))
        {
            this.addressChanged = (_, _) => this.RefreshHosts();
            NetworkChange.NetworkAddressChanged += this.addressChanged;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        if (this.addressChanged is not null)
        {
            NetworkChange.NetworkAddressChanged -= this.addressChanged;
            this.addressChanged = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Recomputes the automatic hosts for a non-loopback bind; a loopback-only app adds none.</summary>
    private void RefreshHosts()
    {
        bool anyAddress = listener.AppEndpoints.Any(static endpoint => endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any));
        List<IPAddress> bound = anyAddress ? [.. NonLoopbackAddresses()] : [];
        bool remote = anyAddress || listener.AppEndpoints.Any(static endpoint => !IPAddress.IsLoopback(endpoint.Address));
        hosts.Set(remote ? GatewayDynamicHosts.Automatic(settings.Options, Environment.MachineName, bound) : []);
    }

    private static IEnumerable<IPAddress> NonLoopbackAddresses()
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (!IPAddress.IsLoopback(unicast.Address) && unicast.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                {
                    yield return unicast.Address;
                }
            }
        }
    }
}
