namespace Agency.Huddle.Console;

using Agency.Huddle.Acp.Abstractions;

/// <summary>Options controlling how a <see cref="Repl"/> starts and runs a session.</summary>
internal sealed record ReplOptions(string Cwd, bool AutoApprove, SystemPromptOptions? SystemPrompt = null, bool EnableTools = false);
