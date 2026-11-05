namespace Agency.Huddle.App.Acp.Tools;

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

    /// <inheritdoc />
    public string Description => prompts.Render("tool.addEvidence.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["command"] = new JsonObject { ["type"] = "string" },
            ["exitCode"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "The number the command returned (0 for success). Required whenever command is given; a command without it is refused.",
            },
            ["logRef"] = new JsonObject { ["type"] = "string" },
            ["note"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "A short remark. Use it alone, with no command, when you ran nothing; with a command it rides along on the same line.",
            },
        },
        ["required"] = new JsonArray { "taskId" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        (TaskToolTarget? target, string refusal) = await TaskToolText.StartAsync(arguments, store, directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return refusal;
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

        if (!TryReadExitCode(arguments, out int? exitCode, out string exitRefusal))
        {
            return exitRefusal;
        }

        string cleanCommand = Clean(command);
        string cleanNote = Clean(note);
        if (cleanCommand.Length == 0 && cleanNote.Length == 0)
        {
            return "Give a command or a note.";
        }

        if (!TryReadExplicitLogRef(cleanCommand, logRef, out string? logPath, out int? logLine))
        {
            return "'logRef' must look like path#L12.";
        }

        if (cleanCommand.Length > 0 && exitCode is null)
        {
            return "A command needs its exit code, or the line does not count. Pass exitCode, the number the command returned (0 for success). For a remark about work where no command was run, pass note instead. Nothing was added.";
        }

        EvidenceLine line = this.BuildLine(target.Actor.Name, cleanCommand, cleanNote, exitCode, logPath, logLine);

        if (!TaskToolText.TryRun(() => tasks.AddEvidence(target.Task.Id, line, target.Actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result is TaskResult.Saved saved
            ? $"Added evidence to {saved.Task.Id}."
            : TaskToolText.Refusal(result, this.Name);
    }

    /// <summary>Reads the optional <c>exitCode</c> argument; <paramref name="exitCode"/> stays null when it is absent.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="exitCode">The parsed exit code, or null when none was given.</param>
    /// <param name="refusal">The refusal text when the value is not an integer.</param>
    private static bool TryReadExitCode(JsonObject arguments, out int? exitCode, out string refusal)
    {
        exitCode = null;
        refusal = string.Empty;
        if (arguments["exitCode"] is null)
        {
            return true;
        }

        if (!TaskToolText.TryGetInt(arguments, "exitCode", null, out int code, out refusal))
        {
            return false;
        }

        exitCode = code;
        return true;
    }

    /// <summary>Parses a caller-supplied <c>logRef</c>; it only counts when a command is given. Returns false when it is malformed.</summary>
    /// <param name="cleanCommand">The cleaned command text.</param>
    /// <param name="logRef">The raw <c>logRef</c> argument.</param>
    /// <param name="logPath">The path part, or null when no reference was supplied.</param>
    /// <param name="logLine">The line part, or null when no reference was supplied.</param>
    private static bool TryReadExplicitLogRef(string cleanCommand, string? logRef, out string? logPath, out int? logLine)
    {
        logPath = null;
        logLine = null;
        if (cleanCommand.Length == 0 || string.IsNullOrWhiteSpace(logRef))
        {
            return true;
        }

        if (!TryParseLogRef(logRef, out logPath, out int explicitLine))
        {
            return false;
        }

        logLine = explicitLine;
        return true;
    }

    /// <summary>Builds the Evidence line: a note-only line carries no clock or caller, a command line looks up the log reference when none was given.</summary>
    /// <param name="actorName">The caller's name.</param>
    /// <param name="cleanCommand">The cleaned command text, possibly empty.</param>
    /// <param name="cleanNote">The cleaned note text, possibly empty.</param>
    /// <param name="exitCode">The exit code, if any.</param>
    /// <param name="logPath">The explicit log path, if any.</param>
    /// <param name="logLine">The explicit log line, if any.</param>
    private EvidenceLine BuildLine(string actorName, string cleanCommand, string cleanNote, int? exitCode, string? logPath, int? logLine)
    {
        if (cleanCommand.Length == 0)
        {
            return new EvidenceLine(default, string.Empty, string.Empty, null, null, null, cleanNote);
        }

        if (logPath is null)
        {
            ToolCallLogRef? found = this.FindLogReference(actorName, cleanCommand);
            logPath = found?.RelativePath;
            logLine = found?.Line;
        }

        // The note stays out of the command: a Proof compares the command to what the Definition of Done names, character for character.
        return new EvidenceLine(clock.GetUtcNow(), actorName, cleanCommand, exitCode, logPath, logLine, string.Empty, cleanNote);
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
