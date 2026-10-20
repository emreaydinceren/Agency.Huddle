namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Microsoft.Extensions.Options;

/// <summary>Records one line in a Library document's history on the Agent's word (DocHistory spec §6.3).</summary>
/// <remarks>
/// Follows <see cref="WatchFolderTool"/>'s pattern: an <c>agentName</c> bound at construction, so the actor is never an
/// argument, and result texts written in code. Never throws for an expected failure; every refusal is one line the Agent can
/// act on. Nothing here checks that the document really changed (ADR-0060).
/// </remarks>
internal sealed class LogChangeTool(
    DocHistoryRecorder recorder,
    LibraryPathResolver resolver,
    IOptions<TeamOptions> options,
    TeammatePaths paths,
    string agentName,
    IPromptSource prompts) : IAppTool
{
    /// <inheritdoc />
    public string Name => "log_change";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.logChange.description");

    /// <inheritdoc />
    public JsonObject InputSchema => BuildSchema();

    /// <summary>The input schema the live tests' stand-in tool repeats verbatim (DocHistory spec §10).</summary>
    /// <returns>A new schema object.</returns>
    internal static JsonObject BuildSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["kind"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "created", "edited", "reviewed", "objected" },
            },
            ["summary"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "path", "kind", "summary" },
    };

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Task.FromResult(this.Log(arguments));
    }

    /// <summary>Checks the arguments in the order of the spec's refusal table and records the entry.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The text the Agent reads.</returns>
    private string Log(JsonObject arguments)
    {
        if (!TaskToolText.TryGetString(arguments, "path", out string? path, out string pathRefusal))
        {
            return pathRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "kind", out string? kindText, out string kindRefusal))
        {
            return kindRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "summary", out string? summaryText, out string summaryRefusal))
        {
            return summaryRefusal;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return "'path' is a required argument.";
        }

        if (!resolver.TryResolveAbsolute(this.Absolute(path), out LibraryPath? file))
        {
            return $"'{path}' is not in the Library. Only Library documents have a history.";
        }

        string? refusal = RefuseFile(path, file);
        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryParseKind(kindText, out DocChangeKind kind))
        {
            return "'kind' must be created, edited, reviewed or objected.";
        }

        int max = options.Value.DocHistory.MaxSummaryLength;
        string summary = OneLine(summaryText);
        if (summary.Length == 0 || summary.Length > max)
        {
            return $"'summary' is required, one line of at most {max} characters.";
        }

        return recorder.AgentLogged(file, agentName, kind, summary)
            ? "Logged."
            : "The change could not be recorded. The document itself is unchanged.";
    }

    /// <summary>
    /// A path the Agent gave relative, resolved against its own Work Dir, which is the folder it was told it works in. An
    /// Agent's own file tools take full paths, but it often logs the name it was given in the Message (DocHistory spec §6.3).
    /// </summary>
    /// <param name="path">The path as the Agent gave it.</param>
    /// <returns>The path unchanged when it is rooted or a <c>file:</c> URL, else the path under the Agent's Work Dir.</returns>
    private string Absolute(string path) =>
        Path.IsPathRooted(path) || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.Combine(paths.WorkDir(agentName), path);

    /// <summary>Reads one of the four kind names, in any letter case. A number is not a name.</summary>
    /// <param name="text">The Agent's text.</param>
    /// <param name="kind">The kind, when the text names one.</param>
    /// <returns>Whether <paramref name="text"/> is one of the four names.</returns>
    private static bool TryParseKind(string? text, out DocChangeKind kind)
    {
        DocChangeKind? parsed = text?.Trim().ToUpperInvariant() switch
        {
            "CREATED" => DocChangeKind.Created,
            "EDITED" => DocChangeKind.Edited,
            "REVIEWED" => DocChangeKind.Reviewed,
            "OBJECTED" => DocChangeKind.Objected,
            _ => null,
        };
        kind = parsed.GetValueOrDefault();
        return parsed is not null;
    }

    /// <summary>The refusal for a resolved path that cannot have a history, or <see langword="null"/>.</summary>
    /// <param name="path">The path as the Agent gave it.</param>
    /// <param name="file">The resolved Library path.</param>
    /// <returns>The refusal text, or <see langword="null"/> when the document can take an entry.</returns>
    private static string? RefuseFile(string path, LibraryPath file)
    {
        if (!LibraryFileKinds.IsMarkdown(file.RelativePath))
        {
            return "Only Markdown documents have a history.";
        }

        if (file.ReadOnly)
        {
            return LibraryPath.ReadOnlyReason;
        }

        return File.Exists(file.FullPath)
            ? null
            : $"'{path}' does not exist. Log a change after the file is written.";
    }

    /// <summary>Replaces line breaks with spaces and trims.</summary>
    /// <param name="text">The Agent's summary, or <see langword="null"/>.</param>
    /// <returns>The one-line summary, empty when none was given.</returns>
    private static string OneLine(string? text) =>
        (text ?? string.Empty).Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
}
