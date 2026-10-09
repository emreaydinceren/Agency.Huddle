namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Adds a line to a Task's append-only "## Evidence" section (Spec Evidence). The line carries the caller's name, the
/// clock and, when a <c>command</c> is given, the newest matching entry of the caller's own tool-call log.
/// </summary>
internal sealed class AddEvidenceTool(
    TaskService tasks,
    TaskStore store,
    ToolCallLogWriter log,
    ITeamDirectory directory,
    TimeProvider clock,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private const string FieldSeparator = " | ";
    private const string LogMarker = "#L";

    /// <inheritdoc />
    public string Name => "add_evidence";

    /// <summary>The placeholder values a prompt renders with; the tool descriptions take none.</summary>
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Description => prompts.Render("tool.addEvidence.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["command"] = new JsonObject { ["type"] = "string" },
            ["exitCode"] = new JsonObject { ["type"] = "integer" },
            ["logRef"] = new JsonObject { ["type"] = "string" },
            ["note"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryResolve(store, taskIdText, out TaskItem? current, out string resolveRefusal))
        {
            return resolveRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "command", out string? command, out string commandRefusal))
        {
            return commandRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "note", out string? note, out string noteRefusal))
        {
            return noteRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "logRef", out string? logRef, out string logRefRefusal))
        {
            return logRefRefusal;
        }

        int? exitCode = null;
        if (arguments["exitCode"] is not null)
        {
            if (!TaskToolText.TryGetInt(arguments, "exitCode", null, out int code, out string exitRefusal))
            {
                return exitRefusal;
            }

            exitCode = code;
        }

        string cleanCommand = Clean(command);
        string cleanNote = Clean(note);
        if (cleanCommand.Length == 0 && cleanNote.Length == 0)
        {
            return "Give a command or a note.";
        }

        string? logPath = null;
        int? logLine = null;
        if (cleanCommand.Length > 0 && !string.IsNullOrWhiteSpace(logRef))
        {
            if (!TryParseLogRef(logRef, out logPath, out int explicitLine))
            {
                return "'logRef' must look like path#L12.";
            }

            logLine = explicitLine;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        EvidenceLine line;
        if (cleanCommand.Length == 0)
        {
            line = new EvidenceLine(default, string.Empty, string.Empty, null, null, null, cleanNote);
        }
        else
        {
            if (logPath is null)
            {
                ToolCallLogRef? found = this.FindLogReference(actor.Name, cleanCommand);
                logPath = found?.RelativePath;
                logLine = found?.Line;
            }

            string text = cleanNote.Length == 0 ? cleanCommand : string.Create(CultureInfo.InvariantCulture, $"{cleanCommand} ({cleanNote})");
            line = new EvidenceLine(clock.GetUtcNow(), actor.Name, text, exitCode, logPath, logLine, string.Empty);
        }

        if (!TaskToolText.TryRun(() => tasks.AddEvidence(current.Id, line, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result switch
        {
            TaskResult.Saved saved => $"Added evidence to {saved.Task.Id}.",
            TaskResult.NotFound notFound => $"Unknown task '{notFound.Id}'.",
            TaskResult.Conflict conflict => $"{conflict.Current.Id} was changed by someone else; please try again.",
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            _ => throw new UnreachableException("add_evidence's TaskService.AddEvidence never returns Unchanged."),
        };
    }

    /// <summary>Collapses line breaks and replaces the Evidence field separator so the text stays one field of one line.</summary>
    private static string Clean(string? text) =>
        (text ?? string.Empty).ReplaceLineEndings(" ").Replace(FieldSeparator, " / ", StringComparison.Ordinal).Trim();

    /// <summary>Splits <c>path#L12</c> into its path and line number.</summary>
    private static bool TryParseLogRef(string logRef, out string path, out int line)
    {
        int marker = logRef.LastIndexOf(LogMarker, StringComparison.Ordinal);
        path = marker > 0 ? logRef[..marker].Trim() : string.Empty;
        line = 0;
        return path.Length > 0
            && int.TryParse(logRef[(marker + LogMarker.Length)..], NumberStyles.None, CultureInfo.InvariantCulture, out line);
    }

    /// <summary>The newest log entry of the caller that matches the command, or null when there is none or the log cannot be read.</summary>
    private ToolCallLogRef? FindLogReference(string personaName, string command)
    {
        try
        {
            return log.FindLatest(personaName, command);
        }
        catch (IOException)
        {
            // An unreadable log only means the line carries no log reference; the Evidence is still worth recording.
            return null;
        }
    }
}
