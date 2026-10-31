namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Every call to the <c>tailscale</c> command line goes through here (Spec 6.3; D-11), so tests use canned output.
/// A missing executable surfaces as <see cref="System.ComponentModel.Win32Exception"/> from both methods.
/// </summary>
internal interface ITailscaleCli
{
    /// <summary>Runs <c>tailscale</c> to completion.</summary>
    /// <param name="args">The arguments, without the executable.</param>
    /// <param name="timeout">How long to wait; <see cref="TimeoutException"/> when it passes.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The exit code and the captured output.</returns>
    Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Starts <c>tailscale</c> as a child that keeps running (a foreground Serve or Funnel route).</summary>
    /// <param name="args">The arguments, without the executable.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The child.</returns>
    Task<ITailscaleChild> StartAsync(IReadOnlyList<string> args, CancellationToken cancellationToken);
}
