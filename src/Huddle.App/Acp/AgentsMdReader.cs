using System.Globalization;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Reads the Human's three levels of <c>AGENTS.md</c> (Spec §6.2) afresh at each session start: Huddle-wide,
/// then each Team in the order given, then that Team's Projects sorted ordinally.
/// </summary>
/// <remarks>The file system decides case (D-8). An unreadable file is an absent level and a Warning, never a failed session.</remarks>
/// <param name="prompts">Renders the cut line a too-long file ends with.</param>
/// <param name="options">Holds the per-file cap.</param>
/// <param name="logger">Records an unreadable file.</param>
internal sealed class AgentsMdReader(IPromptSource prompts, AgentsMdOptions options, ILogger<AgentsMdReader> logger)
{
    /// <summary>Reads every level that has text.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="teamNames">The folder names of the Agent's Teams, in the order given.</param>
    /// <returns>The non-empty levels in composition order; empty when there are none.</returns>
    public AgentsMdSnapshot Read(string teamsRoot, IReadOnlyList<string> teamNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamsRoot);
        ArgumentNullException.ThrowIfNull(teamNames);

        List<AgentsMdSection> sections = [];
        List<string> omitted = [];
        foreach (AgentsMdFile file in AgentsMdLocator.Enumerate(teamsRoot, teamNames))
        {
            this.AddSection(sections, omitted, file);
        }

        this.ApplyBudget(sections, omitted);
        return new AgentsMdSnapshot(sections, omitted);
    }

    /// <summary>Drops Project sections from the end while the total is over the budget, then cuts the last section to fit (Spec §8.2).</summary>
    /// <param name="sections">The sections, edited in place.</param>
    /// <param name="omitted">Receives the path of each dropped section.</param>
    private void ApplyBudget(List<AgentsMdSection> sections, List<string> omitted)
    {
        int maxTotal = options.MaxTotalChars;
        if (maxTotal <= 0)
        {
            return;
        }

        int total = sections.Sum(static section => section.Text.Length);
        while (total > maxTotal)
        {
            int index = sections.FindLastIndex(static section => section.Level == AgentsMdLevel.Project);
            if (index < 0)
            {
                break;
            }

            total -= sections[index].Text.Length;
            omitted.Add(sections[index].Path);
            sections.RemoveAt(index);
        }

        if (total > maxTotal && sections.Count > 0)
        {
            AgentsMdSection last = sections[^1];
            int allowed = Math.Max(maxTotal - (total - last.Text.Length), 1);
            sections[^1] = last with { Text = this.Cut(last.Text, last.Path, allowed) };
        }
    }

    /// <summary>Adds the level's section when its file holds any text.</summary>
    /// <param name="sections">The sections so far.</param>
    /// <param name="omitted">The files left out with a reason.</param>
    /// <param name="file">The level's file, as the locator found it.</param>
    private void AddSection(List<AgentsMdSection> sections, List<string> omitted, AgentsMdFile file)
    {
        string path = file.FullPath;
        if (!File.Exists(path))
        {
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read the AGENTS.md file '{Path}'; its level is left out of the system prompt.", path);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        (string body, bool had) = AgentsMdFrontmatter.Strip(text);
        if (had)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Stripped the settings block from '{Path}'; settings belong in _team.md.", path);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                omitted.Add(path + " (settings block only)");
                return;
            }
        }

        sections.Add(new AgentsMdSection(file.Level, file.Team, file.Project, path, this.Cut(body, path, options.MaxCharsPerFile), had));
    }

    /// <summary>Cuts <paramref name="text"/> at the cap and appends the cut line naming the file.</summary>
    /// <param name="text">The file's whole text.</param>
    /// <param name="path">The file's path, named in the cut line.</param>
    /// <param name="maxChars">The most characters to keep; zero or less keeps the whole text.</param>
    private string Cut(string text, string path, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
        {
            return text;
        }

        string cutLine = prompts.Render(
            "systemPrompt.agentsMdCut",
            new Dictionary<string, string>
            {
                ["{{max}}"] = maxChars.ToString(CultureInfo.InvariantCulture),
                ["{{path}}"] = path,
            });

        return text[..KeptLength(text, maxChars)] + "\n" + cutLine;
    }

    /// <summary>The number of characters to keep: the cap, less one when the cut would split a surrogate pair or a CR LF line break.</summary>
    /// <param name="text">The file's whole text, longer than the cap.</param>
    /// <param name="maxChars">The cap.</param>
    private static int KeptLength(string text, int maxChars)
    {
        bool splitsPair = char.IsHighSurrogate(text[maxChars - 1]) && char.IsLowSurrogate(text[maxChars]);
        bool splitsLineBreak = text[maxChars - 1] == '\r' && text[maxChars] == '\n';
        return splitsPair || splitsLineBreak ? maxChars - 1 : maxChars;
    }
}
