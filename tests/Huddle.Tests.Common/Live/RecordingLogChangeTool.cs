using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Live;

/// <summary>One call a live Agent made to <c>log_change</c>.</summary>
/// <param name="Arguments">The arguments exactly as the model sent them.</param>
/// <param name="Result">What the tool answered.</param>
internal sealed record LoggedCall(JsonObject Arguments, string Result);

/// <summary>
/// A <c>log_change</c> that records every call (DocHistory spec §10, DH-T0). It has the shipped name, input schema and
/// description, so a live model meets exactly what it will meet once the real tool is offered. Without an inner tool it
/// answers <c>Logged.</c> to a call the real tool would accept and the real tool's own refusal text to a call it would
/// refuse (outside <see cref="Folder"/>; a relative path is taken to be under it, as the real tool takes it under the Work Dir); given the real tool, it passes each call through and records what that returned (DH-T9).
/// </summary>
internal sealed class RecordingLogChangeTool(IAppTool? inner = null) : IAppTool
{
    private readonly List<LoggedCall> calls = [];
    private readonly Lock gate = new();

    /// <summary>The working folder of the run in progress; a path outside it is refused. <see langword="null"/> accepts any rooted path.</summary>
    public string? Folder { get; set; }

    /// <summary>The calls so far, in order.</summary>
    public IReadOnlyList<LoggedCall> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc />
    public string Name => "log_change";

    /// <inheritdoc />
    public string Description => PromptCatalog.Get("tool.logChange.description").Default;

    /// <inheritdoc />
    public JsonObject InputSchema => LogChangeTool.BuildSchema();

    /// <summary>Answers as the real tool would for a path it cannot place in the Library.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns><c>Logged.</c> or the real tool's refusal.</returns>
    private string Standin(JsonObject arguments)
    {
        string path = (string?)arguments["path"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "'path' is a required argument.";
        }

        string rooted = Path.IsPathRooted(path) || this.Folder is null ? path : Path.Combine(this.Folder, path);
        bool inLibrary = Path.IsPathFullyQualified(rooted)
            && (this.Folder is null || Path.GetFullPath(rooted).StartsWith(Path.GetFullPath(this.Folder), StringComparison.OrdinalIgnoreCase));
        if (!inLibrary)
        {
            return $"'{path}' is not in the Library. Only Library documents have a history.";
        }

        string kind = ((string?)arguments["kind"] ?? string.Empty).Trim().ToUpperInvariant();
        if (kind is not ("CREATED" or "EDITED" or "REVIEWED" or "OBJECTED"))
        {
            return "'kind' must be created, edited, reviewed or objected.";
        }

        string summary = ((string?)arguments["summary"] ?? string.Empty).Trim();
        return summary.Length is 0 or > 200 ? "'summary' is required, one line of at most 200 characters." : "Logged.";
    }

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        string result = inner is null ? this.Standin(arguments) : await inner.InvokeAsync(arguments, cancellationToken);
        lock (this.gate)
        {
            this.calls.Add(new LoggedCall((JsonObject)arguments.DeepClone(), result));
        }

        return result;
    }
}
