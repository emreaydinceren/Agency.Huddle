using System.Globalization;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Reads the Human's three levels of standing Instructions (Instructions spec §3.1) afresh at each session
/// start: <c>Teams/Instructions.md</c> for every Agent, <c>Teams/&lt;Team&gt;/Instructions.md</c> for each Team
/// the Agent is in, and <c>Agents/&lt;Name&gt;/work/Instructions.md</c> for the Agent alone.
/// </summary>
/// <remarks>
/// The same read-and-tolerate pattern as <c>RoleBlockBuilder.ReadProcess</c>: an unreadable file is an empty
/// level and a Warning, never a failed session. The file name is matched ignoring case. Frontmatter is kept.
/// </remarks>
/// <param name="prompts">Renders the cut line a too-long file ends with.</param>
/// <param name="maxChars">The most characters one file contributes; zero or less means no cap.</param>
/// <param name="logger">Records an unreadable file.</param>
internal sealed class InstructionsReader(IPromptSource prompts, int maxChars, ILogger<InstructionsReader> logger)
{
    /// <summary>The source label of the Huddle-wide level.</summary>
    internal const string HuddleSource = "Huddle";

    /// <summary>The source label of the Agent's own level.</summary>
    internal const string YouSource = "You";

    /// <summary>Reads every level that has text.</summary>
    /// <param name="teamsRoot">The Teams root folder, holding the Huddle-wide file and each Team folder.</param>
    /// <param name="teamNames">The folder names of the Agent's Teams, in the order its <c>Teams:</c> line gives them.</param>
    /// <param name="workDir">The Agent's Work Dir, holding its own file.</param>
    /// <returns>The non-empty levels from general to specific; empty when there are none.</returns>
    public InstructionsSnapshot Read(string teamsRoot, IReadOnlyList<string> teamNames, string workDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamsRoot);
        ArgumentNullException.ThrowIfNull(teamNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);

        List<InstructionsSection> sections = [];
        this.AddSection(sections, HuddleSource, teamsRoot);

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string team in teamNames)
        {
            if (LibraryNames.Validate(team) is not null || !seen.Add(team))
            {
                continue;
            }

            this.AddSection(sections, $"Team {team}", Path.Combine(teamsRoot, team));
        }

        this.AddSection(sections, YouSource, workDir);
        return new InstructionsSnapshot(sections);
    }

    /// <summary>Adds the level's section when its file holds any text.</summary>
    /// <param name="sections">The sections so far.</param>
    /// <param name="source">The level's source label.</param>
    /// <param name="folder">The folder that holds the level's file.</param>
    private void AddSection(List<InstructionsSection> sections, string source, string folder)
    {
        string? path = FindFile(folder);
        if (path is null)
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
            logger.LogWarning(ex, "Could not read the Instructions file '{Path}'; its level is left out of the system prompt.", path);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        sections.Add(new InstructionsSection(source, this.Cut(text, path)));
    }

    /// <summary>Finds <c>Instructions.md</c> directly inside <paramref name="folder"/>, comparing the name ignoring case.</summary>
    /// <param name="folder">The folder to look in.</param>
    /// <returns>The file's path, or <see langword="null"/> when there is none or the folder cannot be listed.</returns>
    private static string? FindFile(string folder)
    {
        try
        {
            foreach (string candidate in Directory.EnumerateFiles(folder))
            {
                if (string.Equals(Path.GetFileName(candidate), TeamPaths.InstructionsFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing or locked folder holds no readable Instructions; its level is simply absent.
            return null;
        }

        return null;
    }

    /// <summary>Cuts <paramref name="text"/> at the cap and appends the cut line naming the file.</summary>
    /// <param name="text">The file's whole text.</param>
    /// <param name="path">The file's path, named in the cut line.</param>
    private string Cut(string text, string path)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
        {
            return text;
        }

        string cutLine = prompts.Render(
            "systemPrompt.instructionsCut",
            new Dictionary<string, string>
            {
                ["{{max}}"] = maxChars.ToString(CultureInfo.InvariantCulture),
                ["{{path}}"] = path,
            });

        return text[..maxChars] + "\n" + cutLine;
    }
}
