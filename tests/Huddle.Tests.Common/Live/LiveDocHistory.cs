namespace Agency.Huddle.Tests.Live;

/// <summary>
/// The switch and the paths for the paid DocHistory lane (DocHistory spec §10, <i>Live tests</i>). The lane has a variable
/// of its own, <c>TEAM_E2E_DOCHISTORY=1</c>, so setting <c>TEAM_E2E</c> does not start it and the reverse holds too.
/// </summary>
internal static class LiveDocHistory
{
    /// <summary>Whether the paid DocHistory tests may run: <c>TEAM_E2E_DOCHISTORY</c> is <c>1</c>.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("TEAM_E2E_DOCHISTORY") == "1";

    /// <summary>The repository root: the nearest folder above the test binaries holding <c>Huddle.slnx</c>.</summary>
    public static string RepoRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException($"Could not find Huddle.slnx above '{AppContext.BaseDirectory}'.");
        }
    }

    /// <summary>The real Claude Adapter's entry script under <c>tools/acp</c>.</summary>
    public static string AdapterEntry => Path.Combine(RepoRoot, "tools", "acp", "node_modules", "@agentclientprotocol", "claude-agent-acp", "dist", "index.js");
}
