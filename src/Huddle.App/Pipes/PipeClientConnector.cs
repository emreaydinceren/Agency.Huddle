using System.IO.Pipes;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Connects an in-process Agent to the app's own <see cref="PipeServer"/> as an external client
/// would, retrying while the server is still starting. <c>PersonaRunner</c> and
/// <c>DemoAgentHost</c> share it so the demo path and the production path connect the same way;
/// each decides for itself what running out of attempts means.
/// </summary>
internal static class PipeClientConnector
{
    /// <summary>How many times a connection is attempted in all.</summary>
    public const int Attempts = 30;

    /// <summary>How long one attempt waits for the server, in milliseconds.</summary>
    private const int AttemptTimeoutMilliseconds = 100;

    /// <summary>The pause between attempts.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Connects to <paramref name="pipeName"/> on this machine, up to <see cref="Attempts"/> times. A
    /// timeout or an <see cref="IOException"/> (the server has not created its next instance yet)
    /// disposes that attempt's stream and retries after <see cref="RetryDelay"/>.
    /// </summary>
    /// <param name="pipeName">The pipe the app's server listens on.</param>
    /// <param name="ct">Cancels the attempts and the pauses.</param>
    /// <returns>The connected stream, owned by the caller; or <see langword="null"/> when every attempt failed.</returns>
    public static async Task<NamedPipeClientStream?> TryConnectAsync(string pipeName, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(AttemptTimeoutMilliseconds, ct).ConfigureAwait(false);
                return client;
            }
            catch (TimeoutException)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            if (attempt < Attempts)
            {
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
        }

        return null;
    }
}
