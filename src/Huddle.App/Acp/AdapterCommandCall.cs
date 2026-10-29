namespace Agency.Huddle.App.Acp;

/// <summary>An Adapter command a Human asked an Agent to run: its name, and the free text that follows it.</summary>
/// <param name="Name">The name after the slash, without it.</param>
/// <param name="Arguments">Everything after the name, trimmed; empty when there is none. Passed to the Adapter verbatim.</param>
internal sealed record AdapterCommandCall(string Name, string Arguments);
