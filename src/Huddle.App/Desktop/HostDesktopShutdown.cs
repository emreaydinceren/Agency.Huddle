namespace Agency.Huddle.App.Desktop;

/// <summary>Stops the host by calling <see cref="IHostApplicationLifetime.StopApplication"/>.</summary>
/// <param name="lifetime">The host's application lifetime.</param>
internal sealed class HostDesktopShutdown(IHostApplicationLifetime lifetime) : IDesktopShutdown
{
    /// <inheritdoc />
    public void Stop()
    {
        lifetime.StopApplication();
    }
}
