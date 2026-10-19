using System.ComponentModel;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
internal sealed class DesktopLifetimeService(
    IServer server,
    IHostApplicationLifetime lifetime,
    IOptions<DesktopOptions> options,
    IOptions<TeamOptions> team,
    DesktopSession session,
    IBrowserOpener opener,
    ILogger<DesktopLifetimeService> logger) : IHostedService
{
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
        string? url = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("The server reported no listening address; the desktop instance record was not written and the browser was not opened.");
            return;
        }

        InstanceFile.Write(team.Value.DataDir, new InstanceInfo(url, session.StopToken, Environment.ProcessId));

        if (options.Value.OpenBrowser)
        {
            this.TryOpen(url);
        }
    }

    /// <summary>
    /// Opens <paramref name="url"/>, logging instead of throwing when the opener rejects it or cannot launch a
    /// browser, so an unopenable address never fails startup.
    /// </summary>
    /// <param name="url">The instance URL.</param>
    private void TryOpen(string url)
    {
        try
        {
            opener.Open(url);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "The browser was not opened for {Url}.", url);
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(ex, "The browser was not opened for {Url}.", url);
        }
        catch (Win32Exception ex)
        {
            logger.LogWarning(ex, "The browser was not opened for {Url}.", url);
        }
    }
}
