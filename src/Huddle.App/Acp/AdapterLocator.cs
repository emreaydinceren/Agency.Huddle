namespace Agency.Huddle.App.Acp;

/// <summary>
/// Locates the ACP Claude Code adapter's entry point by walking up from a starting directory.
/// Keying on the adapter file itself, rather than on a solution file, is deliberate: it gives
/// <see cref="Agency.Huddle.App"/> no dependency on <c>Huddle.slnx</c>.
/// </summary>
internal static class AdapterLocator
{
    private const string RelativeAdapterPath =
        "tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js";

    internal static string? Locate(string startDirectory)
    {
        var relative = RelativeAdapterPath.Replace('/', Path.DirectorySeparatorChar);
        DirectoryInfo? directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}