namespace Agency.Huddle.App.Services;

/// <summary>Where an appended tool call landed, so Evidence can cite <c>RelativePath#L{Line}</c>.</summary>
/// <param name="RelativePath">The log file relative to the data folder, with forward slashes.</param>
/// <param name="Line">The 1-based line of the entry in that file.</param>
internal sealed record ToolCallLogRef(string RelativePath, int Line);
