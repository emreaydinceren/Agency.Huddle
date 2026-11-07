using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// What one Turn keeps of its tool calls: the rules for which updates count, the last JSON output and
/// the failure flag an Adapter command's outcome line is built from, and the paths File Changes counts
/// as the Agent's own. One per Turn, held by the session's active Turn. Not thread-safe and takes no
/// lock: the session's gate guards <see cref="Touched"/> (the event reader adds to it, and the run
/// task copies it once the Turn has ended), and the single-threaded event reader is the only writer
/// of everything else, read by the consumer only after the Turn's completion has been awaited.
/// </summary>
internal sealed class TurnToolCalls
{
    /// <summary>
    /// The last tool output of this Turn that was a JSON object, which is where an Adapter command
    /// reports its figures.
    /// </summary>
    public string? LastOutputJson { get; private set; }

    /// <summary>Whether any tool call of this Turn reached <see cref="ToolCallStatus.Failed"/>.</summary>
    public bool ToolCallFailed { get; private set; }

    /// <summary>The ids of the tool calls this Turn started, so an update belonging to an earlier Turn is ignored.</summary>
    public HashSet<string> ToolCallIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Full paths this Agent's own tool calls touched this Turn (FC §6.8 item 2). Changed and read
    /// only under the session's gate, and only while this Turn is the session's active one.
    /// </summary>
    public HashSet<string> Touched { get; } = new(FolderSnapshot.PathComparer);

    /// <summary>
    /// Keeps, for the Turn in flight, the last tool output that is a JSON object and whether any tool
    /// call failed, which is all an Adapter command's outcome line is built from. Only calls this Turn
    /// itself started count: a Stop or an idle timeout lets the consumer move on without waiting for
    /// the aborted Turn's tail, so a late update for an earlier Turn's call must not be read as this
    /// Turn's failure.
    /// </summary>
    /// <param name="toolCallId">The tool call the update belongs to.</param>
    /// <param name="isStart">Whether this is the call's first event, which enrols it in the Turn.</param>
    /// <param name="status">The tool call's status on this update.</param>
    /// <param name="rawOutputJson">The tool call's output, if this update carries one.</param>
    public void RecordResult(string toolCallId, bool isStart, ToolCallStatus status, string? rawOutputJson)
    {
        if (isStart)
        {
            this.ToolCallIds.Add(toolCallId);
        }
        else if (!this.ToolCallIds.Contains(toolCallId))
        {
            return;
        }

        if (status == ToolCallStatus.Failed)
        {
            this.ToolCallFailed = true;
        }

        if (rawOutputJson is not null && rawOutputJson.AsSpan().TrimStart().StartsWith('{'))
        {
            this.LastOutputJson = rawOutputJson;
        }
    }

    /// <summary>
    /// Adds every rooted path found in <paramref name="rawInputJson"/> to the <see cref="Touched"/>
    /// set, per FC §6.8 item 2 and FC D-3b - but only for
    /// <see cref="ToolKind.Edit"/>, <see cref="ToolKind.Delete"/> or <see cref="ToolKind.Move"/>: an
    /// <see cref="ToolKind.Execute"/> call is not parsed, because a shell command is not a path and
    /// guessing at one could claim another Agent's change as this Agent's own. The caller holds the
    /// session's gate.
    /// </summary>
    /// <param name="kind">The tool call's category, as ACP reported it.</param>
    /// <param name="rawInputJson">The tool call's raw input, if any.</param>
    public void RecordTouched(ToolKind kind, string? rawInputJson)
    {
        if (kind is not (ToolKind.Edit or ToolKind.Delete or ToolKind.Move))
        {
            return;
        }

        foreach (var path in TouchedPaths.From(rawInputJson))
        {
            this.Touched.Add(path);
        }
    }

    /// <summary>Adds one full path to the <see cref="Touched"/> set. The caller holds the session's gate.</summary>
    /// <param name="path">The full path to count as touched.</param>
    public void AddTouched(string path) => this.Touched.Add(path);
}
