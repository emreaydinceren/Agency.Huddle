using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Refuses an Agent's tool call that would change the <c>needsYouLevel</c> or <c>spendCap</c> line of a <c>_team.md</c> or <c>_project.md</c>
/// under the Teams root (ADR-0088). The level and its cap are the Human's standing decision for a whole scope, so only the Human sets them.
/// </summary>
/// <remarks>
/// A read is never refused, and neither is an edit of any other line of those files. An edit is refused when its old or new text mentions either key,
/// or when applying it to the file on disk would change the level or the cap (so replacing only the value is caught too). A whole-file write is refused
/// when the level or cap it carries differs from the file on disk, a missing file counting as unset. Deleting or moving such a file is refused while it
/// carries either key. Like <see cref="AgentsMdGuard"/> it sees path and content arguments only, so a shell command gets past it: that boundary is soft and
/// documented. Paths are compared ignoring case.
/// </remarks>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="workDir">The Agent's own Work Dir, where a relative path resolves.</param>
/// <param name="prompts">Renders the refusal reason (<c>systemPrompt.needsYouLevelRefusal</c>).</param>
internal sealed class NeedsYouLevelGuard(string teamsRoot, string workDir, IPromptSource prompts) : IToolCallGuard
{
    private static readonly string[] WholeContentNames = ["content", "file_text", "contents", "new_content", "text"];

    private static readonly string[] OldTextNames = ["old_string", "old_str", "oldString", "oldText", "old_text"];

    private static readonly string[] NewTextNames = ["new_string", "new_str", "newString", "newText", "new_text"];

    private static readonly string[] PathNames = ["file_path", "path", "notebook_path", "old_path", "new_path"];

    /// <inheritdoc />
    string? IToolCallGuard.RefusalFor(ToolCallInfo toolCall, string personaName) => this.RefusalFor(toolCall);

    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when the call does not change a Needs You level or cap.</returns>
    public string? RefusalFor(ToolCallInfo toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!ToolCallPaths.ChangesFiles(toolCall.Kind))
        {
            return null;
        }

        string root = Path.GetFullPath(teamsRoot.Replace('\\', '/'));
        List<string> paths = [.. ToolCallPaths.From(toolCall, workDir).Select(static resolved => Path.GetFullPath(resolved.Replace('\\', '/')))];
        List<string> guarded = [.. paths.Where(path => IsGuardedFile(path, root))];
        if (guarded.Count == 0)
        {
            return null;
        }

        bool changes = toolCall.Kind is ToolKind.Delete or ToolKind.Move
            ? CarriesLevel(paths)
            : guarded.Any(path => ChangesLevelOrCap(toolCall.RawInputJson, path));
        return changes ? prompts.Render("systemPrompt.needsYouLevelRefusal") : null;
    }

    /// <summary>Whether the path is a Team file at depth 2 or a Project file at depth 3 below the Teams root, where the Team and the Project are not reserved names.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="root">The full Teams root.</param>
    private static bool IsGuardedFile(string path, string root)
    {
        string[] segments = PathContainment.Below(path, root, StringComparison.OrdinalIgnoreCase)
            ?.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            ?? [];
        return segments.Length switch
        {
            2 => string.Equals(segments[1], TeamPaths.TeamFileName, StringComparison.OrdinalIgnoreCase)
                && !TeamNames.IsReservedTeamName(segments[0]),
            3 => string.Equals(segments[2], TeamPaths.ProjectFileName, StringComparison.OrdinalIgnoreCase)
                && !TeamNames.IsReservedTeamName(segments[0])
                && !TeamNames.IsReservedProjectName(segments[1]),
            _ => false,
        };
    }

    /// <summary>Whether any of the files, as they are on disk, holds a level or a cap: removing it, or moving it onto a guarded place, would change what a scope has set.</summary>
    /// <param name="paths">The full paths the call names.</param>
    private static bool CarriesLevel(List<string> paths) =>
        paths.Any(path => Read(path, ReadDisk(path)) != (null, null));

    /// <summary>Whether the content arguments of an edit or write would change the level or the cap of the file at <paramref name="path"/>.</summary>
    /// <param name="rawInputJson">The call's arguments, or <see langword="null"/>.</param>
    /// <param name="path">The guarded file.</param>
    private static bool ChangesLevelOrCap(string? rawInputJson, string path)
    {
        if (rawInputJson is not { Length: > 0 })
        {
            return false;
        }

        Collected collected = new();
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawInputJson);
            Walk(document.RootElement, collected);
        }
        catch (JsonException)
        {
            // Not something the guard can judge; it refuses only what it can positively identify.
            return false;
        }

        if (collected.Texts.Any(NeedsYouLevelText.MentionsKey))
        {
            return true;
        }

        string? disk = ReadDisk(path);
        (NeedsYouLevel? Level, decimal? Cap) before = Read(path, disk);
        return collected.WholeContents.Any(content => Read(path, content) != before)
            || (collected.Edits.Count > 0 && Read(path, Apply(disk, collected.Edits)) != before);
    }

    /// <summary>Applies the edits in order to the file's text. An edit whose old text is not in the file is skipped: the tool would fail it.</summary>
    /// <param name="disk">The file's text, or <see langword="null"/> when it does not exist.</param>
    /// <param name="edits">The old and new text pairs.</param>
    private static string? Apply(string? disk, List<(string Old, string New, bool All)> edits)
    {
        string? text = disk;
        foreach ((string oldText, string newText, bool all) in edits)
        {
            int first = text is null || oldText.Length == 0 ? -1 : text.IndexOf(oldText, StringComparison.Ordinal);
            if (text is null || first < 0)
            {
                continue;
            }

            text = all
                ? text.Replace(oldText, newText, StringComparison.Ordinal)
                : string.Concat(text.AsSpan(0, first), newText, text.AsSpan(first + oldText.Length));
        }

        return text;
    }

    /// <summary>Reads the level and cap a <c>_team.md</c> or <c>_project.md</c> text carries.</summary>
    /// <param name="path">The file's path; its name picks the parser.</param>
    /// <param name="text">The text, or <see langword="null"/> for a missing file, which carries neither.</param>
    private static (NeedsYouLevel? Level, decimal? Cap) Read(string path, string? text)
    {
        if (text is null)
        {
            return (null, null);
        }

        if (string.Equals(Path.GetFileName(path), TeamPaths.ProjectFileName, StringComparison.OrdinalIgnoreCase))
        {
            ProjectFile project = ProjectFile.Parse(text);
            return (project.NeedsYouLevel, project.SpendCap);
        }

        return TeamFile.TryParse(text, out TeamFile? team) ? (team.NeedsYouLevel, team.SpendCap) : (null, null);
    }

    /// <summary>The file's text, or <see langword="null"/> when it is missing or cannot be read.</summary>
    /// <param name="path">A full path.</param>
    private static string? ReadDisk(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Collects the whole-file contents, the old and new text pairs and every other string argument of a call, however deeply nested (a multi-edit carries an array of pairs).</summary>
    /// <param name="element">The element being read.</param>
    /// <param name="collected">What has been found so far.</param>
    private static void Walk(JsonElement element, Collected collected)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WalkObject(element, collected);
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Walk(item, collected);
                }

                break;
            case JsonValueKind.String:
                collected.Texts.Add(element.GetString() ?? string.Empty);
                break;
            default:
                break;
        }
    }

    /// <summary>Reads one object's properties: a whole-file content, an old and new pair, a path (ignored), or any other value to walk.</summary>
    /// <param name="element">The object.</param>
    /// <param name="collected">What has been found so far.</param>
    private static void WalkObject(JsonElement element, Collected collected)
    {
        string? oldText = null;
        string? newText = null;
        bool all = false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string? text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (text is not null && Matches(WholeContentNames, property.Name))
            {
                collected.WholeContents.Add(text);
            }
            else if (text is not null && Matches(OldTextNames, property.Name))
            {
                oldText = text;
            }
            else if (text is not null && Matches(NewTextNames, property.Name))
            {
                newText = text;
            }
            else if (property.Value.ValueKind is JsonValueKind.True && property.Name.Contains("replace", StringComparison.OrdinalIgnoreCase))
            {
                all = true;
            }
            else if (!Matches(PathNames, property.Name))
            {
                Walk(property.Value, collected);
            }
        }

        if (oldText is not null || newText is not null)
        {
            collected.Texts.Add(oldText ?? string.Empty);
            collected.Texts.Add(newText ?? string.Empty);
            collected.Edits.Add((oldText ?? string.Empty, newText ?? string.Empty, all));
        }
    }

    /// <summary>Whether a property name is one of the names, ignoring case.</summary>
    /// <param name="names">The names.</param>
    /// <param name="name">The property name.</param>
    private static bool Matches(string[] names, string name) => names.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>What <see cref="Walk"/> found in one call's arguments.</summary>
    private sealed class Collected
    {
        /// <summary>Every string that is not a whole-file content or a path: old text, new text and any other argument.</summary>
        public List<string> Texts { get; } = [];

        /// <summary>The complete new contents of the file, as a write carries them.</summary>
        public List<string> WholeContents { get; } = [];

        /// <summary>The old and new text pairs, with whether every occurrence is replaced.</summary>
        public List<(string Old, string New, bool All)> Edits { get; } = [];
    }
}
