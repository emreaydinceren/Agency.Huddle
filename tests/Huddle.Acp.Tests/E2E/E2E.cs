namespace Agency.Huddle.Acp.Tests.E2E;

internal static class E2E
{
    public static bool Enabled => Environment.GetEnvironmentVariable("TEAM_E2E") == "1";

    internal static string RepoRoot
    {
        get
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "Huddle.slnx");
                if (File.Exists(candidate))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate the repository root (a directory containing 'Huddle.slnx') by walking up from '{AppContext.BaseDirectory}'.");
        }
    }

    internal static string AdapterEntry => Path.Combine(
        RepoRoot,
        "tools",
        "acp",
        "node_modules",
        "@agentclientprotocol",
        "claude-agent-acp",
        "dist",
        "index.js");
}
