namespace Agency.Huddle.Packs;

/// <summary>What the command line asked for.</summary>
/// <param name="Command"><c>validate</c>, <c>roles</c>, <c>installed</c>, <c>skills</c>, <c>schema</c>, <c>version</c> or <c>help</c>.</param>
/// <param name="Argument">The path, the search text or the schema kind.</param>
/// <param name="DataDir">The <c>--data-dir</c> value.</param>
/// <param name="Json"><c>--json</c>.</param>
/// <param name="Quiet"><c>--quiet</c>.</param>
/// <param name="Strict"><c>--strict</c>.</param>
internal sealed record ParsedArguments(string Command, string? Argument, string? DataDir, bool Json, bool Quiet, bool Strict);
