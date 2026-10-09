namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides whether a Persona's <c>git merge</c> or <c>git push</c> may run. Consulted by
/// <see cref="WorkDirPermissionHandler"/> when the Adapter asks permission for a shell command (PR-7).
/// </summary>
internal interface IMergeGate
{
    /// <summary>Asks whether the Persona's Team allows the merge or push right now.</summary>
    /// <param name="personaName">The Persona whose Agent asked to run the command.</param>
    /// <param name="command">The shell command that merges or pushes.</param>
    /// <returns>The reason to refuse, or <see langword="null"/> when the command may run.</returns>
    string? RefusalFor(string personaName, string command);
}
