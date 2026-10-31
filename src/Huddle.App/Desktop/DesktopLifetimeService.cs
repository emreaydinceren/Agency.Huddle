using System.ComponentModel;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// Publishes the running instance once the server is listening: writes <c>instance.json</c> with the bound URL,
/// the stop token and the process id, then opens the browser when <see cref="DesktopOptions.OpenBrowser"/> is on.
/// The record is deleted again on shutdown.
/// </summary>
/// <param name="server">The web server, whose addresses are read after Kestrel has bound its ports.</param>
/// <param name="lifetime">Raises <see cref="IHostApplicationLifetime.ApplicationStarted"/> once the server is listening.</param>
/// <param name="options">The desktop host settings.</param>
/// <param name="team">The team settings; <see cref="TeamOptions.DataDir"/> is where the record lives.</param>
/// <param name="session">The per-instance session holding the stop token.</param>
/// <param name="opener">Opens the instance URL in the browser.</param>
/// <param name="logger">The logger.</param>
/// <param name="bootstrap">Mints the bootstrap token the opened URL carries (Spec 6.9, C-9); <see langword="null"/> opens the bare address.</param>
/// <param name="gateway">The recorded listeners; its app URL wins over the server's address list and the managed endpoint is skipped (D-5).</param>
internal sealed class DesktopLifetimeService(
    IServer server,
    IHostApplicationLifetime lifetime,
    IOptions<DesktopOptions> options,
    IOptions<TeamOptions> team,
    DesktopSession session,
    IBrowserOpener opener,
    ILogger<DesktopLifetimeService> logger,
    BootstrapLinks? bootstrap = null,
    GatewayListener? gateway = null) : IHostedService
{
    private readonly BootstrapLinks? bootstrap = bootstrap;

    private CancellationTokenRegistration startedRegistration;

    /// <summary>
    /// Registers the publish callback on <see cref="IHostApplicationLifetime.ApplicationStarted"/> and returns at
    /// once, so startup is never blocked.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.startedRegistration = lifetime.ApplicationStarted.Register(this.Publish);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the instance record.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this.startedRegistration.DisposeAsync().ConfigureAwait(false);
        InstanceFile.Delete(team.Value.DataDir);
    }

    /// <summary>
    /// Writes the instance record for the first bound address and opens the browser on it. Does nothing, apart from
    /// a warning, when the server reports no address.
    /// </summary>
    private void Publish()
    {
        string? url = gateway?.AppUrl ?? this.FirstUnmanagedAddress();

        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("The server reported no listening address; the desktop instance record was not written and the browser was not opened.");
            return;
        }

        string address = this.bootstrap is null ? url : BootstrapLinks.NormalizeListenAddress(url);
        InstanceFile.Write(team.Value.DataDir, new InstanceInfo(address, session.StopToken, Environment.ProcessId));

        if (options.Value.OpenBrowser)
        {
            this.TryOpen(this.bootstrap?.AdminUrl(address) ?? address, address);
        }
    }

    /// <summary>The first address the server reports that is not the managed (Tailscale) loopback endpoint (D-5).</summary>
    private string? FirstUnmanagedAddress()
    {
        int? managedPort = gateway?.ManagedEndpoint?.Port;
        return BoundServerAddresses.Of(server).FirstOrDefault(
            address => managedPort is null || !Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) || uri.Port != managedPort);
    }

    /// <summary>
    /// Opens <paramref name="url"/>, logging instead of throwing when the opener rejects it or cannot launch a
    /// browser, so an unopenable address never fails startup.
    /// </summary>
    /// <remarks>
    /// <paramref name="url"/> carries the bootstrap token in its fragment (R-3), and an opener's exception message may
    /// repeat it, so only the bare <paramref name="address"/> and the exception type are logged, never the exception itself.
    /// </remarks>
    /// <param name="url">The URL to open, possibly with the bootstrap fragment.</param>
    /// <param name="address">The bare instance address, safe to log.</param>
    private void TryOpen(string url, string address)
    {
        try
        {
            opener.Open(url);
        }
        catch (Exception ex) when (ex is ArgumentException or UriFormatException or Win32Exception)
        {
            logger.LogWarning("The browser was not opened for {Url} ({Reason}).", address, ex.GetType().Name);
        }
    }
}
