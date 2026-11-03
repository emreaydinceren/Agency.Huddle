namespace Agency.Huddle.App.SecondBrain;

/// <summary>Finds the files and folders the Second Brain layout retired (Spec §6.8).</summary>
internal static class RetiredFileScan
{
    private const string Instructions = "Instructions.md";

    /// <summary>
    /// Lists retired items under <paramref name="dataDir"/>: <c>Process.md</c> and <c>Instructions.md</c>
    /// anywhere under <c>Teams/</c>, <c>Instructions.md</c> under <c>Agents/*/work/</c>, a top-level
    /// <c>Uploads/</c> folder and <c>prompts.json</c>.
    /// </summary>
    /// <param name="dataDir">The data folder.</param>
    /// <returns>Relative paths with <c>/</c> separators, in ordinal order.</returns>
    internal static IReadOnlyList<string> Find(string dataDir)
    {
        List<string> found = [];
        string teams = Path.Combine(dataDir, "Teams");
        if (Directory.Exists(teams))
        {
            foreach (string file in Directory.EnumerateFiles(teams, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, "Process.md", StringComparison.Ordinal) || string.Equals(name, Instructions, StringComparison.Ordinal))
                {
                    found.Add(Relative(dataDir, file));
                }
            }
        }

        string agents = Path.Combine(dataDir, "Agents");
        if (Directory.Exists(agents))
        {
            foreach (string agent in Directory.EnumerateDirectories(agents))
            {
                string file = Path.Combine(agent, "work", Instructions);
                if (File.Exists(file))
                {
                    found.Add(Relative(dataDir, file));
                }
            }
        }

        if (Directory.Exists(Path.Combine(dataDir, "Uploads")))
        {
            found.Add("Uploads");
        }

        if (File.Exists(Path.Combine(dataDir, "prompts.json")))
        {
            found.Add("prompts.json");
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static string Relative(string dataDir, string file) => Path.GetRelativePath(dataDir, file).Replace('\\', '/');
}
