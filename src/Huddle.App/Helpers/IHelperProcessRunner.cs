using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Runs one prompt in one throwaway adapter session and returns the final answer text: the one place a real process is
/// spawned for a Helper, and the seam tests replace (the same rule as <c>IAdapterProbeRunner</c>).
/// </summary>
internal interface IHelperProcessRunner
{
    /// <summary>Spawns, opens a session, sends the prompt, reads the Turn's text, and disposes. Throws on any failure or on cancellation.</summary>
    /// <param name="processOptions">How to launch the adapter.</param>
    /// <param name="request">The session and the prompt.</param>
    /// <param name="cancellationToken">Cancels the run; disposing the host ends the process.</param>
    Task<HelperTurn> RunAsync(AgentProcessOptions processOptions, HelperSessionRequest request, CancellationToken cancellationToken);
}
