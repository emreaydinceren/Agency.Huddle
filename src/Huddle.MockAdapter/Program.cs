using Agency.Huddle.Acp.Tests.Fakes;

namespace Agency.Huddle.MockAdapter;

/// <summary>Entry point for <c>mock-acp</c>, the scripted ACP agent this solution builds and owns.</summary>
internal static class Program
{
    /// <summary>
    /// Starts the mock adapter as a process: an ACP agent that speaks newline-delimited JSON-RPC
    /// 2.0 over stdin/stdout, per Spec §6.10 ("Two modes", process mode). Cancellation is
    /// requested on Ctrl+C and on process exit, so the read loop does not outlive its host.
    /// </summary>
    /// <param name="args">
    /// Process arguments. Recognizes an optional <c>--script &lt;path&gt;</c> pair, reserved for
    /// the V2 script format (Spec §6.10, "V1 / V2"): the surface is parsed here so it exists, but
    /// is deliberately inert in V1 — the mock always runs
    /// <see cref="MockBehaviour.ChunkedEchoAsync"/>.
    /// </param>
    /// <returns>0 on a clean shutdown or a requested cancellation; non-zero on an unhandled fault.</returns>
    internal static async Task<int> Main(string[] args)
    {
        // The parsed value is unused in V1; this call only reserves the --script argument
        // surface for V2 (Spec §6.10, "V1 / V2") so a future implementation does not have to
        // change the CLI shape.
        _ = ParseScriptPath(args);

        using CancellationTokenSource cancellationSource = new();
        ConsoleCancelEventHandler onCancelKeyPress = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            CancelSafely(cancellationSource);
        };
        EventHandler onProcessExit = (_, _) => CancelSafely(cancellationSource);

        Console.CancelKeyPress += onCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += onProcessExit;

        try
        {
            Stream input = Console.OpenStandardInput();
            Stream output = Console.OpenStandardOutput(); // stdout is protocol-only — Spec §12 (E-16): every diagnostic below goes to Console.Error instead.
            DuplexStream duplex = new(input, output);
            await using FakeAcpAgent agent = new(duplex);
            agent.OnPrompt = MockBehaviour.ChunkedEchoAsync;

            await agent.RunAsync(cancellationSource.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
#pragma warning disable CA1031 // Top-level entry point: any unhandled fault must become a non-zero exit code instead of crashing the process silently; the fault is logged to stderr immediately below, which is the only reason this catch is safe.
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"mock-acp faulted: {ex}").ConfigureAwait(false);
            return 1;
        }
#pragma warning restore CA1031
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit -= onProcessExit;
        }
    }

    /// <summary>
    /// Parses an optional <c>--script &lt;path&gt;</c> argument pair. Reserved for the V2 script
    /// format (Spec §6.10, "V1 / V2"), which will replay a JSON script instead of the scripted
    /// <see cref="MockBehaviour"/> default; deliberately unused by V1.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>The path that followed <c>--script</c>, or <see langword="null"/> if absent.</returns>
    private static string? ParseScriptPath(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--script", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Cancels <paramref name="cancellationSource"/>, tolerating a source that is already disposed by a concurrent shutdown path.</summary>
    private static void CancelSafely(CancellationTokenSource cancellationSource)
    {
        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Console.CancelKeyPress and ProcessExit can both fire during teardown; the source
            // may already be disposed by the time the second one runs. Nothing left to cancel.
        }
    }
}
