using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Modules;

/// <summary>What a module knows when it prepares a Work Dir, after the session's tools are settled.</summary>
/// <param name="Path">The Persona's Work Dir.</param>
/// <param name="DataDir">The App's data folder.</param>
/// <param name="ToolNames">The session's tool names, already prefixed.</param>
/// <param name="Identity">The Persona's parsed identity, or <see langword="null"/>.</param>
/// <param name="Services">The app's own container.</param>
/// <param name="Logger">Receives an install failure.</param>
internal sealed record ModuleWorkDir(string Path, string DataDir, IReadOnlyList<string> ToolNames, PersonaIdentity? Identity, IServiceProvider Services, ILogger Logger);
