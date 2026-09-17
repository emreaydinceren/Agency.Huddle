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
    /// <summary>
    /// Resolves the launch command for <paramref name="profile"/>, trying explicit
    /// <see cref="AdapterProfile.Args"/>, then <see cref="AdapterProfile.AdapterPath"/>, then (for
    /// a profile that takes the <c>mcp__</c> prefix) <see cref="AdapterLocator"/>, in that order.
    /// </summary>
    /// <param name="profile">The Adapter profile to launch.</param>
    /// <param name="workDir">The working directory the launched process runs in.</param>
    /// <param name="probeStart">The directory <see cref="AdapterLocator"/> walks up from.</param>
    /// <returns>The resolved options, or <see langword="null"/> when nothing tells us where the adapter lives.</returns>
    internal static AgentProcessOptions? TryCreate(AdapterProfile profile, string workDir, string probeStart)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Args is { Count: > 0 })
        {
            return new AgentProcessOptions(profile.Command, profile.Args, workDir);
        }

        if (!string.IsNullOrWhiteSpace(profile.AdapterPath))
        {
            return new AgentProcessOptions(profile.Command, [profile.AdapterPath], workDir);
        }

        // AdapterLocator is consulted only when profile.UsesToolNamePrefix is true. The coupling
        // looks odd and is deliberate: a profile that does not take the mcp__ prefix is not the
        // Node adapter, and the locator knows only how to find the Node adapter (Spec §6.3,
        // Implementation notes). Without this gate, an `agency` profile configured as a bare
        // Command with no Args and no AdapterPath would fall through to AdapterLocator and launch
        // the Node adapter under the wrong profile's name (Spec §12, E-7).
        if (profile.UsesToolNamePrefix)
        {
            var located = AdapterLocator.Locate(probeStart);
            if (located is not null)
            {
                return new AgentProcessOptions(profile.Command, [located], workDir);
            }
        }

        return null;
    }
}