namespace Agency.Huddle.App.Services;

/// <summary>One tool call as it is written to the lasting tool-call log.</summary>
/// <param name="Time">When the call started or finished; its UTC date picks the log file.</param>
/// <param name="Room">The Room the Turn ran in.</param>
/// <param name="Task">The Task the Turn belonged to, or <see langword="null"/> when unknown.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Arguments">A summary of the arguments; cut to <see cref="ToolCallLogWriter.MaxArgumentsLength"/> characters when written.</param>
/// <param name="Status">The final status of the call.</param>
/// <param name="DurationMs">How long the call took, in milliseconds.</param>
internal sealed record ToolCallLogEntry(
    DateTimeOffset Time,
    string Room,
    string? Task,
    string Tool,
    string Arguments,
    string Status,
    long DurationMs);
