using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Hosting.Server;

namespace Agency.Huddle.App.Cli;

/// <summary>
/// Prints the development ready line once the server is listening (Spec 6.9, C-14). A server that reports no addresses (the in-memory
/// TestServer) stays quiet; so does desktop mode, whose launcher opens the browser itself.
/// </summary>
/// <param name="server">The web server whose addresses are read once it is listening.</param>
/// <param name="lifetime">Raises <see cref="IHostApplicationLifetime.ApplicationStarted"/>.</param>
/// <param name="links">Mints the bootstrap token.</param>
/// <param name="output">The writer the line goes to.</param>
/// <param name="configuration">Tells whether desktop mode is on.</param>
/// <param name="gateway">The recorded listeners; its app URL is announced and the managed endpoint skipped (D-5).</param>
internal sealed class DevConsoleAnnouncer(
    IServer server,
    IHostApplicationLifetime lifetime,
    BootstrapLinks links,
    DevConsoleOutput output,
    IConfiguration configuration,
    GatewayListener? gateway = null) : IHostedService
{
    private CancellationTokenRegistration startedRegistration;

    /// <summary>Registers the announcement on <see cref="IHostApplicationLifetime.ApplicationStarted"/>.</summary>
    /// <param name="cancellationToken">Not used.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        bool desktop = configuration.GetSection("Team:Desktop").GetValue<bool>("Enabled");
        if (!desktop)
        {
            this.startedRegistration = lifetime.ApplicationStarted.Register(this.Announce);
        }

        return Task.CompletedTask;
    }

    /// <summary>Drops the registration.</summary>
    /// <param name="cancellationToken">Not used.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this.startedRegistration.DisposeAsync().ConfigureAwait(false);
    }

    private void Announce()
    {
        int? managedPort = gateway?.ManagedEndpoint?.Port;
        IEnumerable<string>? addresses = gateway?.AppUrl is { } appUrl
            ? [appUrl]
            : BoundServerAddresses.Of(server).Where(address => managedPort is null || !Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) || uri.Port != managedPort);
        _ = DevConsole.Announce(addresses, links, output.Writer);
    }
}
