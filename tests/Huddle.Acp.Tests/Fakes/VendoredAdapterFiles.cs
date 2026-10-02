namespace Agency.Huddle.Acp.Tests.Fakes;

/// <summary>
/// Locates the vendored <c>claude-agent-acp</c> adapter that <c>tools/acp/install.ps1</c> installs, for
/// tests that pin the fake against its source. The adapter is not part of the repository, so a clean
/// checkout has none and those tests skip rather than fail. A git worktree has no <c>node_modules</c>
/// of its own, so the search walks up through every parent directory, which reaches the main checkout.
/// </summary>
internal static class VendoredAdapterFiles
{
    private static readonly string[] PackageDirectory =
    [
        "tools",
        "acp",
        "node_modules",
        "@agentclientprotocol",
    ];

    /// <summary>Gets a value indicating whether the adapter's <c>elicitation.js</c> was found.</summary>
    public static bool Present => VendoredAdapterFiles.ElicitationJs is not null;

    /// <summary>Gets the path of the adapter's <c>dist/elicitation.js</c>, or null when it is not installed.</summary>
    internal static string? ElicitationJs => VendoredAdapterFiles.Find("claude-agent-acp", "dist", "elicitation.js");

    /// <summary>Gets the path of the adapter's <c>dist/acp-agent.js</c>, or null when it is not installed.</summary>
    internal static string? AcpAgentJs => VendoredAdapterFiles.Find("claude-agent-acp", "dist", "acp-agent.js");

    /// <summary>Gets the path of the ACP SDK's <c>dist/schema/index.js</c>, or null when it is not installed.</summary>
    internal static string? SdkSchemaJs => VendoredAdapterFiles.Find("sdk", "dist", "schema", "index.js");

    private static string? Find(params string[] relativeToPackageScope)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. VendoredAdapterFiles.PackageDirectory, .. relativeToPackageScope]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
