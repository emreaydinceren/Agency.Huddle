namespace Agency.Huddle.Seeder;

/// <summary>
/// The <c>huddle-seed</c> entry point. It is an explicit, namespaced class rather than top-level statements
/// so its generated <c>Program</c> type cannot clash with <c>Huddle.App</c>'s in a project that references both.
/// </summary>
internal static class SeederProgram
{
    /// <summary>Runs the seeder with the process arguments; Ctrl+C cancels the run.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        return await SeedCli.RunAsync(args, Console.Out, Console.Error, TimeProvider.System, cts.Token);
    }
}
