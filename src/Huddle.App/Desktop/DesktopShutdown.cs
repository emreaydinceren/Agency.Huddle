namespace Agency.Huddle.App.Desktop;

/// <summary>The seam through which the stop endpoint asks the running host to shut down.</summary>
internal interface IDesktopShutdown
{
    /// <summary>Requests a graceful shutdown of the host.</summary>
    void Stop();
}

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
