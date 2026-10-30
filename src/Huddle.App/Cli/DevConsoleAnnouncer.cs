using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

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
internal sealed class DevConsoleAnnouncer(
    IServer server,
    IHostApplicationLifetime lifetime,
    BootstrapLinks links,
    DevConsoleOutput output,
    IConfiguration configuration) : IHostedService
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
        IServerAddressesFeature? feature = server.Features.Get<IServerAddressesFeature>();
        _ = DevConsole.Announce(feature?.Addresses, links, output.Writer);
    }
}
