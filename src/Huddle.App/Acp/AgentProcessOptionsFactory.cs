using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Builds the <see cref="AgentProcessOptions"/> used to launch the ACP adapter, or returns
/// <see langword="null"/> when nothing tells us where the adapter lives. Returning null rather
/// than throwing is the point: a developer who has not run <c>tools/acp/install.ps1</c> must
/// still be able to <c>dotnet run</c> the chat app.
/// </summary>
internal static class AgentProcessOptionsFactory
{
    internal static AgentProcessOptions? TryCreate(AcpOptions options, string workDir, string probeStart)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Args is { Count: > 0 })
        {
            return new AgentProcessOptions(options.Command, options.Args, workDir);
        }

        if (!string.IsNullOrWhiteSpace(options.AdapterPath))
        {
            return new AgentProcessOptions(options.Command, [options.AdapterPath], workDir);
        }

        var located = AdapterLocator.Locate(probeStart);
        if (located is not null)
        {
            return new AgentProcessOptions(options.Command, [located], workDir);
        }

        return null;
    }
}