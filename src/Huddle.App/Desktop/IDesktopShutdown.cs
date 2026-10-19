namespace Agency.Huddle.App.Desktop;

/// <summary>The seam through which the stop endpoint asks the running host to shut down.</summary>
internal interface IDesktopShutdown
{
    /// <summary>Requests a graceful shutdown of the host.</summary>
    void Stop();
}
