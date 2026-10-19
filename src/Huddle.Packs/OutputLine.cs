namespace Agency.Huddle.Packs;

/// <summary>One line of <c>validate</c> output before the columns are padded.</summary>
/// <param name="Severity"><c>ERROR</c>, <c>WARN</c> or <c>OK</c>.</param>
/// <param name="Code">The code; empty for <c>OK</c>.</param>
/// <param name="File">The file as shown.</param>
/// <param name="Message">The message on one line.</param>
internal sealed record OutputLine(string Severity, string Code, string File, string Message);
